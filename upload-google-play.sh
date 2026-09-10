#!/bin/bash

set -euo pipefail

PACKAGE_NAME="${GOOGLE_PLAY_PACKAGE_NAME:-mx.contabee.app}"
TRACK="${GOOGLE_PLAY_TRACK:-internal}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
AAB_DIRECTORY="$SCRIPT_DIR/ContaBeeMovil/bin/Release/net10.0-android/publish"
WORK_DIR="$(mktemp -d /tmp/contabee-google-play.XXXXXX)"
SERVICE_ACCOUNT_PATH="$WORK_DIR/service-account.json"
PRIVATE_KEY_PATH="$WORK_DIR/private-key.pem"
TOKEN_RESPONSE_PATH="$WORK_DIR/token-response.json"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

if [ -z "${GOOGLE_PLAY_SERVICE_ACCOUNT_JSON:-}" ]; then
  echo "ERROR: Falta el secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON."
  exit 1
fi

printf '%s' "$GOOGLE_PLAY_SERVICE_ACCOUNT_JSON" > "$SERVICE_ACCOUNT_PATH"
chmod 600 "$SERVICE_ACCOUNT_PATH"

if ! jq -e '
  .type == "service_account" and
  (.client_email | type == "string" and length > 0) and
  (.private_key | type == "string" and length > 0)
' "$SERVICE_ACCOUNT_PATH" > /dev/null; then
  echo "ERROR: GOOGLE_PLAY_SERVICE_ACCOUNT_JSON no contiene una cuenta de servicio válida."
  exit 1
fi

client_email="$(jq -r '.client_email' "$SERVICE_ACCOUNT_PATH")"
jq -r '.private_key' "$SERVICE_ACCOUNT_PATH" > "$PRIVATE_KEY_PATH"
chmod 600 "$PRIVATE_KEY_PATH"

base64url() {
  openssl base64 -A | tr '+/' '-_' | tr -d '='
}

issued_at="$(date +%s)"
expires_at="$((issued_at + 3600))"
token_uri="https://oauth2.googleapis.com/token"
scope="https://www.googleapis.com/auth/androidpublisher"

jwt_header="$(printf '%s' '{"alg":"RS256","typ":"JWT"}' | base64url)"
jwt_claims="$(
  jq -cn \
    --arg iss "$client_email" \
    --arg scope "$scope" \
    --arg aud "$token_uri" \
    --argjson iat "$issued_at" \
    --argjson exp "$expires_at" \
    '{iss: $iss, scope: $scope, aud: $aud, iat: $iat, exp: $exp}' \
    | base64url
)"
unsigned_jwt="$jwt_header.$jwt_claims"
jwt_signature="$(
  printf '%s' "$unsigned_jwt" \
    | openssl dgst -sha256 -sign "$PRIVATE_KEY_PATH" -binary \
    | base64url
)"
signed_jwt="$unsigned_jwt.$jwt_signature"

echo "==> Autenticando la cuenta de servicio en Google Play..."
token_status="$(
  curl --silent --show-error \
    --output "$TOKEN_RESPONSE_PATH" \
    --write-out '%{http_code}' \
    --request POST \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer' \
    --data-urlencode "assertion=$signed_jwt" \
    "$token_uri"
)"

if [[ "$token_status" != 2* ]]; then
  echo "ERROR: Google rechazó la autenticación (HTTP $token_status)."
  jq -c '{error, error_description}' "$TOKEN_RESPONSE_PATH" 2>/dev/null || true
  exit 1
fi

access_token="$(jq -r '.access_token // empty' "$TOKEN_RESPONSE_PATH")"
if [ -z "$access_token" ]; then
  echo "ERROR: Google no devolvió un access token."
  exit 1
fi

mapfile -t aab_files < <(find "$AAB_DIRECTORY" -maxdepth 1 -type f -name '*-Signed.aab' -print)
if [ "${#aab_files[@]}" -ne 1 ]; then
  echo "ERROR: Se esperaba exactamente un AAB firmado en $AAB_DIRECTORY y se encontraron ${#aab_files[@]}."
  exit 1
fi
aab_path="${aab_files[0]}"

api_root="https://androidpublisher.googleapis.com/androidpublisher/v3/applications/$PACKAGE_NAME"
upload_root="https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications/$PACKAGE_NAME"
edit_response_path="$WORK_DIR/edit-response.json"
current_track_path="$WORK_DIR/current-track.json"
bundle_response_path="$WORK_DIR/bundle-response.json"
track_request_path="$WORK_DIR/track-request.json"
track_response_path="$WORK_DIR/track-response.json"
validation_response_path="$WORK_DIR/validation-response.json"
commit_response_path="$WORK_DIR/commit-response.json"

google_request() {
  local method="$1"
  local url="$2"
  local output_path="$3"
  shift 3

  local status
  status="$(
    curl --silent --show-error \
      --output "$output_path" \
      --write-out '%{http_code}' \
      --request "$method" \
      --header "Authorization: Bearer $access_token" \
      "$@" \
      "$url"
  )"

  if [[ "$status" != 2* ]]; then
    echo "ERROR: Google Play Developer API respondió HTTP $status."
    jq -c '.error // .' "$output_path" 2>/dev/null || true
    return 1
  fi
}

echo "==> Creando edición para $PACKAGE_NAME..."
google_request \
  POST \
  "$api_root/edits" \
  "$edit_response_path" \
  --header 'Content-Type: application/json' \
  --data '{}'

edit_id="$(jq -r '.id // empty' "$edit_response_path")"
if [ -z "$edit_id" ]; then
  echo "ERROR: Google Play no devolvió el identificador de la edición."
  exit 1
fi

echo "==> Revisando la pista $TRACK..."
google_request \
  GET \
  "$api_root/edits/$edit_id/tracks/$TRACK" \
  "$current_track_path"

if jq -e '.releases // [] | any(.status == "draft")' "$current_track_path" > /dev/null; then
  echo "ERROR: Ya existe un borrador en la pista $TRACK."
  echo "Resuélvelo o elimínalo desde Play Console antes de crear otro."
  exit 1
fi

echo "==> Subiendo $(basename "$aab_path")..."
google_request \
  POST \
  "$upload_root/edits/$edit_id/bundles?uploadType=media" \
  "$bundle_response_path" \
  --header 'Content-Type: application/octet-stream' \
  --data-binary "@$aab_path"

version_code="$(jq -r '.versionCode // empty' "$bundle_response_path")"
if [ -z "$version_code" ]; then
  echo "ERROR: Google Play no devolvió el código de versión del AAB."
  exit 1
fi

jq \
  --arg track "$TRACK" \
  --arg version_code "$version_code" \
  '.track = $track |
   .releases = ((.releases // []) + [{versionCodes: [$version_code], status: "draft"}])' \
  "$current_track_path" \
  > "$track_request_path"

echo "==> Creando borrador $TRACK para la versión $version_code..."
google_request \
  PUT \
  "$api_root/edits/$edit_id/tracks/$TRACK" \
  "$track_response_path" \
  --header 'Content-Type: application/json' \
  --data-binary "@$track_request_path"

echo "==> Validando la edición en Google Play..."
google_request \
  POST \
  "$api_root/edits/$edit_id:validate" \
  "$validation_response_path" \
  --header 'Content-Type: application/json' \
  --data '{}'

echo "==> Confirmando edición sin publicar a usuarios..."
google_request \
  POST \
  "$api_root/edits/$edit_id:commit" \
  "$commit_response_path" \
  --header 'Content-Type: application/json' \
  --data '{}'

echo ""
echo "✓ AAB versión $version_code cargado en Google Play."
echo "  Pista: $TRACK"
echo "  Estado: borrador (no disponible para usuarios)"

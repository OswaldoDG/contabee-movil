#!/bin/bash

# Valida o envía a revisión una versión ya cargada en Google Play.
set -euo pipefail

PACKAGE_NAME="${GOOGLE_PLAY_PACKAGE_NAME:-mx.contabee.app}"
VERSION_CODE="${STORE_VERSION_CODE:-}"
VERSION_NAME="${STORE_VERSION_NAME:-}"
RELEASE_NOTES="${STORE_RELEASE_NOTES:-}"
RELEASE_NOTES_LANGUAGE="${GOOGLE_PLAY_RELEASE_NOTES_LANGUAGE:-es-419}"
SUBMIT_REVIEW="${STORE_SUBMIT_REVIEW:-false}"
CONFIRMATION="${STORE_REVIEW_CONFIRMATION:-}"
WORK_DIR="$(mktemp -d /tmp/contabee-google-review.XXXXXX)"
SERVICE_ACCOUNT_PATH="$WORK_DIR/service-account.json"
PRIVATE_KEY_PATH="$WORK_DIR/private-key.pem"
TOKEN_RESPONSE_PATH="$WORK_DIR/token-response.json"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

for command_name in curl jq openssl; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "ERROR: No se encontró el comando requerido: $command_name."
    exit 1
  fi
done

if [ -z "${GOOGLE_PLAY_RELEASE_SERVICE_ACCOUNT_JSON:-}" ]; then
  echo "ERROR: Falta el secret GOOGLE_PLAY_RELEASE_SERVICE_ACCOUNT_JSON."
  exit 1
fi

if [[ ! "$VERSION_CODE" =~ ^[0-9]+$ ]]; then
  echo "ERROR: STORE_VERSION_CODE debe ser un número entero positivo."
  exit 1
fi

if [[ ! "$VERSION_NAME" =~ ^[0-9]+(\.[0-9]+){1,2}$ ]]; then
  echo "ERROR: STORE_VERSION_NAME debe tener un formato como 2.5.11."
  exit 1
fi

if [ -z "$RELEASE_NOTES" ] || [ "${#RELEASE_NOTES}" -gt 500 ]; then
  echo "ERROR: STORE_RELEASE_NOTES debe contener entre 1 y 500 caracteres."
  exit 1
fi

if [ "$SUBMIT_REVIEW" != "true" ] && [ "$SUBMIT_REVIEW" != "false" ]; then
  echo "ERROR: STORE_SUBMIT_REVIEW sólo admite true o false."
  exit 1
fi

if [ "$SUBMIT_REVIEW" = "true" ] && [ "$CONFIRMATION" != "PUBLICAR" ]; then
  echo "ERROR: La confirmación no coincide con PUBLICAR."
  exit 1
fi

printf '%s' "$GOOGLE_PLAY_RELEASE_SERVICE_ACCOUNT_JSON" > "$SERVICE_ACCOUNT_PATH"
chmod 600 "$SERVICE_ACCOUNT_PATH"

if ! jq -e '
  .type == "service_account" and
  (.client_email | type == "string" and length > 0) and
  (.private_key | type == "string" and length > 0)
' "$SERVICE_ACCOUNT_PATH" > /dev/null; then
  echo "ERROR: GOOGLE_PLAY_RELEASE_SERVICE_ACCOUNT_JSON no contiene una cuenta de servicio válida."
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

echo "==> Autenticando la cuenta de publicación en Google Play..."
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

api_root="https://androidpublisher.googleapis.com/androidpublisher/v3/applications/$PACKAGE_NAME"
edit_response_path="$WORK_DIR/edit-response.json"
bundles_response_path="$WORK_DIR/bundles-response.json"
internal_track_path="$WORK_DIR/internal-track.json"
production_track_path="$WORK_DIR/production-track.json"
track_request_path="$WORK_DIR/track-request.json"
track_response_path="$WORK_DIR/track-response.json"
validation_response_path="$WORK_DIR/validation-response.json"
commit_response_path="$WORK_DIR/commit-response.json"
delete_response_path="$WORK_DIR/delete-response.json"

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

echo "==> Creando una edición temporal para $PACKAGE_NAME..."
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

echo "==> Comprobando el AAB $VERSION_NAME ($VERSION_CODE)..."
google_request \
  GET \
  "$api_root/edits/$edit_id/bundles" \
  "$bundles_response_path"

if ! jq -e --arg version_code "$VERSION_CODE" '
  .bundles // [] | any((.versionCode | tostring) == $version_code)
' "$bundles_response_path" >/dev/null; then
  echo "ERROR: El versionCode $VERSION_CODE no existe entre los AAB de Google Play."
  exit 1
fi

echo "==> Verificando que la versión esté en la pista interna..."
google_request \
  GET \
  "$api_root/edits/$edit_id/tracks/internal" \
  "$internal_track_path"

if ! jq -e --arg version_code "$VERSION_CODE" '
  [.releases[]?.versionCodes[]? | tostring] | index($version_code) != null
' "$internal_track_path" >/dev/null; then
  echo "ERROR: La versión $VERSION_CODE no está cargada en la pista interna."
  exit 1
fi

echo "==> Revisando el estado actual de producción..."
google_request \
  GET \
  "$api_root/edits/$edit_id/tracks/production" \
  "$production_track_path"

if jq -e --arg version_code "$VERSION_CODE" '
  [.releases[]?.versionCodes[]? | tostring] | index($version_code) != null
' "$production_track_path" >/dev/null; then
  echo "ERROR: La versión $VERSION_CODE ya está asociada a la pista de producción."
  echo "Revísala en Play Console antes de volver a ejecutar esta Action."
  exit 1
fi

if jq -e '.releases // [] | any(.status != "completed")' "$production_track_path" >/dev/null; then
  echo "ERROR: Producción ya contiene una entrega draft, inProgress o halted."
  echo "No se modificó para evitar interferir con otra publicación."
  exit 1
fi

release_name="${GOOGLE_PLAY_RELEASE_NAME:-ContaBee $VERSION_NAME ($VERSION_CODE)}"
jq -n \
  --arg version_code "$VERSION_CODE" \
  --arg release_name "$release_name" \
  --arg language "$RELEASE_NOTES_LANGUAGE" \
  --arg release_notes "$RELEASE_NOTES" \
  '{
    track: "production",
    releases: [{
      name: $release_name,
      versionCodes: [$version_code],
      status: "completed",
      releaseNotes: [{language: $language, text: $release_notes}]
    }]
  }' > "$track_request_path"

echo "==> Preparando la entrega de producción dentro de la edición..."
google_request \
  PUT \
  "$api_root/edits/$edit_id/tracks/production" \
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

if [ "$SUBMIT_REVIEW" = "false" ]; then
  google_request \
    DELETE \
    "$api_root/edits/$edit_id" \
    "$delete_response_path"

  echo ""
  echo "✓ Validación de Android terminada correctamente."
  echo "  Versión: $VERSION_NAME ($VERSION_CODE)"
  echo "  La edición temporal fue eliminada; no se envió a revisión ni se publicó."
  exit 0
fi

echo "==> Enviando los cambios a revisión sin interrumpir otra revisión activa..."
google_request \
  POST \
  "$api_root/edits/$edit_id:commit?changesNotSentForReview=false&changesInReviewBehavior=ERROR_IF_IN_REVIEW" \
  "$commit_response_path" \
  --header 'Content-Type: application/json' \
  --data '{}'

echo ""
echo "✓ Android $VERSION_NAME ($VERSION_CODE) enviado a revisión."
echo "  Destino: producción"
echo "  Publicación esperada: retenida por Publicación gestionada de Google Play"
echo "  Esta Action no publicó la versión manualmente."

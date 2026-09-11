#!/bin/bash

# Valida o envía a revisión una versión ya cargada en App Store Connect.
set -euo pipefail

BUNDLE_ID="${APP_STORE_BUNDLE_ID:-mx.contabee.app}"
VERSION_NAME="${STORE_VERSION_NAME:-}"
BUILD_NUMBER="${STORE_VERSION_CODE:-}"
RELEASE_NOTES="${STORE_RELEASE_NOTES:-}"
SUBMIT_REVIEW="${STORE_SUBMIT_REVIEW:-false}"
CONFIRMATION="${STORE_REVIEW_CONFIRMATION:-}"
WAIT_FOR_PROCESSING="${STORE_WAIT_FOR_PROCESSING:-false}"
KEY_ID="${APP_STORE_CONNECT_REVIEW_KEY_ID:-}"
ISSUER_ID="${APP_STORE_CONNECT_REVIEW_ISSUER_ID:-}"
PRIVATE_KEY_BASE64="${APP_STORE_CONNECT_REVIEW_PRIVATE_KEY_BASE64:-}"
WORK_DIR="$(mktemp -d /tmp/contabee-apple-review.XXXXXX)"
PRIVATE_KEY_PATH="$WORK_DIR/AuthKey_$KEY_ID.p8"
API_ROOT="https://api.appstoreconnect.apple.com/v1"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

for command_name in base64 curl jq node openssl; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "ERROR: No se encontró el comando requerido: $command_name."
    exit 1
  fi
done

for variable_name in \
  APP_STORE_CONNECT_REVIEW_KEY_ID \
  APP_STORE_CONNECT_REVIEW_ISSUER_ID \
  APP_STORE_CONNECT_REVIEW_PRIVATE_KEY_BASE64; do
  if [ -z "${!variable_name:-}" ]; then
    echo "ERROR: Falta el secret $variable_name."
    exit 1
  fi
done

if [[ ! "$KEY_ID" =~ ^[A-Za-z0-9]+$ ]]; then
  echo "ERROR: APP_STORE_CONNECT_REVIEW_KEY_ID tiene un formato inválido."
  exit 1
fi

if [[ ! "$ISSUER_ID" =~ ^[A-Fa-f0-9-]+$ ]]; then
  echo "ERROR: APP_STORE_CONNECT_REVIEW_ISSUER_ID tiene un formato inválido."
  exit 1
fi

if [[ ! "$VERSION_NAME" =~ ^[0-9]+(\.[0-9]+){1,2}$ ]]; then
  echo "ERROR: STORE_VERSION_NAME debe tener un formato como 2.5.11."
  exit 1
fi

if [[ ! "$BUILD_NUMBER" =~ ^[0-9]+$ ]]; then
  echo "ERROR: STORE_VERSION_CODE debe ser un número entero positivo."
  exit 1
fi

if [ -z "$RELEASE_NOTES" ] || [ "${#RELEASE_NOTES}" -gt 4000 ]; then
  echo "ERROR: STORE_RELEASE_NOTES debe contener entre 1 y 4000 caracteres."
  exit 1
fi

if [ "$SUBMIT_REVIEW" != "true" ] && [ "$SUBMIT_REVIEW" != "false" ]; then
  echo "ERROR: STORE_SUBMIT_REVIEW sólo admite true o false."
  exit 1
fi

if [ "$WAIT_FOR_PROCESSING" != "true" ] && [ "$WAIT_FOR_PROCESSING" != "false" ]; then
  echo "ERROR: STORE_WAIT_FOR_PROCESSING sólo admite true o false."
  exit 1
fi

if [ "$SUBMIT_REVIEW" = "true" ] && [ "$CONFIRMATION" != "ENVIAR A REVISION" ]; then
  echo "ERROR: La confirmación no coincide con ENVIAR A REVISION."
  exit 1
fi

if ! printf '%s' "$PRIVATE_KEY_BASE64" | base64 --decode > "$PRIVATE_KEY_PATH" 2>/dev/null; then
  if ! printf '%s' "$PRIVATE_KEY_BASE64" | base64 -D > "$PRIVATE_KEY_PATH" 2>/dev/null; then
    echo "ERROR: APP_STORE_CONNECT_REVIEW_PRIVATE_KEY_BASE64 no contiene Base64 válido."
    exit 1
  fi
fi
chmod 600 "$PRIVATE_KEY_PATH"

if ! openssl pkey -in "$PRIVATE_KEY_PATH" -noout >/dev/null 2>&1; then
  echo "ERROR: La clave privada reconstruida no es un archivo P8 válido."
  exit 1
fi

access_token="$(
  APPLE_KEY_ID="$KEY_ID" \
  APPLE_ISSUER_ID="$ISSUER_ID" \
  APPLE_PRIVATE_KEY_PATH="$PRIVATE_KEY_PATH" \
  node <<'NODE'
const crypto = require('crypto');
const fs = require('fs');

const encode = value => Buffer.from(value).toString('base64url');
const now = Math.floor(Date.now() / 1000);
const header = encode(JSON.stringify({
  alg: 'ES256',
  kid: process.env.APPLE_KEY_ID,
  typ: 'JWT'
}));
const payload = encode(JSON.stringify({
  iss: process.env.APPLE_ISSUER_ID,
  iat: now,
  exp: now + 1190,
  aud: 'appstoreconnect-v1'
}));
const signingInput = `${header}.${payload}`;
const signature = crypto.sign(
  'sha256',
  Buffer.from(signingInput),
  {
    key: fs.readFileSync(process.env.APPLE_PRIVATE_KEY_PATH),
    dsaEncoding: 'ieee-p1363'
  }
);
process.stdout.write(`${signingInput}.${signature.toString('base64url')}`);
NODE
)"

if [ -z "$access_token" ]; then
  echo "ERROR: No se pudo generar el token de App Store Connect."
  exit 1
fi

app_store_request() {
  local method="$1"
  local path="$2"
  local output_path="$3"
  local input_path="${4:-}"
  local curl_arguments=(
    --silent
    --show-error
    --globoff
    --output "$output_path"
    --write-out '%{http_code}'
    --request "$method"
    --header "Authorization: Bearer $access_token"
    --header 'Content-Type: application/json'
  )

  if [ -n "$input_path" ]; then
    curl_arguments+=(--data-binary "@$input_path")
  fi

  local status
  status="$(curl "${curl_arguments[@]}" "$API_ROOT$path")"

  if [[ "$status" != 2* ]]; then
    echo "ERROR: App Store Connect API respondió HTTP $status en $method $path."
    jq -c '[.errors[]? | {status, code, title, detail, source}]' "$output_path" 2>/dev/null || true
    return 1
  fi
}

apps_response_path="$WORK_DIR/apps.json"
builds_response_path="$WORK_DIR/builds.json"
versions_response_path="$WORK_DIR/versions.json"
version_create_request_path="$WORK_DIR/version-create-request.json"
version_create_response_path="$WORK_DIR/version-create-response.json"
version_update_request_path="$WORK_DIR/version-update-request.json"
version_update_response_path="$WORK_DIR/version-update-response.json"
localizations_response_path="$WORK_DIR/localizations.json"
localization_update_request_path="$WORK_DIR/localization-update-request.json"
localization_update_response_path="$WORK_DIR/localization-update-response.json"
build_link_request_path="$WORK_DIR/build-link-request.json"
build_link_response_path="$WORK_DIR/build-link-response.json"
submissions_response_path="$WORK_DIR/submissions.json"
submission_create_request_path="$WORK_DIR/submission-create-request.json"
submission_create_response_path="$WORK_DIR/submission-create-response.json"
submission_items_response_path="$WORK_DIR/submission-items.json"
submission_item_request_path="$WORK_DIR/submission-item-request.json"
submission_item_response_path="$WORK_DIR/submission-item-response.json"
submission_update_request_path="$WORK_DIR/submission-update-request.json"
submission_update_response_path="$WORK_DIR/submission-update-response.json"

echo "==> Buscando la app $BUNDLE_ID en App Store Connect..."
app_store_request \
  GET \
  "/apps?filter%5BbundleId%5D=$BUNDLE_ID&fields%5Bapps%5D=name,bundleId,primaryLocale&limit=2" \
  "$apps_response_path"

app_count="$(jq '.data | length' "$apps_response_path")"
if [ "$app_count" -ne 1 ]; then
  echo "ERROR: Se esperaba una app para $BUNDLE_ID y Apple devolvió $app_count."
  exit 1
fi

app_id="$(jq -r '.data[0].id' "$apps_response_path")"
primary_locale="$(jq -r '.data[0].attributes.primaryLocale // empty' "$apps_response_path")"

echo "==> Buscando el build $VERSION_NAME ($BUILD_NUMBER)..."
processing_attempt=1
max_processing_attempts=60
while true; do
  app_store_request \
    GET \
    "/builds?filter%5Bapp%5D=$app_id&filter%5Bversion%5D=$BUILD_NUMBER&filter%5BpreReleaseVersion.version%5D=$VERSION_NAME&filter%5BpreReleaseVersion.platform%5D=IOS&filter%5BbuildAudienceType%5D=APP_STORE_ELIGIBLE&fields%5Bbuilds%5D=version,processingState,uploadedDate,expired&limit=2" \
    "$builds_response_path"

  build_count="$(jq '.data | length' "$builds_response_path")"
  processing_state="$(jq -r '.data[0].attributes.processingState // "NOT_FOUND"' "$builds_response_path")"

  if [ "$build_count" -eq 1 ] && [ "$processing_state" = "VALID" ]; then
    break
  fi

  if [ "$build_count" -gt 1 ]; then
    echo "ERROR: Apple devolvió más de un build para $VERSION_NAME ($BUILD_NUMBER)."
    exit 1
  fi

  if [ "$processing_state" = "FAILED" ] || [ "$processing_state" = "INVALID" ]; then
    echo "ERROR: Apple terminó de procesar el build con estado $processing_state."
    exit 1
  fi

  if [ "$WAIT_FOR_PROCESSING" != "true" ] || [ "$processing_attempt" -ge "$max_processing_attempts" ]; then
    echo "ERROR: El build todavía no está listo. Estado: $processing_state."
    echo "Espera a que App Store Connect termine de procesarlo y vuelve a ejecutar la Action."
    exit 1
  fi

  echo "    Apple aún procesa el build ($processing_state). Reintento $processing_attempt/$max_processing_attempts en 30 segundos..."
  processing_attempt="$((processing_attempt + 1))"
  sleep 30
done

build_id="$(jq -r '.data[0].id' "$builds_response_path")"

echo "==> Buscando la versión $VERSION_NAME de iOS..."
app_store_request \
  GET \
  "/apps/$app_id/appStoreVersions?filter%5Bplatform%5D=IOS&filter%5BversionString%5D=$VERSION_NAME&fields%5BappStoreVersions%5D=versionString,appStoreState,appVersionState,releaseType&limit=2" \
  "$versions_response_path"

version_count="$(jq '.data | length' "$versions_response_path")"
if [ "$version_count" -gt 1 ]; then
  echo "ERROR: Apple devolvió más de una versión iOS $VERSION_NAME."
  exit 1
fi

if [ "$SUBMIT_REVIEW" = "false" ]; then
  echo ""
  echo "✓ Validación de iOS terminada correctamente."
  echo "  App: $BUNDLE_ID"
  echo "  Build válido: $VERSION_NAME ($BUILD_NUMBER)"
  if [ "$version_count" -eq 0 ]; then
    echo "  La versión de App Store todavía no existe; se creará con liberación manual al enviar."
  else
    version_state="$(jq -r '.data[0].attributes.appVersionState // .data[0].attributes.appStoreState // "DESCONOCIDO"' "$versions_response_path")"
    echo "  Estado actual de la versión: $version_state"
  fi
  echo "  No se creó ni modificó información y no se envió a revisión."
  exit 0
fi

if [ "$version_count" -eq 0 ]; then
  echo "==> Creando la versión $VERSION_NAME con liberación manual..."
  jq -n \
    --arg version_name "$VERSION_NAME" \
    --arg app_id "$app_id" \
    '{
      data: {
        type: "appStoreVersions",
        attributes: {
          platform: "IOS",
          versionString: $version_name,
          releaseType: "MANUAL"
        },
        relationships: {
          app: {data: {type: "apps", id: $app_id}}
        }
      }
    }' > "$version_create_request_path"

  app_store_request \
    POST \
    "/appStoreVersions" \
    "$version_create_response_path" \
    "$version_create_request_path"

  version_id="$(jq -r '.data.id // empty' "$version_create_response_path")"
  version_state="$(jq -r '.data.attributes.appVersionState // .data.attributes.appStoreState // "PREPARE_FOR_SUBMISSION"' "$version_create_response_path")"
else
  version_id="$(jq -r '.data[0].id' "$versions_response_path")"
  version_state="$(jq -r '.data[0].attributes.appVersionState // .data[0].attributes.appStoreState // "DESCONOCIDO"' "$versions_response_path")"

  case "$version_state" in
    WAITING_FOR_REVIEW|IN_REVIEW)
      echo "✓ iOS $VERSION_NAME ($BUILD_NUMBER) ya está en estado $version_state."
      exit 0
      ;;
    PREPARE_FOR_SUBMISSION|READY_FOR_REVIEW)
      ;;
    *)
      echo "ERROR: La versión $VERSION_NAME está en estado $version_state y no se puede enviar con seguridad."
      exit 1
      ;;
  esac

  echo "==> Confirmando liberación manual para la versión existente..."
  jq -n \
    --arg version_id "$version_id" \
    '{data: {type: "appStoreVersions", id: $version_id, attributes: {releaseType: "MANUAL"}}}' \
    > "$version_update_request_path"

  app_store_request \
    PATCH \
    "/appStoreVersions/$version_id" \
    "$version_update_response_path" \
    "$version_update_request_path"
fi

echo "==> Actualizando las notas de la versión..."
app_store_request \
  GET \
  "/appStoreVersions/$version_id/appStoreVersionLocalizations?fields%5BappStoreVersionLocalizations%5D=locale,whatsNew&limit=50" \
  "$localizations_response_path"

localization_count="$(jq '.data | length' "$localizations_response_path")"
if [ "$localization_count" -eq 0 ]; then
  echo "ERROR: La versión no heredó ninguna localización de App Store."
  echo "Completa sus metadatos en App Store Connect y vuelve a ejecutar la Action."
  exit 1
fi

localization_id="$(jq -r --arg locale "$primary_locale" '
  [.data[] | select(.attributes.locale == $locale)][0].id //
  (if (.data | length) == 1 then .data[0].id else empty end)
' "$localizations_response_path")"

if [ -z "$localization_id" ]; then
  echo "ERROR: No se encontró la localización principal '$primary_locale' entre las $localization_count localizaciones."
  exit 1
fi

jq -n \
  --arg localization_id "$localization_id" \
  --arg release_notes "$RELEASE_NOTES" \
  '{
    data: {
      type: "appStoreVersionLocalizations",
      id: $localization_id,
      attributes: {whatsNew: $release_notes}
    }
  }' > "$localization_update_request_path"

app_store_request \
  PATCH \
  "/appStoreVersionLocalizations/$localization_id" \
  "$localization_update_response_path" \
  "$localization_update_request_path"

app_store_request \
  GET \
  "/appStoreVersions/$version_id/appStoreVersionLocalizations?fields%5BappStoreVersionLocalizations%5D=locale,whatsNew&limit=50" \
  "$localizations_response_path"

missing_locales="$(jq -r '[.data[] | select((.attributes.whatsNew // "") | length == 0) | .attributes.locale] | join(", ")' "$localizations_response_path")"
if [ -n "$missing_locales" ]; then
  echo "ERROR: Faltan notas de versión para estas localizaciones: $missing_locales."
  echo "Complétalas en App Store Connect y vuelve a ejecutar la Action."
  exit 1
fi

echo "==> Asociando el build con la versión de App Store..."
jq -n \
  --arg build_id "$build_id" \
  '{data: {type: "builds", id: $build_id}}' \
  > "$build_link_request_path"

app_store_request \
  PATCH \
  "/appStoreVersions/$version_id/relationships/build" \
  "$build_link_response_path" \
  "$build_link_request_path"

echo "==> Revisando borradores de App Review..."
app_store_request \
  GET \
  "/apps/$app_id/reviewSubmissions?filter%5Bplatform%5D=IOS&filter%5Bstate%5D=READY_FOR_REVIEW&limit=3" \
  "$submissions_response_path"

submission_count="$(jq '.data | length' "$submissions_response_path")"
if [ "$submission_count" -gt 1 ]; then
  echo "ERROR: Hay más de un borrador de App Review para iOS. Resuélvelos en App Store Connect."
  exit 1
fi

item_already_added=false
if [ "$submission_count" -eq 1 ]; then
  submission_id="$(jq -r '.data[0].id' "$submissions_response_path")"
  app_store_request \
    GET \
    "/reviewSubmissions/$submission_id/items?include=appStoreVersion&limit=50" \
    "$submission_items_response_path"

  item_count="$(jq '.data | length' "$submission_items_response_path")"
  matching_version_count="$(jq --arg version_id "$version_id" '[
    .data[]?.relationships.appStoreVersion.data.id?,
    .included[]? | select(.type == "appStoreVersions") | .id
  ] | map(select(. == $version_id)) | length' "$submission_items_response_path")"

  if [ "$item_count" -gt 0 ] && { [ "$item_count" -ne 1 ] || [ "$matching_version_count" -eq 0 ]; }; then
    echo "ERROR: El borrador de App Review contiene elementos diferentes a esta versión."
    echo "No se modificó para evitar enviar contenido ajeno por accidente."
    exit 1
  fi

  if [ "$matching_version_count" -gt 0 ]; then
    item_already_added=true
  fi
else
  echo "==> Creando un borrador de App Review..."
  jq -n \
    --arg app_id "$app_id" \
    '{
      data: {
        type: "reviewSubmissions",
        attributes: {platform: "IOS"},
        relationships: {app: {data: {type: "apps", id: $app_id}}}
      }
    }' > "$submission_create_request_path"

  app_store_request \
    POST \
    "/reviewSubmissions" \
    "$submission_create_response_path" \
    "$submission_create_request_path"

  submission_id="$(jq -r '.data.id // empty' "$submission_create_response_path")"
  if [ -z "$submission_id" ]; then
    echo "ERROR: Apple no devolvió el identificador del borrador de revisión."
    exit 1
  fi
fi

if [ "$item_already_added" = "false" ]; then
  echo "==> Agregando la versión al borrador de App Review..."
  jq -n \
    --arg submission_id "$submission_id" \
    --arg version_id "$version_id" \
    '{
      data: {
        type: "reviewSubmissionItems",
        relationships: {
          reviewSubmission: {data: {type: "reviewSubmissions", id: $submission_id}},
          appStoreVersion: {data: {type: "appStoreVersions", id: $version_id}}
        }
      }
    }' > "$submission_item_request_path"

  app_store_request \
    POST \
    "/reviewSubmissionItems" \
    "$submission_item_response_path" \
    "$submission_item_request_path"
fi

echo "==> Enviando el borrador a App Review..."
jq -n \
  --arg submission_id "$submission_id" \
  '{data: {type: "reviewSubmissions", id: $submission_id, attributes: {submitted: true}}}' \
  > "$submission_update_request_path"

app_store_request \
  PATCH \
  "/reviewSubmissions/$submission_id" \
  "$submission_update_response_path" \
  "$submission_update_request_path"

final_state="$(jq -r '.data.attributes.state // "ENVIADO"' "$submission_update_response_path")"
echo ""
echo "✓ iOS $VERSION_NAME ($BUILD_NUMBER) enviado a App Review."
echo "  Estado: $final_state"
echo "  Liberación: manual después de la aprobación"
echo "  Esta Action no publicó la versión."

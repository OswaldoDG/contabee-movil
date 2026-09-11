#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
IPA_PATH="${IOS_IPA_PATH:-$REPOSITORY_ROOT/ContaBeeMovil/bin/Release/net10.0-ios/ios-arm64/publish/ContaBeeMovil.ipa}"
KEY_ID="${APP_STORE_CONNECT_KEY_ID:-}"
ISSUER_ID="${APP_STORE_CONNECT_ISSUER_ID:-}"
PRIVATE_KEY_BASE64="${APP_STORE_CONNECT_PRIVATE_KEY_BASE64:-}"
PRIVATE_KEYS_DIRECTORY="$HOME/private_keys"
PRIVATE_KEY_PATH=""
PRIVATE_KEY_CREATED=false
PRIVATE_KEYS_DIRECTORY_CREATED=false
WORK_DIR="$(mktemp -d /tmp/contabee-app-store.XXXXXX)"

cleanup() {
  if [ "$PRIVATE_KEY_CREATED" = true ]; then
    rm -f "$PRIVATE_KEY_PATH"
  fi
  if [ "$PRIVATE_KEYS_DIRECTORY_CREATED" = true ]; then
    rmdir "$PRIVATE_KEYS_DIRECTORY" 2>/dev/null || true
  fi
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

for variable_name in \
  APP_STORE_CONNECT_KEY_ID \
  APP_STORE_CONNECT_ISSUER_ID \
  APP_STORE_CONNECT_PRIVATE_KEY_BASE64; do
  if [ -z "${!variable_name:-}" ]; then
    echo "ERROR: Falta el secret $variable_name."
    exit 1
  fi
done

if [[ ! "$KEY_ID" =~ ^[A-Za-z0-9]+$ ]]; then
  echo "ERROR: APP_STORE_CONNECT_KEY_ID tiene un formato inválido."
  exit 1
fi

if [[ ! "$ISSUER_ID" =~ ^[A-Fa-f0-9-]+$ ]]; then
  echo "ERROR: APP_STORE_CONNECT_ISSUER_ID tiene un formato inválido."
  exit 1
fi

if [ ! -f "$IPA_PATH" ]; then
  echo "ERROR: No se encontró el IPA: $IPA_PATH"
  exit 1
fi

PRIVATE_KEY_PATH="$PRIVATE_KEYS_DIRECTORY/AuthKey_$KEY_ID.p8"
if [ ! -d "$PRIVATE_KEYS_DIRECTORY" ]; then
  mkdir -p "$PRIVATE_KEYS_DIRECTORY"
  PRIVATE_KEYS_DIRECTORY_CREATED=true
fi
if [ -e "$PRIVATE_KEY_PATH" ]; then
  echo "ERROR: Ya existe una clave con el mismo Key ID en $PRIVATE_KEY_PATH."
  exit 1
fi

PRIVATE_KEY_CREATED=true
if ! printf '%s' "$PRIVATE_KEY_BASE64" \
  | base64 --decode -o "$PRIVATE_KEY_PATH"; then
  echo "ERROR: APP_STORE_CONNECT_PRIVATE_KEY_BASE64 no contiene Base64 válido."
  exit 1
fi
chmod 600 "$PRIVATE_KEY_PATH"

if ! openssl pkey -in "$PRIVATE_KEY_PATH" -noout >/dev/null 2>&1; then
  echo "ERROR: La clave privada reconstruida no es un archivo P8 válido."
  exit 1
fi

run_altool() {
  local operation="$1"
  local success_pattern="$2"
  local log_path="$WORK_DIR/$operation.log"
  shift 2

  set +e
  xcrun altool "$@" 2>&1 | tee "$log_path"
  local altool_status="${PIPESTATUS[0]}"
  set -e

  if [ "$altool_status" -ne 0 ] \
    || grep -Eiq '(VERIFY|VALIDATION|UPLOAD)[[:space:]]+FAILED|Failed to (validate|upload) package|(^|[[:space:]])ERROR:' "$log_path"; then
    echo "ERROR: altool reportó un fallo durante $operation."
    return 1
  fi

  if ! grep -Eiq "$success_pattern" "$log_path"; then
    echo "ERROR: altool no confirmó que $operation terminara correctamente."
    return 1
  fi
}

echo "==> Validando el IPA con App Store Connect..."
if ! run_altool \
  validation \
  'No errors validating|VALIDATION SUCCEEDED|VERIFY SUCCEEDED' \
  --validate-app \
  --file "$IPA_PATH" \
  --type ios \
  --apiKey "$KEY_ID" \
  --apiIssuer "$ISSUER_ID"; then
  exit 1
fi

echo "==> Subiendo el IPA a App Store Connect..."
if ! run_altool \
  upload \
  'UPLOAD SUCCEEDED|successfully uploaded' \
  --upload-app \
  --file "$IPA_PATH" \
  --type ios \
  --apiKey "$KEY_ID" \
  --apiIssuer "$ISSUER_ID"; then
  exit 1
fi

echo ""
echo "✓ IPA enviado a App Store Connect."
echo "  Estado siguiente: procesamiento de Apple"
echo "  No se envió a revisión ni se publicó."

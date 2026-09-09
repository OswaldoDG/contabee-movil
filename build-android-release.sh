#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PARENT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$SCRIPT_DIR/ContaBeeMovil"
KEYSTORE_PATH="${ANDROID_KEYSTORE_PATH:-$PARENT_DIR/contabee-release.keystore}"
KEY_ALIAS="${ANDROID_SIGNING_KEY_ALIAS:-contabee}"
PUBLISH_DIR="$PROJECT_DIR/bin/Release/net10.0-android/publish"
WORK_DIR="$(mktemp -d /tmp/contabee-android-release.XXXXXX)"
STORE_PASSWORD_FILE="$WORK_DIR/store-password.txt"
KEY_PASSWORD_FILE="$WORK_DIR/key-password.txt"
CERTIFICATE_PATH="$WORK_DIR/upload-certificate.der"

cleanup() {
  rm -rf "$WORK_DIR"
  unset ANDROID_SIGNING_PASSWORD ANDROID_SIGNING_STORE_PASSWORD ANDROID_SIGNING_KEY_PASSWORD
}
trap cleanup EXIT

if [ ! -f "$KEYSTORE_PATH" ]; then
  echo "ERROR: No se encontró el keystore: $KEYSTORE_PATH"
  echo "Define ANDROID_KEYSTORE_PATH con su ubicación."
  exit 1
fi

STORE_PASSWORD="${ANDROID_SIGNING_STORE_PASSWORD:-${ANDROID_SIGNING_PASSWORD:-}}"
KEY_PASSWORD="${ANDROID_SIGNING_KEY_PASSWORD:-${ANDROID_SIGNING_PASSWORD:-}}"

if [ -z "$STORE_PASSWORD" ] || [ -z "$KEY_PASSWORD" ]; then
  if [ ! -t 0 ]; then
    echo "ERROR: Define ANDROID_SIGNING_PASSWORD o las variables separadas de store/key."
    exit 1
  fi

  read -r -s -p "Contraseña del keystore Android: " STORE_PASSWORD
  echo ""
  KEY_PASSWORD="$STORE_PASSWORD"
fi

printf '%s' "$STORE_PASSWORD" > "$STORE_PASSWORD_FILE"
printf '%s' "$KEY_PASSWORD" > "$KEY_PASSWORD_FILE"
chmod 600 "$STORE_PASSWORD_FILE" "$KEY_PASSWORD_FILE"
unset STORE_PASSWORD KEY_PASSWORD

echo "==> Verificando keystore y alias..."
keytool -list \
  -keystore "$KEYSTORE_PATH" \
  -alias "$KEY_ALIAS" \
  -storepass:file "$STORE_PASSWORD_FILE" > /dev/null

keytool -exportcert \
  -keystore "$KEYSTORE_PATH" \
  -alias "$KEY_ALIAS" \
  -storepass:file "$STORE_PASSWORD_FILE" \
  -file "$CERTIFICATE_PATH" > /dev/null

echo "    Certificado de la clave de carga:"
openssl x509 -inform DER -in "$CERTIFICATE_PATH" -noout -fingerprint -sha256 -enddate

echo "==> Limpiando artefactos Android anteriores..."
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"

echo "==> Restaurando paquetes NuGet..."
dotnet restore "$PROJECT_DIR" --locked-mode

echo "==> Generando Android App Bundle de Release..."
dotnet publish "$PROJECT_DIR" \
  -f net10.0-android \
  -c Release \
  -p:RunAOTCompilation=false \
  -p:AndroidPackageFormats=aab \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$KEYSTORE_PATH" \
  -p:AndroidSigningKeyAlias="$KEY_ALIAS" \
  -p:AndroidSigningKeyPass="file:$KEY_PASSWORD_FILE" \
  -p:AndroidSigningStorePass="file:$STORE_PASSWORD_FILE"

AAB_PATH=$(find "$PUBLISH_DIR" -maxdepth 1 -type f -name '*-Signed.aab' -print -quit)
if [ -z "$AAB_PATH" ]; then
  AAB_PATH=$(find "$PUBLISH_DIR" -maxdepth 1 -type f -name '*.aab' -print -quit)
fi

if [ -z "$AAB_PATH" ]; then
  echo "ERROR: No se encontró el AAB generado en $PUBLISH_DIR"
  exit 1
fi

echo "==> Verificando firma del AAB..."
jarsigner -verify "$AAB_PATH"

echo ""
echo "✓ AAB firmado y validado:"
echo "  $AAB_PATH"

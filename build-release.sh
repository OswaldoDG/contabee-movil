#!/bin/bash

set -euo pipefail

CODESIGN_KEY="${IOS_CODESIGN_KEY:-Apple Distribution: Neurofant Mexico  S.A.P.I. de C.V (X598HW3AYR)}"
CODESIGN_PROVISION="${IOS_CODESIGN_PROVISION:-ContaBee_AppStore}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/ContaBeeMovil"
EXTENSION_PROJECT_DIR="$SCRIPT_DIR/ContaBeeShareExtension"
IPA_PATH="$PROJECT_DIR/bin/Release/net10.0-ios/ios-arm64/publish/ContaBeeMovil.ipa"
WORK_DIR="$(mktemp -d /tmp/contabee-release.XXXXXX)"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

echo "==> Verificando certificado en keychain..."
if ! security find-identity -v -p codesigning | grep -Fq "$CODESIGN_KEY"; then
    echo "ERROR: Certificado con clave privada no encontrado o no válido: $CODESIGN_KEY"
    echo "Ejecuta: security find-identity -v -p codesigning"
    exit 1
fi

echo "==> Limpiando artefactos anteriores..."
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj" \
       "$EXTENSION_PROJECT_DIR/bin" "$EXTENSION_PROJECT_DIR/obj"

echo "==> Restaurando paquetes NuGet..."
dotnet restore "$PROJECT_DIR" \
  -r ios-arm64 \
  --locked-mode \
  -p:ContaBeeIosOnly=true

echo "==> Generando IPA de Release..."
dotnet publish "$PROJECT_DIR" \
  -f net10.0-ios \
  -r ios-arm64 \
  -c Release \
  --no-restore \
  -p:ContaBeeIosOnly=true \
  -p:ArchiveOnBuild=true \
  -p:CodesignKey="$CODESIGN_KEY" \
  -p:CodesignProvision="$CODESIGN_PROVISION"

echo "==> Verificando contenido y firmas del IPA..."
unzip -q "$IPA_PATH" -d "$WORK_DIR"

APP="$WORK_DIR/Payload/ContaBeeMovil.app"
EXTENSION="$APP/PlugIns/ContaBeeShareExtension.appex"

if [ ! -d "$EXTENSION" ]; then
    echo "ERROR: El IPA no contiene ContaBeeShareExtension.appex"
    exit 1
fi

codesign --verify --deep --strict --verbose=2 "$APP"
codesign --verify --deep --strict --verbose=2 "$EXTENSION"

APP_VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$APP/Info.plist")
APP_BUILD=$(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" "$APP/Info.plist")
EXTENSION_VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$EXTENSION/Info.plist")
EXTENSION_BUILD=$(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" "$EXTENSION/Info.plist")

if [ "$APP_VERSION" != "$EXTENSION_VERSION" ] || [ "$APP_BUILD" != "$EXTENSION_BUILD" ]; then
    echo "ERROR: La versión de la app ($APP_VERSION/$APP_BUILD) no coincide con la extensión ($EXTENSION_VERSION/$EXTENSION_BUILD)."
    exit 1
fi

echo ""
echo "✓ IPA firmado y validado: $APP_VERSION ($APP_BUILD)"
echo "  $IPA_PATH"

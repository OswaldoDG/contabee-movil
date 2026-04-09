#!/bin/bash

set -e

CODESIGN_KEY="Apple Distribution: Neurofant Mexico  S.A.P.I. de C.V (X598HW3AYR)"
CODESIGN_PROVISION="ContaBee_AppStore"
PROJECT_DIR="$(dirname "$0")/ContaBeeMovil"
IPA_PATH="$PROJECT_DIR/bin/Release/net10.0-ios/ios-arm64/publish/ContaBeeMovil.ipa"
OUTPUT_IPA="$PROJECT_DIR/bin/Release/net10.0-ios/ios-arm64/publish/ContaBeeMovil_signed.ipa"
PROVISION_FILE="$HOME/Library/MobileDevice/Provisioning Profiles/ContaBee_AppStore.mobileprovision"
WORK_DIR="/tmp/ContaBeeReSign"

echo "==> Limpiando artefactos anteriores..."
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"

echo "==> Restaurando paquetes NuGet..."
dotnet restore "$PROJECT_DIR"

echo "==> Generando IPA de Release..."
dotnet publish "$PROJECT_DIR" \
  -f net10.0-ios \
  -r ios-arm64 \
  -c Release \
  -p:ArchiveOnBuild=true \
  -p:CodesignKey="$CODESIGN_KEY" \
  -p:CodesignProvision="$CODESIGN_PROVISION"

echo ""
echo "==> Verificando certificado en keychain..."
if ! security find-identity -v -p codesigning | grep -q "$(echo "$CODESIGN_KEY" | sed 's/.*(\(.*\))/\1/')"; then
    echo "ERROR: Certificado no encontrado en keychain: $CODESIGN_KEY"
    echo "Ejecuta: security find-identity -v -p codesigning"
    exit 1
fi

echo "==> Limpiando directorio temporal..."
rm -rf "$WORK_DIR"

echo "==> Extrayendo IPA..."
unzip -q "$IPA_PATH" -d "$WORK_DIR"

APP="$WORK_DIR/Payload/ContaBeeMovil.app"

echo "==> Extrayendo entitlements del perfil de aprovisionamiento..."
security cms -D -i "$PROVISION_FILE" > /tmp/provision.plist
/usr/libexec/PlistBuddy -x -c "Print :Entitlements" /tmp/provision.plist > /tmp/entitlements.plist

echo "==> Embediendo perfil de aprovisionamiento..."
cp "$PROVISION_FILE" "$APP/embedded.mobileprovision"

echo "==> Firmando dylibs sueltos..."
find "$APP" -name "*.dylib" | while read -r item; do
    echo "    dylib: $(basename "$item")"
    codesign --force --sign "$CODESIGN_KEY" --timestamp "$item"
done

echo "==> Firmando frameworks (binario interno primero, luego bundle)..."
find "$APP/Frameworks" -name "*.framework" | while read -r fw; do
    name=$(basename "$fw" .framework)
    binary="$fw/$name"
    if [ -f "$binary" ]; then
        echo "    binario: $name"
        codesign --force --sign "$CODESIGN_KEY" --timestamp "$binary"
    fi
    echo "    framework: $name.framework"
    codesign --force --sign "$CODESIGN_KEY" --timestamp "$fw"
done

echo "==> Firmando la app principal..."
codesign --force --sign "$CODESIGN_KEY" --timestamp --entitlements /tmp/entitlements.plist "$APP"

echo "==> Verificando firma..."
codesign --verify --deep --strict "$APP" && echo "    Firma válida." || { echo "ERROR: Firma inválida"; exit 1; }

echo "==> Reempaquetando IPA..."
cd "$WORK_DIR"
zip -qr "$OLDPWD/$OUTPUT_IPA" Payload
cd "$OLDPWD"

echo ""
echo "✓ Listo. Sube el IPA firmado a Transporter:"
echo "  $OUTPUT_IPA"

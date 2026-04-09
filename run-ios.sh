#!/bin/bash

set -e

DEVICE_ID="32C711C8-A4F8-5D93-A343-BABA962AFC2F"
BUNDLE_ID="mx.contabee.app"
CODESIGN_KEY="Apple Development: Created via API (489HK3K7TX)"
CODESIGN_PROVISION="VS: mx.contabee.app Development"
PROJECT_DIR="$(dirname "$0")/ContaBeeMovil"
APP_PATH="$PROJECT_DIR/bin/Debug/net10.0-ios/ios-arm64/ContaBeeMovil.app"

echo "🔨 Building..."
dotnet build "$PROJECT_DIR" \
  -f net10.0-ios \
  -r ios-arm64 \
  -p:CodesignKey="$CODESIGN_KEY" \
  -p:CodesignProvision="$CODESIGN_PROVISION"

echo "📲 Installing on iPhone..."
xcrun devicectl device install app \
  --device "$DEVICE_ID" \
  "$APP_PATH"

echo "🚀 Launching..."
xcrun devicectl device process launch \
  --device "$DEVICE_ID" \
  "$BUNDLE_ID"

echo "✅ Done"

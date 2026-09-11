#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ACTION="${1:-}"
PLATFORM="${2:-}"

usage() {
  cat <<'EOF'
Uso: ./scripts/release/mobile-release.sh <acción> <plataforma>

Acciones:
  build             Genera y valida el paquete firmado.
  upload            Carga un paquete ya generado, sin enviarlo a revisión.
  validate-review   Valida un build ya cargado, sin enviarlo a revisión.
  submit-review     Envía un build ya cargado a revisión.

Plataformas:
  android
  ios

La Action de GitHub coordina ambas plataformas y las barreras entre etapas.
EOF
}

if [ "$PLATFORM" != "android" ] && [ "$PLATFORM" != "ios" ]; then
  usage
  exit 2
fi

case "$ACTION:$PLATFORM" in
  build:android)
    exec "$SCRIPT_DIR/build-android-release.sh"
    ;;
  build:ios)
    exec "$SCRIPT_DIR/build-release.sh"
    ;;
  upload:android)
    exec "$SCRIPT_DIR/upload-google-play.sh"
    ;;
  upload:ios)
    exec "$SCRIPT_DIR/upload-app-store-connect.sh"
    ;;
  validate-review:android)
    export STORE_SUBMIT_REVIEW=false
    exec "$SCRIPT_DIR/submit-google-play-review.sh"
    ;;
  validate-review:ios)
    export STORE_SUBMIT_REVIEW=false
    exec "$SCRIPT_DIR/submit-app-store-review.sh"
    ;;
  submit-review:android)
    export STORE_SUBMIT_REVIEW=true
    exec "$SCRIPT_DIR/submit-google-play-review.sh"
    ;;
  submit-review:ios)
    export STORE_SUBMIT_REVIEW=true
    exec "$SCRIPT_DIR/submit-app-store-review.sh"
    ;;
  *)
    usage
    exit 2
    ;;
esac

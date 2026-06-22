#!/usr/bin/env bash
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity}"
PROJECT="${PROJECT:-$(cd "$(dirname "$0")/.." && pwd)}"
LOG_DIR="${LOG_DIR:-/tmp/sczip-build}"
mkdir -p "$LOG_DIR"

force_close_unity() {
  local pid
  while read -r pid; do
    [ -n "$pid" ] && kill -KILL "$pid" 2>/dev/null || true
  done < <(pgrep -f "${UNITY}.*${PROJECT}" 2>/dev/null || true)
  sleep 1
}

build_target() {
  local method="$1"
  local log="$2"
  echo "=== $method ==="
  force_close_unity
  "$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod "$method" \
    -logFile "$log"
}

TARGET="${1:-all}"
case "$TARGET" in
  android)
    build_target SCZip.Editor.BuildPlayer.BuildAndroid "$LOG_DIR/android.log"
    ;;
  ios)
    build_target SCZip.Editor.BuildPlayer.BuildIOS "$LOG_DIR/ios.log"
    ;;
  all)
    build_target SCZip.Editor.BuildPlayer.BuildAndroid "$LOG_DIR/android.log"
    build_target SCZip.Editor.BuildPlayer.BuildIOS "$LOG_DIR/ios.log"
    ;;
  *)
    echo "Usage: $0 [android|ios|all]"
    exit 1
    ;;
esac

echo "Build outputs:"
ls -la "$PROJECT/Builds/Android/" 2>/dev/null || true
ls -la "$PROJECT/Builds/iOS/" 2>/dev/null | head -10 || true
echo "Logs: $LOG_DIR"

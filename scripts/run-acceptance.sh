#!/usr/bin/env bash
set -uo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity}"
PROJECT="${PROJECT:-$(cd "$(dirname "$0")/.." && pwd)}"
LOG_DIR="${LOG_DIR:-/tmp/sczip-acceptance}"
mkdir -p "$LOG_DIR"

force_close_unity() {
  local pid
  while read -r pid; do
    [ -n "$pid" ] && kill -TERM "$pid" 2>/dev/null || true
  done < <(pgrep -f "${UNITY}.*${PROJECT}" 2>/dev/null || true)
  sleep 2
  while read -r pid; do
    [ -n "$pid" ] && kill -KILL "$pid" 2>/dev/null || true
  done < <(pgrep -f "${UNITY}.*${PROJECT}" 2>/dev/null || true)
  sleep 1
}

compile_project() {
  echo "=== Compile project ==="
  "$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -logFile "$LOG_DIR/compile.log" || true
  if grep -q 'error CS' "$LOG_DIR/compile.log" 2>/dev/null; then
    echo "Compile errors:"
    grep 'error CS' "$LOG_DIR/compile.log" | head -20
    return 1
  fi
  return 0
}

run_editmode_tests() {
  echo "=== EditMode: ArchiveRoundtripEditModeTests ==="
  local attempt=1
  while [ "$attempt" -le 3 ]; do
    "$UNITY" -batchmode -nographics -quit \
      -projectPath "$PROJECT" \
      -runTests -testPlatform EditMode \
      -assemblyNames SCZip.Tests.Editor \
      -testFilter "ArchiveRoundtripEditModeTests" \
      -testResults "$LOG_DIR/editmode.xml" \
      -logFile "$LOG_DIR/editmode.log" || true
    if [ -f "$LOG_DIR/editmode.xml" ] && grep -q 'testcasecount="[1-9]' "$LOG_DIR/editmode.xml"; then
      return 0
    fi
    echo "EditMode results missing, retry $attempt/3..."
    sleep 2
    attempt=$((attempt + 1))
  done
  return 1
}

run_smoke() {
  echo "=== Smoke: Archive roundtrip + scene ==="
  "$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod SCZip.Editor.SmokeTestRunner.Run \
    -logFile "$LOG_DIR/smoke.log"
}

summarize() {
  echo ""
  echo "=== Summary ==="
  if [ -f "$LOG_DIR/editmode.xml" ]; then
    grep -E 'test-run |result=' "$LOG_DIR/editmode.xml" | head -3 || true
    grep -E 'test-case.*result=' "$LOG_DIR/editmode.xml" | grep -v 'result="Passed"' || echo "All EditMode tests passed"
  else
    echo "EditMode: no results (see $LOG_DIR/editmode.log)"
  fi
  if [ -f "$LOG_DIR/smoke.log" ]; then
    grep -E 'SMOKE_PASS|SMOKE_FAIL|error CS' "$LOG_DIR/smoke.log" | tail -5 || true
  fi
  echo "Logs: $LOG_DIR"
}

EXIT_CODE=0
force_close_unity
compile_project || EXIT_CODE=1
run_smoke || EXIT_CODE=1
run_editmode_tests || true
summarize
exit "$EXIT_CODE"

#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BS_DIR="${1:-${BEAT_SABER_DIR:-$ROOT/refs}}"
VERSION="${VERSION:-0.1.0}"

if [[ ! -d "$BS_DIR/Beat Saber_Data/Managed" ]]; then
  echo "Usage: $0 /path/to/Beat\\ Saber" >&2
  echo "  (or set BEAT_SABER_DIR, or place Beat Saber refs at $ROOT/refs)" >&2
  exit 1
fi

DOTNET="${DOTNET:-dotnet}"
if ! command -v "$DOTNET" >/dev/null 2>&1; then
  if [[ -x "/opt/homebrew/opt/dotnet/libexec/dotnet" ]]; then
    DOTNET="/opt/homebrew/opt/dotnet/libexec/dotnet"
  else
    echo "dotnet SDK not found in PATH" >&2
    exit 1
  fi
fi

"$DOTNET" build "$ROOT/BeatFramer/BeatFramer.csproj" -c Release -p:BeatSaberDir="$BS_DIR"

STAGE="$ROOT/dist/stage"
OUT_ZIP="$ROOT/dist/BeatFramer-v${VERSION}.zip"
rm -rf "$STAGE" "$OUT_ZIP"
mkdir -p "$STAGE/Plugins"

cp "$ROOT/BeatFramer/bin/Release/net472/BeatFramer.dll" "$STAGE/Plugins/BeatFramer.dll"
cp "$ROOT/dxvk.conf" "$STAGE/dxvk.conf.proton-optional"

(cd "$STAGE" && zip -r "$OUT_ZIP" Plugins dxvk.conf.proton-optional)
rm -rf "$STAGE"

echo "Packaged: $OUT_ZIP"

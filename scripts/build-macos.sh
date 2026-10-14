#!/usr/bin/env bash
#
# Checkmate - build a ready-to-run macOS app (Checkmate.app).
#
#   ./build-macos.sh                  build for this Mac (Apple Silicon or Intel)
#   ./build-macos.sh --install        ...and copy Checkmate.app to ~/Applications
#   ./build-macos.sh --run            ...and open it when it is done
#   ./build-macos.sh --rid osx-x64    force a target (osx-arm64 or osx-x64)
#
# Run it from the repo's scripts/ folder to build your working copy, or run it on
# its own anywhere: it then downloads the code from GitHub first.
# Needs: the .NET 8+ SDK (and git, when it has to download the code).
# Because you build the app yourself, macOS does not show the "unidentified
# developer" warning for it.
set -euo pipefail

REPO_URL="${CHECKMATE_REPO:-https://github.com/Checkmate-Studios/Checkmate.git}"
BRANCH="${CHECKMATE_BRANCH:-main}"
VERSION="${CHECKMATE_VERSION:-0.1.0}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -t 1 ]; then
  C_STEP=$'\033[36m'; C_OK=$'\033[32m'; C_WARN=$'\033[33m'; C_ERR=$'\033[31m'; C_OFF=$'\033[0m'
else
  C_STEP=""; C_OK=""; C_WARN=""; C_ERR=""; C_OFF=""
fi
step() { printf '\n%s==> %s%s\n' "$C_STEP" "$1" "$C_OFF"; }
ok()   { printf '    %s%s%s\n' "$C_OK" "$1" "$C_OFF"; }
warn() { printf '    %s%s%s\n' "$C_WARN" "$1" "$C_OFF"; }
fail() { printf '\n%sERROR: %s%s\n' "$C_ERR" "$1" "$C_OFF" >&2; exit 1; }

usage() {
  cat <<'USAGE'
Usage: build-macos.sh [--install] [--run] [--rid osx-arm64|osx-x64]
  --install   copy Checkmate.app to ~/Applications
  --run       open Checkmate when the build is finished
  --rid       choose the target instead of detecting this Mac
USAGE
}

INSTALL=0; RUN=0; RID=""
while [ $# -gt 0 ]; do
  case "$1" in
    --install) INSTALL=1 ;;
    --run)     RUN=1 ;;
    --rid)     RID="${2:-}"; [ -n "$RID" ] || fail "--rid needs a value"; shift ;;
    -h|--help) usage; exit 0 ;;
    *)         usage; fail "Unknown option: $1" ;;
  esac
  shift
done

printf '\n  CHECKMATE - macOS build\n'

if [ -z "$RID" ]; then
  case "$(uname -m)" in
    arm64)  RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) fail "Unsupported CPU '$(uname -m)'. Try: $0 --rid osx-arm64" ;;
  esac
fi

install_hint() {
  echo "    Install the .NET 8 SDK (and git), then run this script again:"
  echo "      brew install --cask dotnet-sdk      (Homebrew: https://brew.sh)"
  echo "      xcode-select --install              (gives you git)"
  echo "      or download from https://dotnet.microsoft.com/download"
}

dotnet_major() {
  command -v dotnet >/dev/null 2>&1 || { echo 0; return; }
  dotnet --list-sdks 2>/dev/null | cut -d. -f1 | grep -E '^[0-9]+$' | sort -n | tail -1 || true
}

step "Checking tools"
major="$(dotnet_major)"; major="${major:-0}"
if [ "$major" -lt 8 ]; then
  install_hint
  fail ".NET SDK 8 or newer was not found."
fi
ok ".NET SDK $major found"

PARENT="$(dirname "$HERE")"
if [ -f "$PARENT/Checkmate.sln" ]; then
  ROOT="$PARENT"
  ok "Building your working copy: $ROOT"
else
  command -v git >/dev/null 2>&1 || { install_hint; fail "git is needed to download Checkmate."; }
  ROOT="$HERE/Checkmate-source"
  if [ -d "$ROOT/.git" ]; then
    step "Updating Checkmate from GitHub"
    git -C "$ROOT" pull --ff-only
  else
    step "Downloading Checkmate from GitHub"
    git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$ROOT"
  fi
fi

# ---- build the program ----
BIN="$ROOT/dist/$RID-bin"
step "Building Checkmate ($RID). The first build downloads packages, so it can take a few minutes"
rm -rf "$BIN"
dotnet publish "$ROOT/src/Checkmate.Desktop/Checkmate.Desktop.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false \
  -o "$BIN" || fail "The build failed. Scroll up for the first 'error' line."
[ -f "$BIN/Checkmate" ] || fail "The build finished but $BIN/Checkmate was not created."

# ---- wrap it into Checkmate.app ----
step "Creating Checkmate.app"
APP="$ROOT/dist/$RID/Checkmate.app"
rm -rf "$ROOT/dist/$RID"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$BIN/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/Checkmate"
rm -rf "$BIN"

ICON_KEY=""
LOGO="$ROOT/assets/logo.png"
if [ -f "$LOGO" ] && command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
  ICONSET="$(mktemp -d)/Checkmate.iconset"
  mkdir -p "$ICONSET"
  for s in 16 32 128 256 512; do
    sips -z "$s" "$s" "$LOGO" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
    sips -z "$((s * 2))" "$((s * 2))" "$LOGO" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
  done
  if iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Checkmate.icns" 2>/dev/null; then
    ICON_KEY="  <key>CFBundleIconFile</key><string>Checkmate</string>"
    ok "App icon created from assets/logo.png"
  fi
else
  warn "No app icon (needs assets/logo.png plus the macOS tools sips and iconutil)."
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Checkmate</string>
  <key>CFBundleDisplayName</key><string>Checkmate</string>
  <key>CFBundleIdentifier</key><string>io.github.checkmate-studios.checkmate</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>Checkmate</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>10.15</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.graphics-design</string>
  <key>NSMicrophoneUsageDescription</key><string>Checkmate records your voice when you use Record voice.</string>
$ICON_KEY
</dict>
</plist>
PLIST

# Apple Silicon refuses to start unsigned programs. An "ad-hoc" signature (free, no account) is enough.
if command -v codesign >/dev/null 2>&1; then
  xattr -cr "$APP" 2>/dev/null || true
  if codesign --force --deep --sign - "$APP" >/dev/null 2>&1; then ok "Signed (ad-hoc) so it starts on Apple Silicon"
  else warn "codesign failed. If the app will not open, run: codesign --force --deep --sign - \"$APP\""; fi
else
  warn "codesign not found (not on a Mac?). On a Mac, run: codesign --force --deep --sign - Checkmate.app"
fi

ok "Built: $APP"
TARGET="$APP"
if [ "$INSTALL" = 1 ]; then
  step "Copying to ~/Applications"
  mkdir -p "$HOME/Applications"
  rm -rf "$HOME/Applications/Checkmate.app"
  cp -R "$APP" "$HOME/Applications/Checkmate.app"
  TARGET="$HOME/Applications/Checkmate.app"
  ok "Installed: $TARGET"
fi

printf '\n%sDone!%s  Open it with:  open "%s"\n\n' "$C_OK" "$C_OFF" "$TARGET"
if [ "$RUN" = 1 ]; then open "$TARGET" || true; fi

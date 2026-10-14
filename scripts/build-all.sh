#!/usr/bin/env bash
#
# Checkmate - build for Windows, Linux AND macOS in one go (run on Linux or macOS).
#
#   ./build-all.sh                       all of them
#   ./build-all.sh --only linux,macos    just some (windows, linux, macos)
#
# Output goes to dist/release/:
#   Checkmate-windows-x64.zip   Checkmate.exe
#   Checkmate-linux-x64.tar.gz  Checkmate (Linux program)
#   Checkmate-macos-arm64.zip   Checkmate.app (Apple Silicon)
#   Checkmate-macos-x64.zip     Checkmate.app (Intel Macs)
#
# Run it from the repo's scripts/ folder to build your working copy, or on its own to
# download the code from GitHub first. Needs the .NET 8+ SDK (and git when downloading).
# Run on a Mac it also signs the apps and creates their icon; on Linux the Mac zips come
# with a README-FIRST.txt telling the user the one command to run (codesign).
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

ONLY="windows,linux,macos"
while [ $# -gt 0 ]; do
  case "$1" in
    --only) ONLY="${2:-}"; [ -n "$ONLY" ] || fail "--only needs a value"; shift ;;
    -h|--help) echo "Usage: build-all.sh [--only windows,linux,macos]"; exit 0 ;;
    *) fail "Unknown option: $1" ;;
  esac
  shift
done
wants() { case ",$ONLY," in *",$1,"*) return 0 ;; *) return 1 ;; esac; }
for g in ${ONLY//,/ }; do
  case "$g" in windows|linux|macos) ;; *) fail "Unknown system '$g'. Use: windows, linux, macos" ;; esac
done

printf '\n  CHECKMATE - build for Windows + Linux + macOS\n'

install_hint() {
  echo "    Install the .NET 8 SDK (and git), then run this script again:"
  if   command -v apt-get >/dev/null 2>&1; then echo "      sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0 git zip"
  elif command -v dnf     >/dev/null 2>&1; then echo "      sudo dnf install -y dotnet-sdk-8.0 git zip"
  elif command -v pacman  >/dev/null 2>&1; then echo "      sudo pacman -S dotnet-sdk git zip"
  elif command -v brew    >/dev/null 2>&1; then echo "      brew install --cask dotnet-sdk"
  else echo "      https://dotnet.microsoft.com/download"
  fi
}

dotnet_major() {
  command -v dotnet >/dev/null 2>&1 || { echo 0; return; }
  dotnet --list-sdks 2>/dev/null | cut -d. -f1 | grep -E '^[0-9]+$' | sort -n | tail -1 || true
}

step "Checking tools"
major="$(dotnet_major)"; major="${major:-0}"
if [ "$major" -lt 8 ]; then install_hint; fail ".NET SDK 8 or newer was not found."; fi
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

PROJECT="$ROOT/src/Checkmate.Desktop/Checkmate.Desktop.csproj"
DIST="$ROOT/dist"
RELEASE="$DIST/release"
mkdir -p "$RELEASE"

HAVE_ZIP=0; command -v zip >/dev/null 2>&1 && HAVE_ZIP=1
RESULTS=()

publish() {  # publish <rid> <outdir>
  rm -rf "$2"
  dotnet publish "$PROJECT" -c Release -r "$1" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false \
    -o "$2" || fail "The $1 build failed. Scroll up for the first 'error' line."
}

pack_zip() {  # pack_zip <folder-name-under-dist> <archive-base-name>
  if [ "$HAVE_ZIP" = 1 ]; then
    (cd "$DIST" && rm -f "$RELEASE/$2.zip" && zip -qry "$RELEASE/$2.zip" "$1")
    RESULTS+=("$RELEASE/$2.zip")
  else
    (cd "$DIST" && tar -czf "$RELEASE/$2.tar.gz" "$1")
    RESULTS+=("$RELEASE/$2.tar.gz")
  fi
}

# ---------------- Windows ----------------
if wants windows; then
  step "Building windows-x64"
  BIN="$DIST/windows-x64-bin"; PACK="$DIST/windows-x64"
  publish win-x64 "$BIN"
  [ -f "$BIN/Checkmate.exe" ] || fail "Checkmate.exe was not created."
  rm -rf "$PACK"; mkdir -p "$PACK"; cp -R "$BIN/." "$PACK/"; rm -rf "$BIN"
  pack_zip windows-x64 Checkmate-windows-x64
  ok "Packed windows-x64"
fi

# ---------------- Linux ----------------
if wants linux; then
  step "Building linux-x64"
  BIN="$DIST/linux-x64-bin"; PACK="$DIST/linux-x64"
  publish linux-x64 "$BIN"
  [ -f "$BIN/Checkmate" ] || fail "The Linux program was not created."
  rm -rf "$PACK"; mkdir -p "$PACK"; cp -R "$BIN/." "$PACK/"; rm -rf "$BIN"
  chmod +x "$PACK/Checkmate"
  printf 'Run in a terminal in this folder:\n\n    ./Checkmate\n\nIf it will not start, install the common desktop libraries:\n    Debian/Ubuntu: sudo apt install libfontconfig1 libx11-6 libice6 libsm6 libopenal1\n' > "$PACK/README-FIRST.txt"
  (cd "$DIST" && rm -f "$RELEASE/Checkmate-linux-x64.tar.gz" && tar -czf "$RELEASE/Checkmate-linux-x64.tar.gz" linux-x64)   # tar keeps the "executable" flag
  RESULTS+=("$RELEASE/Checkmate-linux-x64.tar.gz")
  ok "Packed linux-x64"
fi

# ---------------- macOS ----------------
make_app() {  # make_app <rid> <name>
  local rid="$1" name="$2"
  local bin="$DIST/$name-bin" pack="$DIST/$name"
  local app="$pack/Checkmate.app"
  step "Building $name"
  publish "$rid" "$bin"
  [ -f "$bin/Checkmate" ] || fail "The $name program was not created."
  rm -rf "$pack"; mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp -R "$bin/." "$app/Contents/MacOS/"; chmod +x "$app/Contents/MacOS/Checkmate"; rm -rf "$bin"

  local icon_key="" logo="$ROOT/assets/logo.png"
  if [ -f "$logo" ] && command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
    local iconset; iconset="$(mktemp -d)/Checkmate.iconset"; mkdir -p "$iconset"
    for s in 16 32 128 256 512; do
      sips -z "$s" "$s" "$logo" --out "$iconset/icon_${s}x${s}.png" >/dev/null
      sips -z "$((s * 2))" "$((s * 2))" "$logo" --out "$iconset/icon_${s}x${s}@2x.png" >/dev/null
    done
    if iconutil -c icns "$iconset" -o "$app/Contents/Resources/Checkmate.icns" 2>/dev/null; then
      icon_key="  <key>CFBundleIconFile</key><string>Checkmate</string>"
    fi
  fi

  cat > "$app/Contents/Info.plist" <<PLIST
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
$icon_key
</dict>
</plist>
PLIST

  if command -v codesign >/dev/null 2>&1; then
    xattr -cr "$app" 2>/dev/null || true
    codesign --force --deep --sign - "$app" >/dev/null 2>&1 && ok "Signed (ad-hoc)" || warn "codesign failed for $name"
  else
    printf 'Open Terminal in this folder and run these two commands once:\n\n    chmod +x Checkmate.app/Contents/MacOS/Checkmate\n    xattr -cr Checkmate.app && codesign --force --deep --sign - Checkmate.app\n\nThen double-click Checkmate.app. (Apple Silicon Macs refuse unsigned programs; the second\ncommand gives the app a free local signature.) If macOS still warns you, right-click the app and choose Open.\n' > "$pack/README-FIRST.txt"
    warn "Not on a Mac: the zip includes a README-FIRST.txt with the signing command."
  fi

  pack_zip "$name" "Checkmate-$name"
  ok "Packed $name"
}

if wants macos; then
  make_app osx-arm64 macos-arm64
  make_app osx-x64   macos-x64
fi

printf '\n%sDone!%s  Your downloads are in:\n  %s\n' "$C_OK" "$C_OFF" "$RELEASE"
for r in "${RESULTS[@]}"; do printf '    %s (%s)\n' "$(basename "$r")" "$(du -h "$r" | cut -f1)"; done
echo

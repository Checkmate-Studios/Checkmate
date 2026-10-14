#!/usr/bin/env bash
#
# Checkmate - build a ready-to-run LINUX program.
#
#   ./build-linux.sh                  build for this computer (x64 or arm64)
#   ./build-linux.sh --install        ...and add Checkmate to your app menu
#   ./build-linux.sh --run            ...and start it when it is done
#   ./build-linux.sh --rid linux-x64  force a target (linux-x64 or linux-arm64)
#
# Run it from the repo's scripts/ folder to build your working copy, or run it on
# its own anywhere: it then downloads the code from GitHub first.
# Needs: the .NET 8+ SDK (and git, when it has to download the code).
set -euo pipefail

REPO_URL="${CHECKMATE_REPO:-https://github.com/Checkmate-Studios/Checkmate.git}"
BRANCH="${CHECKMATE_BRANCH:-main}"
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
Usage: build-linux.sh [--install] [--run] [--rid linux-x64|linux-arm64]
  --install   copy the program to ~/.local and add it to the application menu
  --run       start Checkmate when the build is finished
  --rid       choose the target instead of detecting this computer
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

printf '\n  CHECKMATE - Linux build\n'

# ---- which CPU? ----
if [ -z "$RID" ]; then
  case "$(uname -m)" in
    x86_64|amd64)  RID="linux-x64" ;;
    aarch64|arm64) RID="linux-arm64" ;;
    *) fail "Unsupported CPU '$(uname -m)'. Try: $0 --rid linux-x64" ;;
  esac
fi

# ---- tools ----
install_hint() {
  echo "    Install the .NET 8 SDK (and git), then run this script again:"
  if   command -v apt-get >/dev/null 2>&1; then echo "      sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0 git"
  elif command -v dnf     >/dev/null 2>&1; then echo "      sudo dnf install -y dotnet-sdk-8.0 git"
  elif command -v pacman  >/dev/null 2>&1; then echo "      sudo pacman -S dotnet-sdk git"
  elif command -v zypper  >/dev/null 2>&1; then echo "      sudo zypper install dotnet-sdk-8.0 git"
  else echo "      https://dotnet.microsoft.com/download"
  fi
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

# ---- where is the code? ----
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

# ---- build ----
OUT="$ROOT/dist/$RID"
step "Building Checkmate ($RID). The first build downloads packages, so it can take a few minutes"
rm -rf "$OUT"
dotnet publish "$ROOT/src/Checkmate.Desktop/Checkmate.Desktop.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false \
  -o "$OUT" || fail "The build failed. Scroll up for the first 'error' line."

[ -f "$OUT/Checkmate" ] || fail "The build finished but $OUT/Checkmate was not created."
chmod +x "$OUT/Checkmate"
ok "Built: $OUT/Checkmate ($(du -h "$OUT/Checkmate" | cut -f1))"

# Avalonia needs a few common desktop libraries
if command -v ldconfig >/dev/null 2>&1 && ! ldconfig -p 2>/dev/null | grep -q 'libfontconfig'; then
  warn "libfontconfig was not found. If Checkmate will not start, install it:"
  warn "  Debian/Ubuntu: sudo apt install libfontconfig1 libx11-6 libice6 libsm6 libopenal1"
  warn "  Fedora:        sudo dnf install fontconfig libX11 libICE libSM openal-soft"
fi

# ---- optional: add to the application menu ----
LAUNCH="$OUT/Checkmate"
if [ "$INSTALL" = 1 ]; then
  step "Installing for your user (no sudo needed)"
  DEST="$HOME/.local/share/Checkmate"
  BIN="$HOME/.local/bin"
  APPS="$HOME/.local/share/applications"
  ICONS="$HOME/.local/share/icons"
  mkdir -p "$DEST" "$BIN" "$APPS" "$ICONS"
  rm -rf "$DEST"; mkdir -p "$DEST"
  cp -R "$OUT/." "$DEST/"
  ln -sf "$DEST/Checkmate" "$BIN/checkmate"

  ICON_LINE=""
  if [ -f "$ROOT/assets/logo.png" ]; then
    cp "$ROOT/assets/logo.png" "$ICONS/checkmate.png"
    ICON_LINE="Icon=$ICONS/checkmate.png"
  fi
  cat > "$APPS/checkmate.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Checkmate
Comment=Free 2D animation studio for anime fans
Exec=$DEST/Checkmate
$ICON_LINE
Terminal=false
Categories=Graphics;2DGraphics;
StartupWMClass=Checkmate
DESKTOP
  command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" 2>/dev/null || true
  LAUNCH="$DEST/Checkmate"
  ok "Installed to $DEST"
  ok "Look for 'Checkmate' in your application menu, or type: checkmate"
  case ":$PATH:" in *":$BIN:"*) ;; *) warn "Add $BIN to your PATH to use the 'checkmate' command." ;; esac
fi

printf '\n%sDone!%s  Run it with:  %s\n\n' "$C_OK" "$C_OFF" "$LAUNCH"
if [ "$RUN" = 1 ]; then "$LAUNCH" >/dev/null 2>&1 & fi

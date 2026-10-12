#!/bin/bash
# Double-click this file in Finder to build Checkmate for your Mac.
cd "$(dirname "$0")" || exit 1
./build-macos.sh "$@"
echo
read -n 1 -s -r -p "Press any key to close this window..."

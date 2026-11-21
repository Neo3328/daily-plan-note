#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
out="$PWD/bin/mac"
app="$out/Daily Plan Note.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
for arch in arm64 x86_64; do
    xcrun swiftc -O -parse-as-library -target "${arch}-apple-macos13.0" -sdk "$(xcrun --sdk macosx --show-sdk-path)" mac/DailyPlanNote.swift -o "$out/$arch"
done
lipo -create "$out/arm64" "$out/x86_64" -output "$app/Contents/MacOS/DailyPlanNote"
cp mac/Info.plist "$app/Contents/Info.plist"
codesign --force --sign - "$app"
codesign --verify --strict "$app"
lipo -verify_arch arm64 x86_64 "$app/Contents/MacOS/DailyPlanNote"
hdiutil create -volname "Daily Plan Note" -srcfolder "$app" -format UDZO -ov "$out/DailyPlanNote-macOS.dmg"
hdiutil verify "$out/DailyPlanNote-macOS.dmg"

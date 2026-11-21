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
cp mac/note.icns "$app/Contents/Resources/note.icns"
codesign --force --sign - "$app"
codesign --verify --strict "$app"
lipo "$app/Contents/MacOS/DailyPlanNote" -verify_arch arm64 x86_64
stage=$(mktemp -d)
ditto "$app" "$stage/Daily Plan Note.app"
ln -s /Applications "$stage/Applications"
hdiutil create -volname "Daily Plan Note" -srcfolder "$stage" -format UDZO -ov "$out/DailyPlanNote-macOS.dmg"
hdiutil verify "$out/DailyPlanNote-macOS.dmg"

mount=$(mktemp -d)
hdiutil attach -readonly -nobrowse -mountpoint "$mount" "$out/DailyPlanNote-macOS.dmg"
trap 'hdiutil detach "$mount"' EXIT
test -x "$mount/Daily Plan Note.app/Contents/MacOS/DailyPlanNote"
test -L "$mount/Applications"


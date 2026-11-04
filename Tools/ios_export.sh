#!/bin/zsh
# Exports the iPhone Xcode project on this Mac, with no phone attached: the first half of
# ios_build_and_run.sh, on its own, for a Mac that only has to build (a borrowed one, or a CI runner).
#
#   zsh Tools/ios_export.sh
#
# Needs Unity 6000.6.0f1 with its iOS Build Support module (GitHub's macOS runners can install it;
# see .github/workflows/ios-build.yml). The Xcode project lands in Builds/iOS, and
#   zsh Tools/ios_build_and_run.sh --xcode --ipa
# turns it into an unsigned .ipa, or
#   zsh Tools/ios_build_and_run.sh --xcode
# installs it on a connected iPhone.
set -u
P=${0:A:h:h}
# Another Unity 6000.6 install: UNITY=/path/to/Unity.app/Contents/MacOS/Unity zsh Tools/ios_export.sh
U=${UNITY:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}
LOGS=${TMPDIR:-/tmp}/cardboardxr
mkdir -p $LOGS
cd $P || exit 1

if [ ! -x "$U" ]; then
    echo "No Unity editor at $U"
    echo "  Install Unity 6000.6.0f1 from Unity Hub with its iOS Build Support module, or point at"
    echo "  another install: UNITY=/path/to/Unity.app/Contents/MacOS/Unity zsh Tools/ios_export.sh"
    echo "  With an export made elsewhere, only Xcode is needed: zsh Tools/ios_build_and_run.sh --xcode --ipa"
    exit 1
fi
if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then echo "Unity is running; close it first"; exit 1; fi

zsh $P/Tools/make_videos.sh || exit 1
echo "== Unity export"
rm -f $LOGS/ios.log
"$U" -batchmode -quit -projectPath $P -buildTarget iOS \
    -executeMethod ProjectBootstrap.StereoBuilder.BuildIos -logFile $LOGS/ios.log
grep -E "\[Build\]|error CS" $LOGS/ios.log | head -10
grep -q "Build Succeeded" $LOGS/ios.log || { echo "Unity export failed; see $LOGS/ios.log"; exit 1; }

echo "Xcode project: $P/Builds/iOS"

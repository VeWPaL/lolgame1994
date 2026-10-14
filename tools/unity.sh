#!/usr/bin/env bash
# The Unity batch commands from docs/STATUS.md, one place, run from the repo root (bash, not
# PowerShell: Start-Process -Wait can hang on Unity). Logs land in unity/Logs/ (ignored).
#   tools/unity.sh bootstrap | test | build | publish [dir] | shot <view> [out.png]   (views: ScreenshotMode.cs, docs/STATUS.md)
# The gate runs test, build and the menu/game/hearts shots with: .\verify.ps1 -Unity
# `shot game` also checks the tick rate, `shot hearts` the health row's pixels; a failed check exits 1.
# UNITY overrides the editor path.
set -u
UNITY=${UNITY:-"/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe"}
[ -d unity/Assets ] || { echo "run from the repo root"; exit 2; }
mkdir -p unity/Logs
case "${1:-}" in bootstrap|test|build)
  [ -x "$UNITY" ] || { echo "no Unity editor at $UNITY (set UNITY=...)"; exit 2; } ;;
esac

case "${1:-}" in
  bootstrap)
    "$UNITY" -batchmode -nographics -projectPath unity \
      -executeMethod Depths.Unity.EditorTools.Bootstrap.Run -quit -logFile unity/Logs/bootstrap.log
    rc=$?
    grep -E "error CS|\[Depths\]|Exception" unity/Logs/bootstrap.log | head -20
    exit $rc ;;
  test)
    rm -f unity/Logs/editmode.xml
    "$UNITY" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
      -testResults "$PWD/unity/Logs/editmode.xml" -logFile unity/Logs/editmode.log
    rc=$?
    [ -f unity/Logs/editmode.xml ] || { echo "no results file; see unity/Logs/editmode.log"; exit 1; }
    head -c 600 unity/Logs/editmode.xml | grep -o '<test-run[^>]*>' | grep -oE '(total|passed|failed|skipped)="[0-9]+"' | tr '\n' ' '; echo
    # each failed case with the first lines of its message (the CDATA after the case's <message>)
    awk '/<test-case /{name=$0; sub(/.*fullname="/,"",name); sub(/".*/,"",name); failed=($0 ~ /result="Failed"/); n=0}
         failed && /<message>/{grab=1}
         grab && n<4 {line=$0; gsub(/.*<!\[CDATA\[|\]\]>.*|^[ \t]+/,"",line); if(line!="") {print (n==0?"FAILED " name ": ":"    ") line; n++}}
         /<\/message>/{grab=0}' unity/Logs/editmode.xml
    exit $rc ;;
  build)
    "$UNITY" -batchmode -nographics -projectPath unity \
      -executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows -quit -logFile unity/Logs/build.log
    rc=$?
    grep -E "error CS|\[Depths\] Build" unity/Logs/build.log | head -20
    exit $rc ;;
  shot)
    view=${2:?view: menu|controls|audio|gameplay|game|hearts|boss|sandbox|summary|pause|banner|warning|lowhp|ledger|lab|labrow|secret|rail|...}
    out=${3:-"$PWD/unity/Logs/shot-$view.png"}
    [ -x unity/Build/Depths.exe ] || { echo "build first: tools/unity.sh build"; exit 2; }
    rm -f "$out"
    # 1280x720 windowed: the hearts check samples panel positions at 16:9
    unity/Build/Depths.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
      -depthsShot "$out" -depthsView "$view" ${DEPTHS_SEED:+-depthsSeed "$DEPTHS_SEED"} -logFile "unity/Logs/shot-$view.log" > /dev/null
    rc=$?
    grep -E '\[Depths\] (check|note)' "unity/Logs/shot-$view.log"
    [ -f "$out" ] && echo "wrote $out" || { echo "no screenshot; see unity/Logs/shot-$view.log"; exit 1; }
    # a failure keeps its log: a rare one is only diagnosable from the run that showed it
    if grep -qF "[Depths] check FAIL" "unity/Logs/shot-$view.log"; then
      keep="unity/Logs/fail-$view-$(date +%Y%m%d-%H%M%S).log"; cp "unity/Logs/shot-$view.log" "$keep"; echo "kept $keep"; exit 1
    fi
    exit $rc ;;
  publish)
    # the owner's playtest copy: one fixed folder outside every checkout, replaced whole by the current build
    dest=${2:-"/c/Users/neefloW/Documents/Depths-playtest"}
    [ -x unity/Build/Depths.exe ] || { echo "build first: tools/unity.sh build"; exit 2; }
    rm -rf "$dest.new" && cp -r unity/Build "$dest.new" || exit 1
    { echo "Depths playtest build"; echo "commit $(git rev-parse --short HEAD) on $(git branch --show-current)"; echo "built $(date -r "$(find unity/Build -type f -printf '%T@ %p
' | sort -n | tail -1 | cut -d' ' -f2-)" '+%Y-%m-%d %H:%M')"; } > "$dest.new/BUILD.txt"
    rm -rf "$dest.old"; [ -d "$dest" ] && mv "$dest" "$dest.old"; mv "$dest.new" "$dest" && rm -rf "$dest.old"
    echo "published to $dest"; cat "$dest/BUILD.txt" ;;
  *) echo "usage: tools/unity.sh bootstrap | test | build | shot <view> [out.png] | publish [dir]"; exit 2 ;;
esac

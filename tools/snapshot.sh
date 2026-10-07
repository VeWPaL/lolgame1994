#!/usr/bin/env bash
# Archives one state of the game: the Windows build, the source at HEAD (plus any uncommitted
# diff), screenshots and a note, in $SNAPSHOTS/<name>/. Never overwrites a snapshot.
#   tools/snapshot.sh <DDMMYY.V.S> "what changed"
# Names are day-month-year.version.subversion (owner's scheme); INDEX.md lists them in time order,
# since DDMMYY does not sort by date across months. Run from the repo root after tools/unity.sh build.
set -u
SNAPSHOTS=${SNAPSHOTS:-"/c/Users/neefloW/Documents/Depths-snapshots"}
name=${1:?usage: tools/snapshot.sh <DDMMYY.V.S> "note"}
note=${2:?a one-line note on what this state is}
echo "$name" | grep -qE '^[0-9]{6}\.[0-9]+\.[0-9]+$' || { echo "name must be DDMMYY.V.S, e.g. 071026.1.0"; exit 2; }
dir="$SNAPSHOTS/$name"
[ -e "$dir" ] && { echo "$dir exists; snapshots are never overwritten"; exit 2; }
[ -x unity/Build/Depths.exe ] || { echo "no unity/Build/Depths.exe: run tools/unity.sh build first"; exit 2; }
mkdir -p "$dir"

cp -r unity/Build "$dir/build"
git archive --format=zip -o "$dir/source.zip" HEAD
git diff HEAD > "$dir/uncommitted.diff"
[ -s "$dir/uncommitted.diff" ] || rm "$dir/uncommitted.diff"
cp depths.html "$dir/" 2>/dev/null; mkdir -p "$dir/js" && cp -r src "$dir/js/" 2>/dev/null
for v in menu controls audio gameplay game hearts; do
  [ -f "unity/Logs/shot-$v.png" ] && cp "unity/Logs/shot-$v.png" "$dir/"
done
commit=$(git rev-parse --short HEAD)
{
  echo "# $name"
  echo
  echo "$note"
  echo
  echo "- date: $(date '+%Y-%m-%d %H:%M')"
  echo "- branch: $(git branch --show-current), commit $commit$( [ -f "$dir/uncommitted.diff" ] && echo ' + uncommitted.diff')"
  echo "- play: build/Depths.exe (the JS reference: depths.html)"
} > "$dir/README.md"
[ -f "$SNAPSHOTS/INDEX.md" ] || printf "# Depths snapshots\n\nOldest first. Name = DDMMYY.version.subversion.\n\n" > "$SNAPSHOTS/INDEX.md"
echo "- $(date '+%Y-%m-%d %H:%M')  **$name**  ($commit)  $note" >> "$SNAPSHOTS/INDEX.md"
echo "snapshot $dir"

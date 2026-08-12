#!/bin/sh
set -eu

root=/volume1/NAS/__DanmuPlusFixture_2.0.3r3__
state=/volume2/@appdata/EmbyServer/plugins/Emby.Plugin.Danmu/composite-seasons
marker=$state/composite-season-v1-ab6c9aec2359fa0f5a6580376e13129863f26bc0139b16666179604d68f9661e.json

test -d "$root"
test ! -L "$root"
test "$(realpath "$root")" = "$root"
test -f "$marker"
grep -q '"SeasonId":"683802d49be16e37ebd02b7a08ba56a8"' "$marker"
test "$(grep -l '"SeasonId":"683802d49be16e37ebd02b7a08ba56a8"' "$state"/*.json | wc -l | tr -d ' ')" = 1

rm -f "$marker"
rm -rf "$root"
test ! -e "$marker"
test ! -e "$root"
echo "RemovedFixtureMarker=$marker"
echo "RemovedFixtureRoot=$root"

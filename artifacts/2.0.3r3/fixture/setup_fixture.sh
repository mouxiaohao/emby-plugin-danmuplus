#!/bin/sh
set -eu

root=/volume1/NAS/__DanmuPlusFixture_2.0.3r3__
series="$root/间谍过家家-r3-fixture (2022)"
season="$series/Season 01"
stage=/tmp/danmu-2.0.3r3-stage/fixture

test ! -e "$root"
test ! -L /volume1/NAS
mkdir -p "$season"
install -o emby -g users -m 0644 "$stage/tvshow.nfo" "$series/tvshow.nfo"
install -o emby -g users -m 0644 "$stage/season.nfo" "$season/season.nfo"
i=1
while [ "$i" -le 25 ]; do
  number=$(printf '%02d' "$i")
  install -o emby -g users -m 0644 "$stage/episode.strm" \
    "$season/间谍过家家-r3-fixture.S01E$number.strm"
  i=$((i + 1))
done
chown -R emby:users "$root"
chmod 0750 "$root" "$series" "$season"
test "$(find "$season" -maxdepth 1 -type f -name '*.strm' | wc -l | tr -d ' ')" = 25
realpath "$root"
find "$root" -type f -print0 | sort -z | xargs -0 sha256sum | sha256sum

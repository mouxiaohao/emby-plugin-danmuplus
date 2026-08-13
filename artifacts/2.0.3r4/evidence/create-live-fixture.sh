#!/bin/sh
set -eu

root='/volume1/__DanmuPlusFixture_2.0.3r4__'
stream='nfs://192.168.50.200/volume1/NAS/Anime/动漫/一拳超人(2015)/Season1/一拳超人-S01E01-第01集.mkv'

test ! -e "$root"
mkdir -p "$root/Complete Fixture (2026)/Season 01"
mkdir -p "$root/Placed Fixture (2026)/Season 01"
mkdir -p "$root/Placed Fixture (2026)/Specials"

write_series() {
    path="$1"
    title="$2"
    printf '%s\n' \
        '<?xml version="1.0" encoding="utf-8"?>' \
        '<tvshow>' \
        "  <title>$title</title>" \
        "  <sorttitle>$title</sorttitle>" \
        '  <year>2026</year>' \
        '  <plot>Disposable DanmuPlus 2.0.3r4 live fixture.</plot>' \
        '</tvshow>' > "$path/tvshow.nfo"
}

write_episode() {
    base="$1"
    season="$2"
    episode="$3"
    title="$4"
    printf '%s\n' "$stream" > "$base.strm"
    printf '%s\n' \
        '<?xml version="1.0" encoding="utf-8"?>' \
        '<episodedetails>' \
        "  <title>$title</title>" \
        "  <season>$season</season>" \
        "  <episode>$episode</episode>" \
        '  <aired>2026-01-01</aired>' \
        '</episodedetails>' > "$base.nfo"
}

write_series "$root/Complete Fixture (2026)" 'DanmuPlus R4 Complete Fixture'
write_series "$root/Placed Fixture (2026)" 'DanmuPlus R4 Placed Fixture'

i=1
while [ "$i" -le 12 ]; do
    number="$(printf '%02d' "$i")"
    write_episode "$root/Complete Fixture (2026)/Season 01/Complete.S01E$number" 1 "$i" "Complete Episode $number"
    write_episode "$root/Placed Fixture (2026)/Season 01/Placed.S01E$number" 1 "$i" "Placed Main Episode $number"
    i=$((i + 1))
done
write_episode "$root/Placed Fixture (2026)/Specials/Placed.S00E01" 0 1 'Placed Special Episode 01'

chown -R mouxiaohao:users "$root"
chmod -R u=rwX,g=rwX,o= "$root"
find "$root" -type f -print0 | sort -z | xargs -0 sha256sum

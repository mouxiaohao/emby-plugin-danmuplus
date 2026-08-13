#!/bin/sh
set -eu

stage_dir="$1"
backup_dir="$2"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
service="pkgctl-EmbyServer"

expected_r5_dll="123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552"
expected_r5_css="b6fad60cb15139ca605bc78521e42497a9e99f935564644f4fab620cc26d54b5"
expected_r5_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"
expected_r6_dll="dc437aea76f1db9b437257a9829b4ebb958815f1065102307835bffc9cf52807"
expected_r6_js="6f1a78e04397f377c0bd50129bc83857ee8cd3a8cd9a37d4bb7138a5946f397c"

hash_of() { sha256sum "$1" | awk '{print $1}'; }
require_hash() {
    actual="$(hash_of "$1")"
    [ "$actual" = "$2" ] || { echo "Hash mismatch: $1 ($actual)" >&2; exit 1; }
}
wait_stopped() {
    i=0
    while [ "$i" -lt 30 ] && netstat -lntp 2>/dev/null | grep -q ':8096 .*EmbyServer'; do
        sleep 1
        i=$((i + 1))
    done
    ! netstat -lntp 2>/dev/null | grep -q ':8096 .*EmbyServer'
}
wait_started() {
    i=0
    while [ "$i" -lt 60 ] && ! curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null 2>&1; do
        sleep 1
        i=$((i + 1))
    done
    curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null
}

require_hash "$dll" "$expected_r5_dll"
require_hash "$css" "$expected_r5_css"
require_hash "$danmu" "$expected_r5_danmu"
require_hash "$stage_dir/Emby.Plugin.Danmu.dll" "$expected_r6_dll"
require_hash "$stage_dir/DanmuSmartMatch.CustomCssJS.js" "$expected_r6_js"
[ "$(grep -o '__embyDanmuSmartMenuV21' "$css" | wc -l | tr -d ' ')" = "1" ]
! grep -q '__embyDanmuSmartMenuV22' "$css"

[ ! -e "$backup_dir" ] || { echo "Backup target already exists: $backup_dir" >&2; exit 1; }
mkdir -p "$backup_dir"
cp -p "$dll" "$backup_dir/Emby.Plugin.Danmu.dll"
cp -p "$css" "$backup_dir/Emby.CustomCssJS.xml"
cp -p "$danmu" "$backup_dir/Emby.Plugin.Danmu.xml"
(cd "$backup_dir" && sha256sum Emby.Plugin.Danmu.dll Emby.CustomCssJS.xml Emby.Plugin.Danmu.xml > SHA256SUMS)

rollback() {
    echo "Deployment failed; restoring paired r5 backup." >&2
    cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$dll"
    cp -p "$backup_dir/Emby.CustomCssJS.xml" "$css"
    cp -p "$backup_dir/Emby.Plugin.Danmu.xml" "$danmu"
    /usr/syno/bin/synosystemctl start "$service" >/dev/null 2>&1 || true
}
trap rollback EXIT HUP INT TERM

/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
wait_stopped
cp "$stage_dir/Emby.Plugin.Danmu.dll" "$dll"
chown emby:users "$dll"
chmod 644 "$dll"
python3 "$stage_dir/update_customcssjs.py" "$css" "$stage_dir/DanmuSmartMatch.CustomCssJS.js"
chown emby:users "$css"
chmod 444 "$css"
chown emby:emby "$danmu"
chmod 444 "$danmu"
require_hash "$dll" "$expected_r6_dll"
[ "$(grep -o '__embyDanmuSmartMenuV22' "$css" | wc -l | tr -d ' ')" = "1" ]
! grep -q '__embyDanmuSmartMenuV21' "$css"

/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
chmod 444 "$backup_dir"/*
chmod 555 "$backup_dir"
echo "BackupDir=$backup_dir"
echo "R6Dll=$(hash_of "$dll")"
echo "R6Css=$(hash_of "$css")"
echo "DanmuConfig=$(hash_of "$danmu")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

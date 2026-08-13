#!/bin/sh
set -eu

stage_dir="$1"
backup_dir="$2"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
new_dll="$plugin_dir/.Emby.Plugin.Danmu.r8.new"
service="pkgctl-EmbyServer"

expected_r7_dll="7755c242bf6f68d38b4c062b8a542571dc66a33b578b706e4c4ba3c32c2a2c72"
expected_r7_css="abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb"
expected_r7_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"
expected_r7_js="af10dffd6605a24ad19da777424e4dbc3afd12a17739f210bd3d96d065466feb"
expected_r8_dll="0199880314a30675c7f3ca17ae72b324e735f2d7cd924ed9c22dc5f4720335ce"

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

require_hash "$dll" "$expected_r7_dll"
require_hash "$css" "$expected_r7_css"
require_hash "$danmu" "$expected_r7_danmu"
require_hash "$stage_dir/Emby.Plugin.Danmu.dll" "$expected_r8_dll"
require_hash "$stage_dir/DanmuSmartMatch.CustomCssJS.js" "$expected_r7_js"
[ "$(grep -o '__embyDanmuSmartMenuV23' "$css" | wc -l | tr -d ' ')" = "1" ]

[ ! -e "$backup_dir" ] || { echo "Backup target already exists: $backup_dir" >&2; exit 1; }
mkdir -p "$backup_dir"
cp -p "$dll" "$backup_dir/Emby.Plugin.Danmu.dll"
cp -p "$css" "$backup_dir/Emby.CustomCssJS.xml"
cp -p "$danmu" "$backup_dir/Emby.Plugin.Danmu.xml"
(cd "$backup_dir" && sha256sum Emby.Plugin.Danmu.dll Emby.CustomCssJS.xml Emby.Plugin.Danmu.xml > SHA256SUMS)

rollback() {
    echo "Deployment failed; restoring paired r7 backup." >&2
    rm -f "$new_dll"
    cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$dll"
    cp -p "$backup_dir/Emby.CustomCssJS.xml" "$css"
    cp -p "$backup_dir/Emby.Plugin.Danmu.xml" "$danmu"
    /usr/syno/bin/synosystemctl start "$service" >/dev/null 2>&1 || true
}
trap rollback EXIT HUP INT TERM

/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
wait_stopped
cp "$stage_dir/Emby.Plugin.Danmu.dll" "$new_dll"
chown emby:users "$new_dll"
chmod 644 "$new_dll"
mv -f "$new_dll" "$dll"
require_hash "$dll" "$expected_r8_dll"
require_hash "$css" "$expected_r7_css"
require_hash "$danmu" "$expected_r7_danmu"
[ "$(grep -o '__embyDanmuSmartMenuV23' "$css" | wc -l | tr -d ' ')" = "1" ]

/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
chmod 444 "$backup_dir"/*
chmod 555 "$backup_dir"
echo "BackupDir=$backup_dir"
echo "R8Dll=$(hash_of "$dll")"
echo "R7Css=$(hash_of "$css")"
echo "DanmuConfig=$(hash_of "$danmu")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

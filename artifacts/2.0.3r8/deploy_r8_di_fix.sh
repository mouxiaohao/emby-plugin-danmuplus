#!/bin/sh
set -eu

stage_dir="$1"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$plugin_dir/configurations/Emby.CustomCssJS.xml"
danmu="$plugin_dir/configurations/Emby.Plugin.Danmu.xml"
new_dll="$plugin_dir/.Emby.Plugin.Danmu.r8.di-fix.new"
service="pkgctl-EmbyServer"
expected_broken="6fd3172f6d902d65bf67520759ca68ab7eca9e7a3248224ee75fdc673273e6ec"
expected_fixed="0199880314a30675c7f3ca17ae72b324e735f2d7cd924ed9c22dc5f4720335ce"
expected_css="abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb"
expected_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"
rollback_dir="/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r8-final-20260813-acceptance"

hash_of() { sha256sum "$1" | awk '{print $1}'; }
require_hash() { [ "$(hash_of "$1")" = "$2" ] || { echo "Hash mismatch: $1" >&2; exit 1; }; }
wait_started() { i=0; while [ "$i" -lt 60 ] && ! curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null 2>&1; do sleep 1; i=$((i+1)); done; curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null; }

require_hash "$dll" "$expected_broken"
require_hash "$css" "$expected_css"
require_hash "$danmu" "$expected_danmu"
require_hash "$stage_dir/Emby.Plugin.Danmu.dll" "$expected_fixed"
require_hash "$rollback_dir/Emby.Plugin.Danmu.dll" "7755c242bf6f68d38b4c062b8a542571dc66a33b578b706e4c4ba3c32c2a2c72"

rollback() {
    rm -f "$new_dll"
    cp -p "$rollback_dir/Emby.Plugin.Danmu.dll" "$dll"
    cp -p "$rollback_dir/Emby.CustomCssJS.xml" "$css"
    cp -p "$rollback_dir/Emby.Plugin.Danmu.xml" "$danmu"
    /usr/syno/bin/synosystemctl start "$service" >/dev/null 2>&1 || true
}
trap rollback EXIT HUP INT TERM
/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
cp "$stage_dir/Emby.Plugin.Danmu.dll" "$new_dll"
chown emby:users "$new_dll"
chmod 644 "$new_dll"
mv -f "$new_dll" "$dll"
require_hash "$dll" "$expected_fixed"
require_hash "$css" "$expected_css"
require_hash "$danmu" "$expected_danmu"
/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
echo "R8FixedDll=$(hash_of "$dll")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

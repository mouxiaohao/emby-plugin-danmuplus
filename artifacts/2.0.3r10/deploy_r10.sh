#!/bin/sh
set -eu

stage_dir="$1"
backup_dir="$2"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
new_dll="$plugin_dir/.Emby.Plugin.Danmu.r10.new"
new_css="$config_dir/.Emby.CustomCssJS.r10.new"
new_danmu="$config_dir/.Emby.Plugin.Danmu.r10.new"
service="pkgctl-EmbyServer"

expected_r9_dll="7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308"
expected_r9_css="abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb"
expected_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"
expected_r10_dll="3b2dbf02f4ef1e47e07d5fc541425b87628bb933359c8af5afad5be13fbdf8d2"
expected_r10_js="0ff2df87ae87afe3b05e265d4b0aa4748d0a27ad09fdb96d97caad12348b6e46"

hash_of() { sha256sum "$1" | cut -d' ' -f1; }
require_hash() {
    actual="$(hash_of "$1")"
    [ "$actual" = "$2" ] || { echo "Hash mismatch: $1 ($actual)" >&2; exit 1; }
}
wait_stopped() {
    i=0
    while [ "$i" -lt 30 ] && pidof EmbyServer >/dev/null 2>&1; do sleep 1; i=$((i + 1)); done
    ! pidof EmbyServer >/dev/null 2>&1
}
wait_started() {
    i=0
    while [ "$i" -lt 60 ] && ! curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null 2>&1; do sleep 1; i=$((i + 1)); done
    curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null
}

[ "$stage_dir" = "/tmp/danmu-r10-stage" ] || { echo "Unexpected stage path" >&2; exit 1; }
case "$backup_dir" in
    "$plugin_dir"/backups/danmu-2.0.3r9-before-r10-*) ;;
    *) echo "Unexpected backup path: $backup_dir" >&2; exit 1 ;;
esac

require_hash "$dll" "$expected_r9_dll"
require_hash "$css" "$expected_r9_css"
require_hash "$danmu" "$expected_danmu"
require_hash "$stage_dir/Emby.Plugin.Danmu.dll" "$expected_r10_dll"
require_hash "$stage_dir/DanmuSmartMatch.CustomCssJS.js" "$expected_r10_js"
(cd "$backup_dir" && sha256sum -c SHA256SUMS)
require_hash "$backup_dir/Emby.Plugin.Danmu.dll" "$expected_r9_dll"
require_hash "$backup_dir/Emby.CustomCssJS.xml" "$expected_r9_css"
require_hash "$backup_dir/Emby.Plugin.Danmu.xml" "$expected_danmu"

rollback() {
    trap - EXIT HUP INT TERM
    echo "Deployment failed; restoring verified r9 trio." >&2
    rm -f "$new_dll" "$new_css" "$new_danmu"
    /usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
    if wait_stopped; then
        cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$new_dll"
        cp -p "$backup_dir/Emby.CustomCssJS.xml" "$new_css"
        cp -p "$backup_dir/Emby.Plugin.Danmu.xml" "$new_danmu"
        mv -f "$new_dll" "$dll"
        mv -f "$new_css" "$css"
        mv -f "$new_danmu" "$danmu"
        require_hash "$dll" "$expected_r9_dll"
        require_hash "$css" "$expected_r9_css"
        require_hash "$danmu" "$expected_danmu"
        /usr/syno/bin/synosystemctl start "$service"
        wait_started
        echo "Rollback restored verified r9 trio." >&2
    else
        echo "Rollback could not stop Emby; active files were not replaced." >&2
        return 1
    fi
}
trap rollback EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
wait_stopped
cp "$stage_dir/Emby.Plugin.Danmu.dll" "$new_dll"
chown emby:users "$new_dll"
chmod 644 "$new_dll"
mv -f "$new_dll" "$dll"
python3 "$stage_dir/update_customcssjs.py" "$css" "$stage_dir/DanmuSmartMatch.CustomCssJS.js"
chown emby:users "$css"
chmod 444 "$css"
chown emby:emby "$danmu"
chmod 444 "$danmu"
require_hash "$dll" "$expected_r10_dll"
require_hash "$danmu" "$expected_danmu"
[ "$(grep -o '__embyDanmuSmartMenuV23' "$css" | wc -l | tr -d ' ')" = "1" ]

/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
rm -rf "$stage_dir"
printf 'BackupDir=%s\n' "$backup_dir"
printf 'R10Dll=%s\n' "$(hash_of "$dll")"
printf 'R10Css=%s\n' "$(hash_of "$css")"
printf 'DanmuConfig=%s\n' "$(hash_of "$danmu")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

#!/bin/sh
set -eu

stage_dir="/tmp/danmu-r2-stage"
backup_dir="/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.4r2-predeploy-20260815-040854"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
new_dll="$plugin_dir/.Emby.Plugin.Danmu.r2.new"
service="pkgctl-EmbyServer"

old_dll="b31b51c882e7cd0e57790501634e72fed3f4f2b7b608bc172403bc27e6c58d9d"
old_css="0ecfd6105c49b8d512d8e0278e7affe5cb10645401a2058ceb3268755b9b8314"
old_danmu="02519afd92022babacf9e6d516c44c0dde0117a2744593501d8cb29222536069"
new_dll_hash="97e8e0d6baded7b1b9d4a780babbf133f9901bb0d61da392e0b1ec5b043a2065"
new_js_hash="d9c11dfe86864695d0d3def93aa1f7c4633ca1e35a2eeaa3e9339249c01e4180"

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

require_hash "$dll" "$old_dll"
require_hash "$css" "$old_css"
require_hash "$danmu" "$old_danmu"
require_hash "$stage_dir/Emby.Plugin.Danmu.dll" "$new_dll_hash"
require_hash "$stage_dir/DanmuSmartMatch.CustomCssJS.js" "$new_js_hash"
(cd "$backup_dir" && sha256sum -c SHA256SUMS)

rollback() {
    trap - EXIT HUP INT TERM
    echo "Deployment failed; restoring verified 2.0.4r1 files." >&2
    rm -f "$new_dll"
    /usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
    if wait_stopped; then
        cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$new_dll"
        mv -f "$new_dll" "$dll"
        cp -p "$backup_dir/Emby.CustomCssJS.xml" "$css"
        cp -p "$backup_dir/Emby.Plugin.Danmu.xml" "$danmu"
        require_hash "$dll" "$old_dll"
        require_hash "$css" "$old_css"
        require_hash "$danmu" "$old_danmu"
        /usr/syno/bin/synosystemctl start "$service"
        wait_started
        echo "Rollback restored verified 2.0.4r1 files." >&2
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
chown 240699:100 "$new_dll"
chmod 644 "$new_dll"
mv -f "$new_dll" "$dll"
python3 "$stage_dir/update_customcssjs.py" "$css" "$stage_dir/DanmuSmartMatch.CustomCssJS.js"
chown 240699:100 "$css"
chmod 444 "$css"
chown 240699:240699 "$danmu"
chmod 444 "$danmu"
require_hash "$dll" "$new_dll_hash"
require_hash "$danmu" "$old_danmu"
[ "$(grep -o '__embyDanmuSmartMenuV23' "$css" | wc -l | tr -d ' ')" = "1" ]

/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
printf 'BackupDir=%s\n' "$backup_dir"
printf 'R2Dll=%s\n' "$(hash_of "$dll")"
printf 'R2Css=%s\n' "$(hash_of "$css")"
printf 'DanmuConfig=%s\n' "$(hash_of "$danmu")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

#!/bin/sh
set -eu

plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
backup_dir="$plugin_dir/backups/danmu-2.0.3r9-before-r10-20260814-130725"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
tmp_dll="$plugin_dir/.Emby.Plugin.Danmu.r9.rollback"
tmp_css="$config_dir/.Emby.CustomCssJS.r9.rollback"
tmp_danmu="$config_dir/.Emby.Plugin.Danmu.r9.rollback"
service="pkgctl-EmbyServer"
expected_dll="7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308"
expected_css="abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb"
expected_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"

hash_of() { sha256sum "$1" | cut -d' ' -f1; }
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

(cd "$backup_dir" && sha256sum -c SHA256SUMS)
[ "$(hash_of "$backup_dir/Emby.Plugin.Danmu.dll")" = "$expected_dll" ]
[ "$(hash_of "$backup_dir/Emby.CustomCssJS.xml")" = "$expected_css" ]
[ "$(hash_of "$backup_dir/Emby.Plugin.Danmu.xml")" = "$expected_danmu" ]

/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
wait_stopped
rm -f "$tmp_dll" "$tmp_css" "$tmp_danmu"
cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$tmp_dll"
cp -p "$backup_dir/Emby.CustomCssJS.xml" "$tmp_css"
cp -p "$backup_dir/Emby.Plugin.Danmu.xml" "$tmp_danmu"
mv -f "$tmp_dll" "$dll"
mv -f "$tmp_css" "$css"
mv -f "$tmp_danmu" "$danmu"
[ "$(hash_of "$dll")" = "$expected_dll" ]
[ "$(hash_of "$css")" = "$expected_css" ]
[ "$(hash_of "$danmu")" = "$expected_danmu" ]
/usr/syno/bin/synosystemctl start "$service"
wait_started
printf 'DLL_SHA=%s\n' "$(hash_of "$dll")"
printf 'CSS_SHA=%s\n' "$(hash_of "$css")"
printf 'CONFIG_SHA=%s\n' "$(hash_of "$danmu")"
printf 'SERVICE='; /usr/syno/bin/synosystemctl get-active-status "$service"
printf 'PID='; pidof EmbyServer
printf 'HEALTH='; curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

#!/bin/sh
set -eu

stage_dll="$1"
backup_dir="$2"
plugin_dir="/volume2/@appdata/EmbyServer/plugins"
config_dir="$plugin_dir/configurations"
dll="$plugin_dir/Emby.Plugin.Danmu.dll"
css="$config_dir/Emby.CustomCssJS.xml"
danmu="$config_dir/Emby.Plugin.Danmu.xml"
new_dll="$plugin_dir/.Emby.Plugin.Danmu.r9.new"
service="pkgctl-EmbyServer"

expected_r8_dll="0199880314a30675c7f3ca17ae72b324e735f2d7cd924ed9c22dc5f4720335ce"
expected_r9_dll="7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308"
expected_css="abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb"
expected_danmu="a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973"

hash_of() { sha256sum "$1" | awk '{print $1}'; }
require_hash() {
    actual="$(hash_of "$1")"
    [ "$actual" = "$2" ] || { echo "Hash mismatch: $1 ($actual)" >&2; exit 1; }
}
wait_stopped() {
    i=0
    while [ "$i" -lt 30 ] && pidof EmbyServer >/dev/null 2>&1; do
        sleep 1
        i=$((i + 1))
    done
    ! pidof EmbyServer >/dev/null 2>&1
}
wait_started() {
    i=0
    while [ "$i" -lt 60 ] && ! curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null 2>&1; do
        sleep 1
        i=$((i + 1))
    done
    curl -fsS http://127.0.0.1:8096/emby/System/Info/Public >/dev/null
}

[ "$stage_dll" = "/tmp/Emby.Plugin.Danmu.r9.dll" ] || { echo "Unexpected stage path" >&2; exit 1; }
case "$backup_dir" in
    /volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r8-before-r9-*) ;;
    *) echo "Unexpected backup path: $backup_dir" >&2; exit 1 ;;
esac

require_hash "$dll" "$expected_r8_dll"
require_hash "$stage_dll" "$expected_r9_dll"
require_hash "$css" "$expected_css"
require_hash "$danmu" "$expected_danmu"
[ ! -e "$backup_dir" ] || { echo "Backup target already exists: $backup_dir" >&2; exit 1; }

mkdir "$backup_dir"
cp -p "$dll" "$backup_dir/Emby.Plugin.Danmu.dll"
cp -p "$css" "$backup_dir/Emby.CustomCssJS.xml"
cp -p "$danmu" "$backup_dir/Emby.Plugin.Danmu.xml"
(cd "$backup_dir" && sha256sum Emby.Plugin.Danmu.dll Emby.CustomCssJS.xml Emby.Plugin.Danmu.xml > SHA256SUMS)

rollback() {
    # The rollback itself can fail; never let its EXIT trap re-enter it.
    trap - EXIT HUP INT TERM
    echo "Deployment failed; restoring r8 backup." >&2
    rollback_failed=0
    r8_restored=0
    rollback_tmp="$plugin_dir/.Emby.Plugin.Danmu.r8.rollback.$$"

    rm -f "$new_dll" "$rollback_tmp" || {
        echo "Rollback warning: could not remove a temporary DLL." >&2
        rollback_failed=1
    }

    if ! /usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1; then
        echo "Rollback error: could not stop $service." >&2
        rollback_failed=1
    fi
    if ! wait_stopped; then
        echo "Rollback error: EmbyServer did not exit; r8 DLL was not replaced." >&2
        rollback_failed=1
    else
        if ! cp -p "$backup_dir/Emby.Plugin.Danmu.dll" "$rollback_tmp"; then
            echo "Rollback error: could not stage the r8 DLL." >&2
            rollback_failed=1
        elif ! mv -f "$rollback_tmp" "$dll"; then
            echo "Rollback error: could not atomically restore the r8 DLL." >&2
            rollback_failed=1
        else
            if ! actual_r8_dll="$(hash_of "$dll")"; then
                echo "Rollback error: could not calculate the restored r8 DLL hash." >&2
                rollback_failed=1
            elif [ "$actual_r8_dll" != "$expected_r8_dll" ]; then
                echo "Rollback error: restored r8 DLL hash mismatch: $actual_r8_dll" >&2
                rollback_failed=1
            else
                r8_restored=1
            fi
        fi
    fi

    if [ "$r8_restored" -eq 1 ]; then
        if ! /usr/syno/bin/synosystemctl start "$service"; then
            echo "Rollback error: could not start $service." >&2
            rollback_failed=1
        fi
        if ! wait_started; then
            echo "Rollback error: EmbyServer did not become ready after rollback." >&2
            rollback_failed=1
        fi
    else
        echo "Rollback error: r8 DLL was not atomically restored and hash-verified; service will remain stopped." >&2
        echo "Manual action required: restore $backup_dir/Emby.Plugin.Danmu.dll, verify $expected_r8_dll, then start $service." >&2
        return 1
    fi

    if [ "$rollback_failed" -ne 0 ]; then
        echo "Rollback completed with failures; inspect the errors above before retrying deployment." >&2
        return 1
    fi
    echo "Rollback restored and started the verified r8 DLL." >&2
}
on_signal() {
    signal_status="$1"
    if rollback; then
        exit "$signal_status"
    fi
    exit 1
}
trap rollback EXIT
trap 'on_signal 129' HUP
trap 'on_signal 130' INT
trap 'on_signal 143' TERM

/usr/syno/bin/synosystemctl stop "$service" >/dev/null 2>&1 || true
wait_stopped
cp "$stage_dll" "$new_dll"
chown emby:users "$new_dll"
chmod 644 "$new_dll"
mv -f "$new_dll" "$dll"
require_hash "$dll" "$expected_r9_dll"
require_hash "$css" "$expected_css"
require_hash "$danmu" "$expected_danmu"

/usr/syno/bin/synosystemctl start "$service"
wait_started
trap - EXIT HUP INT TERM
chmod 444 "$backup_dir"/*
chmod 555 "$backup_dir"
rm -f "$stage_dll"
echo "BackupDir=$backup_dir"
echo "R9Dll=$(hash_of "$dll")"
echo "Css=$(hash_of "$css")"
echo "DanmuConfig=$(hash_of "$danmu")"
curl -fsS http://127.0.0.1:8096/emby/System/Info/Public

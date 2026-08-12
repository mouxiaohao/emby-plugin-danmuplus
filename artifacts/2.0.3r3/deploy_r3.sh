#!/bin/sh
set -eu

app=/volume2/@appdata/EmbyServer
plugin=$app/plugins/Emby.Plugin.Danmu.dll
danmuxml=$app/plugins/configurations/Emby.Plugin.Danmu.xml
cssxml=$app/plugins/configurations/Emby.CustomCssJS.xml
backup=$app/plugins/backups/danmu-2.0.3r3-predeploy-r2-baseline
stage=/tmp/danmu-2.0.3r3-stage
expected_exe=/volume2/@appstore/EmbyServer/system/EmbyServer

pid=$(pidof EmbyServer)
test -n "$pid"
test "$(readlink /proc/$pid/exe)" = "$expected_exe"
netstat -lntp 2>/dev/null | grep -q ':8096 '
test "$(sha256sum "$plugin" | cut -d' ' -f1)" = 617d4491d9b5726ea04b9571cc1b53ea9ea7d3ab7a3bd235a9a9002edb493912
test "$(sha256sum "$danmuxml" | cut -d' ' -f1)" = a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973
test "$(sha256sum "$cssxml" | cut -d' ' -f1)" = 00d056c30d0b406551222524aedc0a9f7107bd2b2b58f7d0872def648f11ed3d
test ! -e "$backup"

/usr/syno/bin/synosystemctl stop pkgctl-EmbyServer || true
i=0
while [ "$i" -lt 30 ] && kill -0 "$pid" 2>/dev/null; do
  sleep 1
  i=$((i + 1))
done
if kill -0 "$pid" 2>/dev/null; then
  echo "Emby did not stop" >&2
  exit 1
fi

mkdir -m 700 "$backup"
cp -p "$plugin" "$backup/Emby.Plugin.Danmu.dll.r2"
cp -p "$danmuxml" "$backup/Emby.Plugin.Danmu.xml.r2"
cp -p "$cssxml" "$backup/Emby.CustomCssJS.xml.r2"
mkdir "$backup/database" "$backup/server-config" "$backup/composite-state"
for file in "$app/data/library.db" "$app/data/library.db-wal" "$app/data/library.db-shm"; do
  [ ! -e "$file" ] || cp -p "$file" "$backup/database/"
done
cp -a "$app/config/." "$backup/server-config/"
find "$app" -type d -name composite-seasons -print > "$backup/composite-state/paths.txt"
while IFS= read -r directory; do
  [ -z "$directory" ] || cp -a "$directory" "$backup/composite-state/"
done < "$backup/composite-state/paths.txt"
sha256sum "$backup/Emby.Plugin.Danmu.dll.r2" "$backup/Emby.Plugin.Danmu.xml.r2" \
  "$backup/Emby.CustomCssJS.xml.r2" > "$backup/r2-trio.sha256"
sha256sum "$backup"/database/* > "$backup/database.sha256" 2>/dev/null || true

rollback() {
  cp -p "$backup/Emby.Plugin.Danmu.dll.r2" "$plugin"
  cp -p "$backup/Emby.Plugin.Danmu.xml.r2" "$danmuxml"
  cp -p "$backup/Emby.CustomCssJS.xml.r2" "$cssxml"
  /usr/syno/bin/synosystemctl start pkgctl-EmbyServer || true
}
trap 'rollback' HUP INT TERM EXIT

install -o emby -g users -m 0644 "$stage/Emby.Plugin.Danmu.dll" "$plugin"
python3 "$stage/update_customcssjs.py" "$cssxml" "$stage/DanmuSmartMatch.CustomCssJS.js"
test "$(sha256sum "$plugin" | cut -d' ' -f1)" = 9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71
test "$(grep -o '__embyDanmuSmartMenuV18' "$cssxml" | wc -l | tr -d ' ')" = 0
test "$(grep -o '__embyDanmuSmartMenuV19' "$cssxml" | wc -l | tr -d ' ')" = 1

/usr/syno/bin/synosystemctl start pkgctl-EmbyServer
trap - HUP INT TERM EXIT
chmod -R a-w "$backup"
echo "Backup=$backup"
sha256sum "$plugin" "$danmuxml" "$cssxml"

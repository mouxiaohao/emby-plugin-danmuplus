#!/bin/sh
set -eu
B=/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-final-predeploy-20260812-141458
mkdir -p "$B/config" "$B/database-raw" "$B/composite-state" "$B/staging"
cp -p /var/packages/EmbyServer/var/plugins/Emby.Plugin.Danmu.dll "$B/Emby.Plugin.Danmu.dll.r3"
cp -p /var/packages/EmbyServer/var/plugins/configurations/Emby.Plugin.Danmu.xml "$B/config/Emby.Plugin.Danmu.xml.r3"
cp -p /var/packages/EmbyServer/var/plugins/configurations/Emby.CustomCssJS.xml "$B/config/Emby.CustomCssJS.xml.r3"
cp -p /var/packages/EmbyServer/var/data/library.db "$B/database-raw/library.db"
cp -p /var/packages/EmbyServer/var/data/library.db-wal "$B/database-raw/library.db-wal"
cp -p /var/packages/EmbyServer/var/data/library.db-shm "$B/database-raw/library.db-shm"
sqlite3 /var/packages/EmbyServer/var/data/library.db ".backup '$B/library-consistent.db'"
cp -a /var/packages/EmbyServer/var/plugins/Emby.Plugin.Danmu/composite-seasons "$B/composite-state/"
find "$B" -type f ! -name SHA256SUMS -exec sha256sum {} \; | sort -k2 > "$B/SHA256SUMS"
echo BACKUP_OK
wc -l "$B/SHA256SUMS"
sha256sum "$B/Emby.Plugin.Danmu.dll.r3" "$B/config/Emby.Plugin.Danmu.xml.r3" "$B/config/Emby.CustomCssJS.xml.r3"

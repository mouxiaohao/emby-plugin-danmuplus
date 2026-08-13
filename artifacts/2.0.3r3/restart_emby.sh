#!/bin/sh
set -eu

expected_exe="/volume2/@appstore/EmbyServer/system/EmbyServer"
pid="$(netstat -lntp 2>/dev/null | awk '$4 ~ /:8096$/ && $7 ~ /EmbyServer$/ { split($7, parts, "/"); print parts[1]; exit }')"
if [ -z "$pid" ] || [ ! -e "/proc/$pid/exe" ]; then
    echo "Emby listener process was not found" >&2
    exit 1
fi
actual_exe="$(readlink "/proc/$pid/exe")"
if [ "$actual_exe" != "$expected_exe" ]; then
    echo "Refusing to stop unexpected listener executable: $actual_exe" >&2
    exit 1
fi

/usr/syno/bin/synosystemctl stop pkgctl-EmbyServer || true
i=0
while [ "$i" -lt 10 ] && kill -0 "$pid" 2>/dev/null; do
    sleep 1
    i=$((i + 1))
done
if kill -0 "$pid" 2>/dev/null; then
    kill -TERM "$pid"
fi
i=0
while [ "$i" -lt 20 ] && kill -0 "$pid" 2>/dev/null; do
    sleep 1
    i=$((i + 1))
done
if kill -0 "$pid" 2>/dev/null; then
    echo "Emby listener did not exit after SIGTERM" >&2
    exit 1
fi

/usr/syno/bin/synosystemctl start pkgctl-EmbyServer
echo "StoppedEmbyPid=$pid"

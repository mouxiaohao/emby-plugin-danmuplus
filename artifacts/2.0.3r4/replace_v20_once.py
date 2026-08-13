#!/usr/bin/env python3
"""Atomically replace exactly one deployed V20 smart-match content entry."""

import html
import os
import re
import stat
import sys
import tempfile

MARKER = "__embyDanmuSmartMenuV20"
CONTENT_PATTERN = re.compile(r"(<content>)(.*?)(</content>)", re.DOTALL)


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: replace_v20_once.py CONFIG_XML SMART_MATCH_JS")
    config_path, script_path = map(os.path.abspath, sys.argv[1:])
    with open(config_path, "r", encoding="utf-8") as stream:
        config = stream.read()
    with open(script_path, "r", encoding="utf-8-sig") as stream:
        script = stream.read()
    if config.count(MARKER) != 1:
        raise RuntimeError("expected exactly one deployed V20 marker")
    if script.count(MARKER) != 1 or "__embyDanmuSmartMenuV19" in script:
        raise RuntimeError("candidate must contain exactly one V20 marker and no V19 marker")
    entries = [match for match in CONTENT_PATTERN.finditer(config) if MARKER in match.group(2)]
    if len(entries) != 1 or entries[0].group(2).count(MARKER) != 1:
        raise RuntimeError("V20 marker is not isolated inside exactly one content entry")
    entry = entries[0]
    updated = config[:entry.start(2)] + html.escape(script, quote=False) + config[entry.end(2):]
    if updated.count(MARKER) != 1 or "__embyDanmuSmartMenuV19" in updated:
        raise RuntimeError("updated configuration violates the unique V20 contract")
    original = os.stat(config_path)
    directory = os.path.dirname(config_path)
    fd, temporary = tempfile.mkstemp(prefix=".danmu-v20-refresh-", suffix=".tmp", dir=directory)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as stream:
            if hasattr(os, "fchmod"):
                os.fchmod(stream.fileno(), stat.S_IMODE(original.st_mode))
            if hasattr(os, "fchown"):
                os.fchown(stream.fileno(), original.st_uid, original.st_gid)
            stream.write(updated)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, config_path)
        if os.name == "posix":
            directory_fd = os.open(directory, os.O_RDONLY)
            try:
                os.fsync(directory_fd)
            finally:
                os.close(directory_fd)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Atomically replace the unique deployed V23 smart-match content entry."""

import html
import os
import re
import stat
import sys
import tempfile


MARKER = "__embyDanmuSmartMenuV23"
CONTENT_PATTERN = re.compile(r"(<content>)(.*?)(</content>)", re.DOTALL)


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: update_customcssjs.py CONFIG_XML SMART_MATCH_JS")
    config_path, script_path = map(os.path.abspath, sys.argv[1:])
    with open(config_path, "r", encoding="utf-8") as stream:
        config = stream.read()
    with open(script_path, "r", encoding="utf-8-sig") as stream:
        script = stream.read()
    if config.count(MARKER) != 1:
        raise RuntimeError("expected exactly one deployed V23 marker")
    if script.count(MARKER) != 1:
        raise RuntimeError("candidate script must contain exactly one V23 marker")
    matching_entries = [match for match in CONTENT_PATTERN.finditer(config) if MARKER in match.group(2)]
    if len(matching_entries) != 1 or matching_entries[0].group(2).count(MARKER) != 1:
        raise RuntimeError("V23 marker is not isolated inside exactly one content entry")
    entry = matching_entries[0]
    updated = config[:entry.start(2)] + html.escape(script, quote=False) + config[entry.end(2):]
    if updated.count(MARKER) != 1:
        raise RuntimeError("updated configuration violates the unique V23 marker contract")
    original = os.stat(config_path)
    directory = os.path.dirname(config_path)
    fd, temporary = tempfile.mkstemp(prefix=".danmu-customcssjs-r2-", suffix=".tmp", dir=directory)
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

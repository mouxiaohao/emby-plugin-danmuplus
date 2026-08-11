#!/usr/bin/env python3
"""Atomically replace only the deployed Danmu smart-match CustomCssJS content."""

import html
import os
import sys


OLD_MARKER = "__embyDanmuSmartMenuV15"
NEW_MARKER = "__embyDanmuSmartMenuV17"


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: update_customcssjs.py CONFIG_XML SMART_MATCH_JS")

    config_path, script_path = sys.argv[1:]
    with open(config_path, "r", encoding="utf-8") as stream:
        config = stream.read()
    with open(script_path, "r", encoding="utf-8-sig") as stream:
        script = stream.read()

    if config.count(OLD_MARKER) != 1:
        raise RuntimeError("expected exactly one deployed V15 smart-match entry")
    if script.count(NEW_MARKER) != 1 or OLD_MARKER in script:
        raise RuntimeError("candidate script marker contract is invalid")

    marker = config.index(OLD_MARKER)
    content_start_tag = config.rfind("<content>", 0, marker)
    content_end = config.find("</content>", marker)
    if content_start_tag < 0 or content_end < 0:
        raise RuntimeError("unable to locate the smart-match content boundaries")
    content_start = content_start_tag + len("<content>")

    replacement = html.escape(script, quote=False)
    updated = config[:content_start] + replacement + config[content_end:]
    if updated.count(NEW_MARKER) != 1 or OLD_MARKER in updated:
        raise RuntimeError("updated smart-match marker contract is invalid")

    temporary_path = config_path + ".2.0.3r1-new"
    try:
        with open(temporary_path, "w", encoding="utf-8", newline="") as stream:
            stream.write(updated)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, config_path)
    finally:
        if os.path.exists(temporary_path):
            os.unlink(temporary_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

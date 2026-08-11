#!/usr/bin/env python3
"""Replace only the Danmu smart-match CustomCssJS entry in Emby's XML config."""

import html
import os
import sys


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("usage: update_customcssjs.py CONFIG_XML SMART_MATCH_JS")

    config_path, script_path = sys.argv[1:]
    with open(config_path, "r", encoding="utf-8") as stream:
        config = stream.read()
    with open(script_path, "r", encoding="utf-8-sig") as stream:
        script = stream.read()

    name = "<name>电视剧/季/集/电影智能匹配下载弹幕</name>"
    if config.count(name) != 1:
        raise RuntimeError("smart-match CustomCssJS entry is missing or duplicated")
    if config.count("__embyDanmuSmartMenuV13") != 1:
        raise RuntimeError("expected exactly one deployed V13 marker")
    if "__embyDanmuSmartMenuV14" not in script:
        raise RuntimeError("candidate script does not contain V14 marker")

    item_start = config.index(name)
    content_start = config.index("<content>", item_start) + len("<content>")
    content_end = config.index("</content>", content_start)
    replacement = html.escape(script, quote=False)
    updated = config[:content_start] + replacement + config[content_end:]
    if updated.count("__embyDanmuSmartMenuV14") != 1 or "__embyDanmuSmartMenuV13" in updated:
        raise RuntimeError("updated marker contract is invalid")

    temporary_path = config_path + ".r3-new"
    with open(temporary_path, "w", encoding="utf-8", newline="") as stream:
        stream.write(updated)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary_path, config_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

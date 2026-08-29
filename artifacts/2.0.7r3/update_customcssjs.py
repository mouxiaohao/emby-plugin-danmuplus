#!/usr/bin/env python3
"""Create a verified XML copy with only the enabled Smart Match content replaced."""

from __future__ import annotations

import hashlib
import html
import json
import os
import re
import sys
import tempfile
import xml.etree.ElementTree as ET


TARGET_NAME = "电视剧/季/集/电影智能匹配下载弹幕"
TARGET_STATE = "forced_on"
OLD_MARKER = "__embyDanmuSmartMenuV35"
NEW_MARKER = "__embyDanmuSmartMenuV36"
CUSTOM_PATTERN = re.compile(r"<Custom\b[^>]*>.*?</Custom>", re.DOTALL)
CONTENT_PATTERN = re.compile(r"(<content\b[^>]*>)(.*?)(</content>)", re.DOTALL)


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def normalized_newlines(value: str) -> str:
    return value.replace("\r\n", "\n").replace("\r", "\n")


def custom_identity(block: str) -> tuple[str, str]:
    element = ET.fromstring(block)
    return element.findtext("name") or "", element.findtext("state") or ""


def main() -> int:
    if len(sys.argv) != 4:
        raise SystemExit("usage: update_customcssjs.py CONFIG_XML SMART_MATCH_JS OUTPUT_XML")

    config_path, script_path, output_path = map(os.path.abspath, sys.argv[1:])
    if os.path.normcase(config_path) == os.path.normcase(output_path):
        raise RuntimeError("output must be a distinct staged path")

    with open(config_path, "r", encoding="utf-8-sig", newline="") as stream:
        config = stream.read()
    with open(script_path, "r", encoding="utf-8-sig", newline="") as stream:
        script = stream.read()

    ET.fromstring(config)
    if script.count(NEW_MARKER) != 1 or script.count(OLD_MARKER) != 0:
        raise RuntimeError("candidate script must contain V36 exactly once and no V35 marker")

    matches = []
    for custom_match in CUSTOM_PATTERN.finditer(config):
        name, state = custom_identity(custom_match.group(0))
        if name == TARGET_NAME and state == TARGET_STATE:
            matches.append(custom_match)
    if len(matches) != 1:
        raise RuntimeError("expected exactly one enabled named Smart Match component")

    custom_match = matches[0]
    old_block = custom_match.group(0)
    content_matches = list(CONTENT_PATTERN.finditer(old_block))
    if len(content_matches) != 1:
        raise RuntimeError("target component must contain exactly one content element")
    content_match = content_matches[0]
    old_content_escaped = content_match.group(2)
    old_content = html.unescape(old_content_escaped)
    if old_content.count(OLD_MARKER) != 1 or old_content.count(NEW_MARKER) != 0:
        raise RuntimeError("deployed target must contain V35 exactly once and no V36 marker")

    new_content_escaped = html.escape(script, quote=False)
    new_block = (
        old_block[: content_match.start(2)]
        + new_content_escaped
        + old_block[content_match.end(2) :]
    )
    updated = config[: custom_match.start()] + new_block + config[custom_match.end() :]

    parsed = ET.fromstring(updated)
    deployed_targets = [
        node
        for node in parsed.findall(".//Custom")
        if (node.findtext("name") or "") == TARGET_NAME
        and (node.findtext("state") or "") == TARGET_STATE
    ]
    if len(deployed_targets) != 1:
        raise RuntimeError("staged XML lost the unique enabled target")
    staged_content = deployed_targets[0].findtext("content") or ""
    if normalized_newlines(staged_content) != normalized_newlines(script):
        raise RuntimeError("staged target content does not decode to the candidate script")

    restored = updated[: custom_match.start()] + old_block + updated[custom_match.start() + len(new_block) :]
    if restored != config:
        raise RuntimeError("bytes outside the target content were modified")

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(
        prefix=".danmu-customcssjs-r3-", suffix=".tmp", dir=os.path.dirname(output_path)
    )
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="") as stream:
            stream.write(updated)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, output_path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)

    evidence = {
        "config_before_sha256": sha256_text(config),
        "config_after_sha256": sha256_text(updated),
        "target_before_length": len(old_content.encode("utf-8")),
        "target_before_sha256": sha256_text(old_content),
        "target_after_length": len(script.encode("utf-8")),
        "target_after_sha256": sha256_text(script),
        "new_marker_count": staged_content.count(NEW_MARKER),
        "old_marker_count": staged_content.count(OLD_MARKER),
        "outside_target_bytes_unchanged": True,
    }
    print(json.dumps(evidence, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

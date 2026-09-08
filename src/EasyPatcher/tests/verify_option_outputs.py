#!/usr/bin/env python3
from __future__ import annotations

import base64
import gzip
import hashlib
import json
import struct
from pathlib import Path

PATCH_ROOT = Path(__file__).resolve().parents[3]
PATCH_JSON = PATCH_ROOT / "berd" / "system.json"
ORIGINAL_ROOT = Path("/mnt/d/Program Files (x86)/Steam/steamapps/common/SG_My Darling's Embrace/USRDIR.bak")
CASE_ROOT = Path("/mnt/d/HermesAgent/tmp/sg-easypatcher-options")
TARGETS = ("FONT.DDS", "FONT2.DDS", "DATA01.DDS")


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def parse_mpk(path: Path) -> dict[str, bytes]:
    data = path.read_bytes()
    if len(data) < 12 or data[:4] != b"MPK\0":
        raise AssertionError(f"MPK header mismatch: {path}")
    count = struct.unpack_from("<i", data, 8)[0]
    entries: dict[str, bytes] = {}
    for expected_index in range(count):
        pos = 12 + expected_index * 256
        index = struct.unpack_from("<i", data, pos + 56)[0]
        offset, size1, size2 = struct.unpack_from("<qqq", data, pos + 60)
        if index != expected_index or size1 != size2 or offset < 0 or size1 < 0 or offset + size1 > len(data):
            raise AssertionError(f"invalid MPK entry {expected_index}: {path}")
        name = data[pos + 84:pos + 256].split(b"\0", 1)[0].decode("utf-8")
        entries[name] = data[offset:offset + size1]
    return entries


def verify_system_case(
    case: str,
    original: dict[str, bytes],
    replacements: dict[str, bytes],
    expected_replacements: set[str],
) -> dict[str, str]:
    live = parse_mpk(CASE_ROOT / case / "USRDIR" / "system.mpk")
    if live.keys() != original.keys():
        raise AssertionError(f"{case}: system.mpk entry set changed")
    for name, source_blob in original.items():
        expected = replacements[name] if name in expected_replacements else source_blob
        if live[name] != expected:
            expected_kind = "patch payload" if name in expected_replacements else "original payload"
            raise AssertionError(f"{case}: {name} differs from expected {expected_kind}")
    return {name: sha(live[name]) for name in TARGETS}


def main() -> None:
    patch = json.loads(PATCH_JSON.read_text(encoding="utf-8"))
    replacements = {
        name: gzip.decompress(base64.b64decode(patch["data"][name]))
        for name in TARGETS
    }
    original_system = parse_mpk(ORIGINAL_ROOT / "system.mpk")
    original_script_bytes = (ORIGINAL_ROOT / "script.mpk").read_bytes()

    expected = {
        "translation": {"FONT.DDS", "FONT2.DDS"},
        "ui": {"DATA01.DDS"},
        "combined": set(TARGETS),
    }
    system_hashes = {
        case: verify_system_case(case, original_system, replacements, selected)
        for case, selected in expected.items()
    }

    script_bytes = {
        case: (CASE_ROOT / case / "USRDIR" / "script.mpk").read_bytes()
        for case in expected
    }
    if script_bytes["ui"] != original_script_bytes:
        raise AssertionError("ui: script.mpk changed even though translation was disabled")
    if script_bytes["translation"] == original_script_bytes:
        raise AssertionError("translation: script.mpk did not receive SCX patches")
    if script_bytes["combined"] != script_bytes["translation"]:
        raise AssertionError("combined: script.mpk differs from translation-only output")

    report = {
        "original_script_sha256": sha(original_script_bytes),
        "translation_script_sha256": sha(script_bytes["translation"]),
        "ui_script_sha256": sha(script_bytes["ui"]),
        "combined_script_sha256": sha(script_bytes["combined"]),
        "system_entry_sha256": system_hashes,
        "OPTION_OUTPUTS_VERIFY": "PASS",
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

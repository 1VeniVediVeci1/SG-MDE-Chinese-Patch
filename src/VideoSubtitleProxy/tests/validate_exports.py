#!/usr/bin/env python3
"""Compare a built proxy's 73 named exports and ordinals with the supplied RAD DLL."""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path


def exports(path: Path) -> list[tuple[int, str]]:
    text = subprocess.check_output(["objdump", "-p", str(path)], text=True, encoding="utf-8")
    section = text.split("[Ordinal/Name Pointer] Table", 1)[1].split("PE File Base Relocations", 1)[0]
    result = []
    for ordinal, name in re.findall(r"\[\s*(\d+)\]\s+([^\s]+)", section):
        result.append((int(ordinal) + 1, name))
    return result


def main() -> int:
    original, proxy = map(Path, sys.argv[1:3])
    expected = exports(original)
    actual = exports(proxy)
    if expected != actual:
        print(f"export mismatch expected={len(expected)} actual={len(actual)}")
        return 1
    if len(actual) != 73:
        print(f"unexpected export count: {len(actual)}")
        return 1
    print(f"exports_ok count={len(actual)} first={actual[0]} last={actual[-1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

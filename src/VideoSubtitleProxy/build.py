#!/usr/bin/env python3
"""Build and stage the x86 Bink subtitle proxy without any game DLL."""
from __future__ import annotations

import argparse
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = ROOT.parents[1]
RUNTIME_DLLS = (
    "libass-5.dll",
    "libbz2-1.dll",
    "libexpat-1.dll",
    "libfontconfig-1.dll",
    "libfreetype-6.dll",
    "libfribidi-0.dll",
    "libgcc_s_dw2-1.dll",
    "libglib-2.0-0.dll",
    "libgraphite2.dll",
    "libharfbuzz-0.dll",
    "libiconv-2.dll",
    "libintl-8.dll",
    "libpcre-1.dll",
    "libpng16-16.dll",
    "libstdc++-6.dll",
    "libwinpthread-1.dll",
    "zlib1.dll",
)


def copy_required(source: Path, destination: Path) -> None:
    if not source.is_file():
        raise SystemExit(f"Missing required file: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.resolve() == destination.resolve():
        return
    shutil.copy2(source, destination)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--deps-dir", type=Path, required=True)
    parser.add_argument("--font", type=Path, required=True)
    parser.add_argument("--subtitle-dir", type=Path, required=True)
    parser.add_argument("--compiler", default="i686-w64-mingw32-g++")
    parser.add_argument("--output", type=Path,
                        default=REPOSITORY_ROOT / "berd" / "video-subtitles")
    parser.add_argument("--import-lib", type=Path,
                        default=ROOT / "build" / "bink2w32.dll.a")
    args = parser.parse_args()

    output = args.output.resolve()
    args.import_lib.parent.mkdir(parents=True, exist_ok=True)
    command = [
        args.compiler, "-std=c++17", "-O2", "-Wall", "-Wextra", "-Werror", "-shared",
        str(ROOT / "bink2w32_proxy.cpp"), str(ROOT / "bink2w32.def"),
        "-o", str(output / "bink2w32.dll"),
        "-static", "-static-libgcc", "-static-libstdc++",
        f"-Wl,--out-implib,{args.import_lib}",
    ]
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(command, check=True)
    runtime = output / "runtime"
    copy_required(args.subtitle_dir / "OP01.zh.ass", runtime / "OP01.zh.ass")
    copy_required(args.subtitle_dir / "prologue01.zh.ass", runtime / "prologue01.zh.ass")
    copy_required(args.font, runtime / "fonts" / "NotoSansCJK-Regular.ttc")
    for dll in RUNTIME_DLLS:
        copy_required(args.deps_dir / dll, runtime / "lib" / dll)
    for obsolete in (output / "dinput8.dll", output / "d3dx9_43.dll"):
        obsolete.unlink(missing_ok=True)
    print(output)


if __name__ == "__main__":
    main()

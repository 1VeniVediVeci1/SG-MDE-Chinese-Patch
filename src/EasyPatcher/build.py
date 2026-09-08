#!/usr/bin/env python3
from __future__ import annotations

import os
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MAGES_ROOT = ROOT.parent / "MagesLib"
REPOSITORY_ROOT = ROOT.parents[1]
REFERENCE_PACKAGE = "microsoft.netframework.referenceassemblies.net45"


def version_key(value: str) -> tuple[int, ...]:
    return tuple(int(part) for part in re.findall(r"\d+", value))


def newest_csc() -> Path:
    result = subprocess.run(
        ["dotnet", "--list-sdks"], check=True, text=True, capture_output=True
    )
    candidates = []
    for line in result.stdout.splitlines():
        match = re.fullmatch(r"(\S+) \[(.+)]", line.strip())
        if match:
            version, sdk_root = match.groups()
            csc = Path(sdk_root) / version / "Roslyn" / "bincore" / "csc.dll"
            if csc.is_file():
                candidates.append((version_key(version), csc))
    if not candidates:
        raise SystemExit("Roslyn csc.dll was not found in any SDK reported by dotnet --list-sdks")
    return max(candidates)[1]


def net45_reference_root() -> Path:
    package_cache = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
    package_root = package_cache / REFERENCE_PACKAGE
    candidates = [
        (version_key(version.name), version / "build/.NETFramework/v4.5")
        for version in package_root.iterdir()
        if version.is_dir() and (version / "build/.NETFramework/v4.5/mscorlib.dll").is_file()
    ] if package_root.is_dir() else []
    if not candidates:
        raise SystemExit(
            "NET Framework 4.5 reference assemblies are not in the NuGet cache.\n"
            "Run: dotnet restore src/EasyPatcher/EasyPatcher.csproj"
        )
    return max(candidates)[1]


def source_files(directory: Path) -> list[Path]:
    return sorted(
        path
        for path in directory.rglob("*.cs")
        if "bin" not in path.parts and "obj" not in path.parts and "tests" not in path.parts
    )


def compile_assembly(
    csc: Path,
    output: Path,
    sources: list[Path],
    references: list[Path],
    target: str,
) -> None:
    missing = [str(path) for path in references if not path.is_file()]
    if missing:
        raise SystemExit("Missing references:\n" + "\n".join(missing))
    command = [
        "dotnet",
        str(csc),
        "/noconfig",
        "/nostdlib+",
        f"/target:{target}",
        "/platform:anycpu32bitpreferred" if target == "winexe" else "/platform:anycpu",
        "/langversion:latest",
        "/optimize+",
        "/deterministic+",
        "/utf8output",
        f"/out:{output}",
        *[f"/reference:{path}" for path in references],
        *[str(path) for path in sources],
    ]
    subprocess.run(command, check=True)


def main() -> None:
    csc = newest_csc()
    reference_root = net45_reference_root()
    framework = lambda *names: [reference_root / name for name in names]

    mages_output_dir = MAGES_ROOT / "bin/Release"
    mages_output_dir.mkdir(parents=True, exist_ok=True)
    mages_output = mages_output_dir / "MagesLib.dll"
    compile_assembly(
        csc,
        mages_output,
        source_files(MAGES_ROOT),
        framework("mscorlib.dll", "System.dll", "System.Core.dll"),
        "library",
    )

    output_dir = ROOT / "bin/Release"
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / "EasyPatcher.exe"
    compile_assembly(
        csc,
        output,
        source_files(ROOT),
        framework(
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Drawing.dll",
            "System.Windows.Forms.dll",
            "System.Configuration.dll",
            "System.IO.Compression.dll",
            "System.IO.Compression.FileSystem.dll",
            "Microsoft.CSharp.dll",
        )
        + [mages_output, REPOSITORY_ROOT / "fastJSON.dll"],
        "winexe",
    )

    shutil.copy2(ROOT / "app.config", output_dir / "EasyPatcher.exe.config")
    shutil.copy2(mages_output, output_dir / "MagesLib.dll")
    shutil.copy2(REPOSITORY_ROOT / "fastJSON.dll", output_dir / "fastJSON.dll")
    print(output)


if __name__ == "__main__":
    main()

# 构建

## 前提

- Python 3（仅构建脚本需要，运行补丁器不需要）。
- 已安装 .NET SDK，`dotnet --list-sdks` 能列出至少一个 SDK。
- NuGet 缓存中有 `Microsoft.NETFramework.ReferenceAssemblies.net45`。若缓存缺失，执行：

```bash
dotnet restore src/EasyPatcher/EasyPatcher.csproj
```

## 命令

```bash
python3 src/EasyPatcher/build.py
```

脚本使用 SDK 自带 Roslyn 编译器，先从 `src/MagesLib` 源码生成 `MagesLib.dll`，再生成 EasyPatcher。

## 输出

`src/EasyPatcher/bin/Release/` 包含：

- `EasyPatcher.exe`
- `EasyPatcher.exe.config`
- `MagesLib.dll`
- `fastJSON.dll`

将这四个文件与项目根目录的完整 `berd` 文件夹复制到同一个新目录，即可在 Windows 中运行 `EasyPatcher.exe`。请保留许可证和使用说明；不要只复制 EXE。构建不会覆盖项目根目录的已安装补丁器或修改补丁数据。

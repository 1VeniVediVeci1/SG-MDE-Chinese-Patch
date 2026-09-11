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

将这四个文件与项目根目录的完整 `berd` 文件夹复制到同一个新目录，即可在 Windows 中运行 `EasyPatcher.exe`。其中 `berd/video-subtitles/` 必须保留完整：它包含 `bink2w32.dll` 字幕代理、`runtime/lib` 的渲染依赖、私有字体和 ASS 文件。该目录不应包含用户游戏的原始 `bink2w32.dll`。

补丁器首次安装时从用户游戏目录备份经校验的原始解码器为 `bink2w32_original.dll`，移除外置字幕时恢复它；原始 `.bk2` 视频不参与构建或安装复制。构建不会覆盖项目根目录的已安装补丁器或修改补丁数据。

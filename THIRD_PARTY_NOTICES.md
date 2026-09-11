# 第三方声明

## MagesTools / EasyPatcher / MagesLib

- 原作者：FENGberd
- 上游：https://github.com/fengberd/MagesTools
- 许可证：MIT，原文保留在本项目根目录 `LICENSE`。
- 本项目的 EasyPatcher 是在上游补丁器基础上恢复并修改的版本，保留 MPK、SCX 与 `berd` 补丁格式及原有备份流程。
- `src/MagesLib` 的 C# 源码取自上游提交 [`3b73030ab6046fca64aa3850682e7c5d82d34bdf`](https://github.com/fengberd/MagesTools/commit/3b73030ab6046fca64aa3850682e7c5d82d34bdf)；工程文件适配当前构建方式。
- 本项目修改包括版本显示、一键应用入口，以及配套的汉化文本和界面调整。不得将上游工具代码描述为本项目原创。

## fastJSON

- 作者：Mehdi Gholam
- 项目：https://github.com/mgholam/fastJSON
- 随附 `fastJSON.dll` 与上述 MagesTools 提交中的 `lib/fastJSON.dll` 相同。
- 许可证原文见 `licenses/fastJSON-LICENSE.txt`。

## 游戏内容与字体

游戏本体、角色、图像、文字、截图及商标归原权利方所有。本项目不包含游戏可执行文件、音视频或存档，使用补丁需要已有的正版游戏安装。

本版本的部分字库构建参考并使用了 STEINS;GATE RE:BOOT 中的 Hiragino 字体资源，界面图片亦包含游戏原有内容。这些内容不属于 MagesTools 的 MIT 授权范围；本项目不声称拥有或有权重新许可它们。README 和对比页的游戏截图仅用于展示补丁效果。

## 视频字幕运行时（新增）

`berd/video-subtitles/runtime/lib/` 中的 17 个 x86 DLL 与
[drdaxxy/bink2-libass v1.0](https://github.com/drdaxxy/bink2-libass/releases/tag/v1.0)
发布资产逐字节一致。该项目的代理代码为 WTFPL-2.0，但随附 DLL 分别受 ISC、MIT、
FreeType、LGPL、MPL/GPL、BSD、zlib、bzip2 和 GCC Runtime Library Exception 等许可约束；
不得将整个运行时依赖包描述为 MIT 或 WTFPL。逐文件组件识别、已核实来源、许可证原文及 LGPL
源码交付条件见 [`licenses/VIDEO_SUBTITLES_DEPENDENCIES.md`](licenses/VIDEO_SUBTITLES_DEPENDENCIES.md)。

`berd/video-subtitles/runtime/fonts/NotoSansCJK-Regular.ttc` 是
[Noto CJK](https://github.com/notofonts/noto-cjk) 字体，采用 SIL Open Font License 1.1；
许可证原文同样随仓库保留。该字体只用于字幕运行时，不改变本项目既有游戏字体说明。

LGPL 组件的精确版本、对应源码、SHA-256 与 MSYS2 构建配方提交见
[`licenses/VIDEO_SUBTITLES_DEPENDENCIES.md`](licenses/VIDEO_SUBTITLES_DEPENDENCIES.md)。

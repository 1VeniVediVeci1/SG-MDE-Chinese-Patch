# 视频字幕运行时：第三方依赖与来源

本文件只覆盖 `berd/video-subtitles/runtime/lib/` 的 17 个 x86 DLL 以及
`berd/video-subtitles/runtime/fonts/NotoSansCJK-Regular.ttc`。不改变或重新许可
游戏、视频、既有汉化内容或其他项目组件。

## 已核实的二进制来源

这 17 个 DLL 与上游 **drdaxxy/bink2-libass v1.0** 发布资产逐字节一致。发布资产：

- https://github.com/drdaxxy/bink2-libass/releases/tag/v1.0
- https://github.com/drdaxxy/bink2-libass/releases/download/v1.0/bink2-libass.zip
- 发布资产 SHA-256：`d9b9ce72a2965aab3af72d3cd36c07572f6a24875ef81e3a391d0f8789907d71`
- `libass-5.dll` SHA-256：`0367e065ab19f06c76b346f1690fd3d7aedb43a59ae1e1f1a9938a1200461e06`

该上游项目自身使用 WTFPL-2.0；其许可原文在
[`bink2-libass-WTFPL-2.0.txt`](bink2-libass-WTFPL-2.0.txt)。这项许可**不**覆盖
随附的 libass/MinGW 依赖 DLL；不得将整包描述为 MIT 或 WTFPL。

字幕使用 Noto CJK 字体：

- 上游：https://github.com/notofonts/noto-cjk
- 本次文件：`NotoSansCJK-Regular.ttc`，SHA-256
  `b76b0433203017ca80401b2ee0dd69350349871c4b19d504c34dbdd80541690a`
- 许可证：SIL Open Font License 1.1，原文见 [`OFL-1.1.txt`](OFL-1.1.txt)，版权声明见 `Noto-CJK-copyright.txt`。

## DLL 识别与许可证

版本来自上游 v1.0 发布资产、DLL 导出/API 实测及其 2016-06-01 前的 MSYS2
`MINGW-packages` 构建配方。仅对 LGPL 组件列出对应源码和配方；宽松许可证组件仍保留许可原文。

|文件|组件/版本|许可证与随附原文|
|---|---|---|
|`libass-5.dll`|libass 0.13.2|ISC；`ISC.txt`、`libass-copyright-notices.txt`|
|`libfontconfig-1.dll`|fontconfig 2.11.95|MIT 风格；`Fontconfig-COPYING.txt`|
|`libfreetype-6.dll`|FreeType（版本未由此发布资产确定）|FreeType License / GPLv2；`FreeType-License.txt`|
|`libfribidi-0.dll`|FriBidi 0.19.7|LGPL-2.1-or-later；`LGPL-2.1.txt`|
|`libglib-2.0-0.dll`|GLib 2.48.1|LGPL-2.1-or-later；`LGPL-2.1.txt`|
|`libgraphite2.dll`|Graphite2 1.3.8|LGPL-2.1-or-later / MPL-2.0 / GPL-2.0-or-later；按 LGPL 路径处理，`LGPL-2.1.txt`|
|`libharfbuzz-0.dll`|HarfBuzz 1.2.7|MIT；`HarfBuzz-MIT.txt`|
|`libiconv-2.dll`|GNU libiconv 1.14|LGPL-2.0-or-later；`LGPL-2.1.txt`|
|`libintl-8.dll`|GNU gettext runtime 0.19.7|LGPL-2.1-or-later；`LGPL-2.1.txt`|
|`libpcre-1.dll`|PCRE 8.38|BSD-3-Clause；`PCRE-LICENCE.txt`|
|`libpng16-16.dll`|libpng 1.6.21|libpng 许可证；`libpng-2.0.txt`|
|`libexpat-1.dll`|Expat 2.1.1|MIT；`Expat-COPYING.txt`|
|`libbz2-1.dll`|bzip2（版本未由此发布资产确定）|bzip2；`bzip2-License.txt`|
|`zlib1.dll`|zlib 1.2.8|zlib；`zlib-License.txt`|
|`libgcc_s_dw2-1.dll`|GCC runtime（版本未由此发布资产确定）|GPL-3.0-with-GCC-exception；`GPL-3.0.txt`、`GCC-Runtime-Library-Exception-3.1.txt`|
|`libstdc++-6.dll`|libstdc++ runtime（版本未由此发布资产确定）|GPL-3.0-with-GCC-exception；同上|
|`libwinpthread-1.dll`|MinGW-w64 winpthreads（版本未由此发布资产确定）|MIT 与 BSD-3-Clause-Clear；`MinGW-winpthreads-COPYING.txt`|

## LGPL 对应源码

对应源码附包：[SG-MDE-Video-Subtitle-Dependency-Sources-v1.2-r4.zip](https://github.com/1VeniVediVeci1/SG-MDE-Chinese-Patch/releases/download/v1.2-r4/SG-MDE-Video-Subtitle-Dependency-Sources-v1.2-r4.zip)。
SHA-256：`b567f5dfe377e3a23ed756456c1e2709b925ba1c1fe78b324e7f4382ed728c22`。此附包仅供源码与许可查阅，安装汉化不需要下载。

下表给出与该 2016 MinGW/MSYS2 二进制组对应的上游源码、SHA-256 和构建配方提交；配方所列
补丁是对应源码的一部分。对应源码组合包括这些源码包及该提交中的相关补丁/`PKGBUILD`。

|组件|源码包（SHA-256）|MSYS2 `MINGW-packages` 配方提交|
|---|---|---|
|FriBidi 0.19.7|https://distfiles.macports.org/fribidi/fribidi-0.19.7.tar.bz2（官方源包镜像，SHA-256 `08222a6212bbc2276a2d55c3bf370109ae4a35b689acbc66571ad2a670595a8e`）|`e32a40cc9256aa10c4fd0561a446e5ebb399f6b8`|
|GLib 2.48.1|https://download.gnome.org/sources/glib/2.48/glib-2.48.1.tar.xz (`74411bff489cb2a3527bac743a51018841a56a4d896cc1e0d0d54f8166a14612`)|`9914be4f9813e8286d3fdccc8864b825d12eaef2`|
|Graphite2 1.3.8|https://github.com/silnrsi/graphite/releases/download/1.3.8/graphite2-1.3.8.tgz (`9f3f25b3a8495ce0782e77f69075c0dd9b7c054847b9bf9ff130bec38f4c8cc2`)|`63d1f6e6f75b3763e3b21e0239cd31d999ee2272`|
|GNU libiconv 1.14|https://ftp.gnu.org/pub/gnu/libiconv/libiconv-1.14.tar.gz (`72b24ded17d687193c3366d0ebe7cde1e6b18f0df8c55438ac95be39e8a30613`)|`7c803d2668b24e520fada3e81d125c9d4d3e56b6`|
|GNU gettext 0.19.7（`libintl-8.dll`）|https://ftp.gnu.org/pub/gnu/gettext/gettext-0.19.7.tar.gz (`5386d2a40500295783c6a52121adcf42a25519e2d23675950619c9e69558c23f`)|`879759d629a30e6a94dcd5dbde04aea592e9a335`|

## 不在本声明范围内

`berd/video-subtitles/bink2w32.dll` 是本项目的代理构建产物；ASS 字幕文件是本项目内容。
原始 RAD Bink DLL、游戏视频及游戏资源均未随此运行时依赖组重新分发。

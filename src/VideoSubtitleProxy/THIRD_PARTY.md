# Third-party provenance

`bink2w32_proxy.cpp` uses the Bink/libass interception design described by
[drdaxxy/bink2-libass](https://github.com/drdaxxy/bink2-libass). That reference is
licensed under WTFPL-2.0; its complete notice is in `LICENSE.bink2-libass.txt`.

The x86 renderer DLLs in `runtime/lib` originate from the upstream
[bink2-libass v1.0 release](https://github.com/drdaxxy/bink2-libass/releases/tag/v1.0).
They retain their individual upstream licenses; the reference proxy's license
does not relicense these libraries. This project's proxy is built from the
source in this directory, not copied from the upstream binary package.

The runtime font is Noto Sans CJK Regular under the SIL Open Font License 1.1.
See [`../../licenses/VIDEO_SUBTITLES_DEPENDENCIES.md`](../../licenses/VIDEO_SUBTITLES_DEPENDENCIES.md)
and [`../../THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) for dependency
sources and the license texts that accompany binary distributions.

No original RAD/Bink decoder or game video is distributed here.

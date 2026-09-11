# Video Subtitle Proxy Build

`bink2w32.dll` is an x86 Bink proxy for SG MDE. It exposes the original 73
named exports with their original ordinals. Only `_BinkOpen@8`, `_BinkClose@4`,
and `_BinkCopyToBuffer@28` are wrapped; the other exports forward to
`bink2w32_original.dll` unchanged, including Bink audio APIs.

At runtime the three wrapped functions load `bink2w32_original.dll` by its
explicit game-root path. libass is loaded only from `sgmde-video-subs/lib`
with `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32`; the proxy
does not alter global DLL search configuration. A missing libass dependency,
font, or ASS file is safe pass-through to the original decoder.

Only `movie/**/OP01.bk2` and `movie/**/prologue01.bk2` get tracks. The wrapper
uses the Bink handle's one-based frame number and exact `FrameRate/FrameRateDiv`
values. It composites `BINKSURFACE32` (`3`, BGRA32) and `BINKSURFACE32A`
(`5`, BGRA32 with alpha, used by the game), preserving the existing alpha channel
on surface `5` and using opaque padding on surface `3`;
other surface values are logged once per Bink handle and left unmodified.

Build the payload from the verified x86 dependency release and Noto font:

```bash
python3 src/VideoSubtitleProxy/build.py \
  --deps-dir /path/to/bink2-libass-release \
  --subtitle-dir /path/to/final-ass-files \
  --font /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc
```

The distribution payload is `berd/video-subtitles/bink2w32.dll` plus
`berd/video-subtitles/runtime/**`. It deliberately excludes the original RAD
DLL and every BK2 video. Installation places the proxy at game root, preserves
the user's original as `bink2w32_original.dll`, and places `runtime/**` at
`gameRoot/sgmde-video-subs/**` (the contents of the payload's `runtime/` directory).

Set `SGMDE_VIDEO_SUBS_LOG=1` before launch for a bounded (64 KiB) diagnostic
log at `sgmde-video-subs/sgmde-video-subs.log`. It records initialization,
open/close, and visibility transitions, not unbounded per-frame output.
The variable must reach the actual game process; a Steam relaunch may discard
the launcher's environment. After diagnostics, close the game and archive the
generated log outside the managed runtime before reinstalling or removing it.

Run the model test with:

```bash
g++ -std=c++17 -Wall -Wextra -Werror -Isrc/VideoSubtitleProxy \
  src/VideoSubtitleProxy/tests/model_test.cpp -o /tmp/sgmde-model-test
/tmp/sgmde-model-test
```

`tests/windows_bink_proxy_smoke.cpp` is the x86 Windows real-decoder harness:
it decodes twice in one process so close/reopen session cleanup is exercised.
Arguments are `<movie> <one-based-frame> <first.bmp> <reopen.bmp> [surface]`;
the optional surface defaults to `3`. Use `5` to exercise the game's alpha format.
`tests/validate_exports.py` compares the completed proxy table to a supplied
original DLL and requires all 73 name/ordinal pairs to match.

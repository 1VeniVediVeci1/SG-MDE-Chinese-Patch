#include "subtitle_model.h"

#include <cassert>
#include <cstring>

int main() {
  assert(sgmde::SelectSubtitle("C:\\game\\USRDIR\\movie\\1920x1080\\OP01.bk2") ==
         sgmde::SubtitleKind::kOpening);
  assert(sgmde::SelectSubtitle("movie/prologue01.bk2") ==
         sgmde::SubtitleKind::kPrologue);
  assert(sgmde::SelectSubtitle("movie/1280x720/OP01.bk2") ==
         sgmde::SubtitleKind::kOpening);
  assert(sgmde::SelectSubtitle("movie/OP010.bk2") == sgmde::SubtitleKind::kNone);
  assert(sgmde::SelectSubtitle("movie/ending01.bk2") == sgmde::SubtitleKind::kNone);
  assert(sgmde::SelectSubtitle("other/OP01.bk2") == sgmde::SubtitleKind::kNone);
  assert(sgmde::FrameToMilliseconds(0, 30, 1) == 0);
  assert(sgmde::FrameToMilliseconds(30, 30, 1) == 1000);
  assert(sgmde::FrameToMilliseconds(30, 30000, 1001) == 1001);
  assert(sgmde::FrameToMilliseconds(97, 5000000, 166833) == 3236);
  assert(sgmde::FrameToMilliseconds(68, 10000000, 333333) == 2266);
  assert(sgmde::FrameToMilliseconds(1, 0, 1) == 0);
  assert(sgmde::ScaleCoordinate(960, 1920, 1920) == 960);
  assert(sgmde::ScaleCoordinate(960, 1280, 1920) == 640);
  assert(sgmde::ScaleCoordinate(1040, 720, 1080) == 693);

  unsigned char translucent_surface[] = {20, 40, 60, 96};
  sgmde::BlendAssBgraPixel(translucent_surface, 0xC86432FFu, 128, true);
  assert(translucent_surface[0] == 20 && translucent_surface[1] == 40 &&
         translucent_surface[2] == 60 && translucent_surface[3] == 96);

  unsigned char composed_surface[] = {20, 40, 60, 96};
  sgmde::BlendAssBgraPixel(composed_surface, 0xC8643200u, 128, true);
  assert(composed_surface[0] == 35 && composed_surface[1] == 70 &&
         composed_surface[2] == 130 && composed_surface[3] == 96);

  unsigned char opaque_surface[] = {20, 40, 60, 96};
  sgmde::BlendAssBgraPixel(opaque_surface, 0xC8643200u, 128, false);
  assert(opaque_surface[0] == 35 && opaque_surface[1] == 70 &&
         opaque_surface[2] == 130 && opaque_surface[3] == 255);

  unsigned char empty_bitmap[] = {20, 40, 60, 96};
  sgmde::BlendAssBgraPixel(empty_bitmap, 0xC8643200u, 0, true);
  assert(std::memcmp(empty_bitmap, "\x14\x28\x3c\x60", 4) == 0);
  return 0;
}

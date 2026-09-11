#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>

struct BinkPrefix {
  std::uint32_t width;
  std::uint32_t height;
  std::uint32_t frames;
  std::uint32_t frame_number;
  std::uint32_t last_frame_number;
  std::uint32_t frame_rate;
  std::uint32_t frame_rate_divisor;
};

extern "C" {
BinkPrefix* __stdcall BinkOpen(const char*, std::uint32_t) __asm__("__BinkOpen@8");
void __stdcall BinkClose(BinkPrefix*) __asm__("__BinkClose@4");
std::int32_t __stdcall BinkCopyToBuffer(BinkPrefix*, void*, std::int32_t, std::uint32_t,
                                        std::uint32_t, std::uint32_t, std::uint32_t)
    __asm__("__BinkCopyToBuffer@28");
void __stdcall BinkDoFrame(BinkPrefix*) __asm__("__BinkDoFrame@4");
void __stdcall BinkGoto(BinkPrefix*, std::uint32_t, std::uint32_t) __asm__("__BinkGoto@12");
void __stdcall BinkSetSoundOnOff(BinkPrefix*, std::uint32_t) __asm__("__BinkSetSoundOnOff@8");
}

constexpr std::uint32_t kBinkSurface32 = 3;

bool WriteBmp(const char* path, const BinkPrefix& info, const std::vector<unsigned char>& pixels) {
  FILE* file = std::fopen(path, "wb");
  if (file == nullptr) return false;
  BITMAPFILEHEADER header = {};
  BITMAPINFOHEADER info_header = {};
  const DWORD byte_count = static_cast<DWORD>(pixels.size());
  header.bfType = 0x4d42;
  header.bfOffBits = sizeof(header) + sizeof(info_header);
  header.bfSize = header.bfOffBits + byte_count;
  info_header.biSize = sizeof(info_header);
  info_header.biWidth = static_cast<LONG>(info.width);
  info_header.biHeight = -static_cast<LONG>(info.height);
  info_header.biPlanes = 1;
  info_header.biBitCount = 32;
  info_header.biCompression = BI_RGB;
  info_header.biSizeImage = byte_count;
  const bool ok = std::fwrite(&header, sizeof(header), 1, file) == 1 &&
                  std::fwrite(&info_header, sizeof(info_header), 1, file) == 1 &&
                  std::fwrite(pixels.data(), pixels.size(), 1, file) == 1;
  std::fclose(file);
  return ok;
}

bool Decode(const char* video, std::uint32_t one_based_frame, const char* output,
            BinkPrefix* captured, std::uint32_t surface) {
  BinkPrefix* bink = BinkOpen(video, 0);
  if (bink == nullptr || bink->width == 0 || bink->height == 0 || bink->frame_rate == 0 ||
      bink->frame_rate_divisor == 0) return false;
  BinkSetSoundOnOff(bink, 0);
  BinkGoto(bink, one_based_frame, 0);
  BinkDoFrame(bink);
  *captured = *bink;
  const std::int32_t pitch = static_cast<std::int32_t>(bink->width * 4);
  std::vector<unsigned char> pixels(static_cast<std::size_t>(pitch) * bink->height);
  const std::int32_t copied = BinkCopyToBuffer(bink, pixels.data(), pitch, bink->height, 0, 0,
                                                surface);
  BinkClose(bink);
  return copied == 0 && WriteBmp(output, *captured, pixels);
}

int main(int argc, char** argv) {
  if (argc != 5 && argc != 6) return 2;
  const std::uint32_t frame = static_cast<std::uint32_t>(std::strtoul(argv[2], nullptr, 10));
  const std::uint32_t surface = argc == 6
      ? static_cast<std::uint32_t>(std::strtoul(argv[5], nullptr, 10)) : kBinkSurface32;
  BinkPrefix first = {};
  BinkPrefix second = {};
  if (!Decode(argv[1], frame, argv[3], &first, surface) ||
      !Decode(argv[1], frame, argv[4], &second, surface)) return 10;
  if (first.width != second.width || first.height != second.height ||
      first.frame_rate != second.frame_rate || first.frame_rate_divisor != second.frame_rate_divisor)
    return 11;
  std::printf("width=%u height=%u frames=%u frameRate=%u frameRateDiv=%u frame=%u surface=%u\n",
              first.width, first.height, first.frames, first.frame_rate, first.frame_rate_divisor,
              frame, surface);
  return 0;
}

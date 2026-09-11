#pragma once

#include <cstdint>
#include <string>

namespace sgmde {

enum class SubtitleKind { kNone, kOpening, kPrologue };

inline SubtitleKind SelectSubtitle(const char* video_path) {
  if (video_path == nullptr) return SubtitleKind::kNone;
  std::string normalized(video_path);
  for (char& value : normalized) {
    if (value == '\\') value = '/';
    if (value >= 'A' && value <= 'Z') value = static_cast<char>(value - 'A' + 'a');
  }
  if (normalized.find("movie/") == std::string::npos) return SubtitleKind::kNone;
  std::string basename(normalized);
  const std::size_t slash = basename.find_last_of("\\/");
  if (slash != std::string::npos) basename.erase(0, slash + 1);
  if (basename == "op01.bk2") return SubtitleKind::kOpening;
  if (basename == "prologue01.bk2") return SubtitleKind::kPrologue;
  return SubtitleKind::kNone;
}

inline std::uint32_t FrameToMilliseconds(std::uint32_t frame, std::uint32_t rate,
                                         std::uint32_t rate_divisor) {
  if (rate == 0 || rate_divisor == 0) return 0;
  const std::uint64_t whole_seconds = frame / rate;
  const std::uint64_t remainder = frame % rate;
  const std::uint64_t whole_milliseconds = whole_seconds * rate_divisor * 1000;
  return static_cast<std::uint32_t>(whole_milliseconds + (remainder * rate_divisor * 1000) / rate);
}

inline int ScaleCoordinate(int coordinate, int target_extent, int source_extent) {
  if (source_extent == 0) return 0;
  return (coordinate * target_extent + source_extent / 2) / source_extent;
}

inline void BlendAssBgraPixel(unsigned char* target, std::uint32_t color,
                              unsigned char coverage, bool preserve_alpha) {
  const unsigned int red = (color >> 24) & 0xff;
  const unsigned int green = (color >> 16) & 0xff;
  const unsigned int blue = (color >> 8) & 0xff;
  const unsigned int opacity = 255 - (color & 0xff);
  const unsigned int alpha = (coverage * opacity + 127) / 255;
  if (!alpha) return;
  const unsigned int inverse = 255 - alpha;
  target[0] = static_cast<unsigned char>((blue * alpha + target[0] * inverse + 127) / 255);
  target[1] = static_cast<unsigned char>((green * alpha + target[1] * inverse + 127) / 255);
  target[2] = static_cast<unsigned char>((red * alpha + target[2] * inverse + 127) / 255);
  if (!preserve_alpha) target[3] = 0xff;
}

}  // namespace sgmde

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <map>
#include <string>
#include <vector>

#include "subtitle_model.h"

static_assert(sizeof(void*) == 4, "The Bink proxy is x86 only");

namespace {

// Documented BINK prefix, checked by the native smoke harness against real BK2s.
struct Bink {
  std::uint32_t width, height, frames, frame_number, last_frame_number;
  std::uint32_t frame_rate, frame_rate_divisor;
};
struct AssImage {
  std::int32_t width, height, stride;
  unsigned char* bitmap;
  std::uint32_t color;
  std::int32_t dst_x, dst_y;
  AssImage* next;
};
struct AssLibrary;
struct AssRenderer;
struct AssTrack;
using BinkOpenFn = Bink*(__stdcall*)(const char*, std::uint32_t);
using BinkCloseFn = void(__stdcall*)(Bink*);
using BinkCopyFn = std::int32_t(__stdcall*)(Bink*, void*, std::int32_t, std::uint32_t,
                                             std::uint32_t, std::uint32_t, std::uint32_t);
using AssLibraryInitFn = AssLibrary* (*)();
using AssLibraryDoneFn = void (*)(AssLibrary*);
using AssRendererInitFn = AssRenderer* (*)(AssLibrary*);
using AssRendererDoneFn = void (*)(AssRenderer*);
using AssReadMemoryFn = AssTrack* (*)(AssLibrary*, char*, std::size_t, char*);
using AssFreeTrackFn = void (*)(AssTrack*);
using AssSetFrameSizeFn = void (*)(AssRenderer*, int, int);
using AssSetFontsFn = void (*)(AssRenderer*, const char*, const char*, int, const char*, int);
using AssSetFontsDirFn = void (*)(AssLibrary*, const char*);
using AssAddFontFn = void (*)(AssLibrary*, char*, char*, int);
using AssRenderFrameFn = AssImage* (*)(AssRenderer*, AssTrack*, long long, int*);

constexpr int kAssFontProviderAutodetect = 0;
constexpr std::uint32_t kBinkSurface32 = 3;
constexpr std::uint32_t kBinkSurface32A = 5;
constexpr DWORD kLogLimitBytes = 64 * 1024;

template <typename T> T ExportAddress(FARPROC address) {
  union { FARPROC raw; T typed; } value = {};
  value.raw = address;
  return value.typed;
}
struct AssApi {
  HMODULE module = nullptr;
  AssLibrary* library = nullptr;
  AssRenderer* renderer = nullptr;
  AssLibraryInitFn library_init = nullptr;
  AssLibraryDoneFn library_done = nullptr;
  AssRendererInitFn renderer_init = nullptr;
  AssRendererDoneFn renderer_done = nullptr;
  AssReadMemoryFn read_memory = nullptr;
  AssFreeTrackFn free_track = nullptr;
  AssSetFrameSizeFn set_frame_size = nullptr;
  AssSetFontsFn set_fonts = nullptr;
  AssSetFontsDirFn set_fonts_dir = nullptr;
  AssAddFontFn add_font = nullptr;
  AssRenderFrameFn render_frame = nullptr;
};
struct Session {
  AssTrack* track = nullptr;
  sgmde::SubtitleKind kind = sgmde::SubtitleKind::kNone;
  bool rendered = false;
  bool visible = false;
  bool unsupported_logged = false;
};

AssApi g_ass;
std::map<Bink*, Session> g_sessions;
CRITICAL_SECTION g_lock;
HMODULE g_original_module = nullptr;
BinkOpenFn g_bink_open = nullptr;
BinkCloseFn g_bink_close = nullptr;
BinkCopyFn g_bink_copy = nullptr;
bool g_ass_failed = false;
bool g_logging_enabled = false;
std::wstring g_game_root, g_runtime_root;
std::vector<unsigned char> g_font_bytes;
std::string g_font_directory_utf8;

std::wstring Join(const std::wstring& left, const wchar_t* right) {
  return left.empty() ? std::wstring(right) : left + L"\\" + right;
}
std::wstring ModuleDirectory(HMODULE module) {
  wchar_t path[MAX_PATH] = {};
  const DWORD length = GetModuleFileNameW(module, path, MAX_PATH);
  if (length == 0 || length >= MAX_PATH) return L"";
  std::wstring value(path, length);
  const std::size_t slash = value.find_last_of(L"\\/");
  return slash == std::wstring::npos ? L"" : value.substr(0, slash);
}
std::string Utf8(const std::wstring& value) {
  const int count = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
  if (count <= 1) return "";
  std::string result(static_cast<std::size_t>(count), '\0');
  WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, &result[0], count, nullptr, nullptr);
  result.pop_back();
  return result;
}
bool ReadFileBytes(const std::wstring& path, std::vector<unsigned char>* bytes) {
  HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return false;
  LARGE_INTEGER size = {};
  if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > 64 * 1024 * 1024) {
    CloseHandle(file);
    return false;
  }
  bytes->resize(static_cast<std::size_t>(size.QuadPart));
  DWORD read = 0;
  const bool ok = ReadFile(file, bytes->data(), static_cast<DWORD>(bytes->size()), &read, nullptr) &&
                  read == bytes->size();
  CloseHandle(file);
  return ok;
}
void Log(const char* format, ...) {
  if (!g_logging_enabled) return;
  CreateDirectoryW(g_runtime_root.c_str(), nullptr);
  const std::wstring path = Join(g_runtime_root, L"sgmde-video-subs.log");
  HANDLE file = CreateFileW(path.c_str(), FILE_APPEND_DATA | GENERIC_READ, FILE_SHARE_READ,
                            nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return;
  LARGE_INTEGER size = {};
  if (GetFileSizeEx(file, &size) && size.QuadPart >= kLogLimitBytes) {
    CloseHandle(file);
    file = CreateFileW(path.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS,
                       FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return;
  }
  char message[640] = {};
  va_list args;
  va_start(args, format);
  _vsnprintf_s(message, sizeof(message), _TRUNCATE, format, args);
  va_end(args);
  SYSTEMTIME now = {};
  GetLocalTime(&now);
  char line[760] = {};
  const int count = _snprintf_s(line, sizeof(line), _TRUNCATE,
      "%04u-%02u-%02u %02u:%02u:%02u %s\r\n", now.wYear, now.wMonth, now.wDay,
      now.wHour, now.wMinute, now.wSecond, message);
  DWORD written = 0;
  if (count > 0) WriteFile(file, line, static_cast<DWORD>(count), &written, nullptr);
  CloseHandle(file);
}
template <typename T> bool LoadAssExport(const char* name, T* destination) {
  *destination = ExportAddress<T>(GetProcAddress(g_ass.module, name));
  return *destination != nullptr;
}
bool InitializeOriginal() {
  if (g_bink_open && g_bink_close && g_bink_copy) return true;
  const std::wstring path = Join(g_game_root, L"bink2w32_original.dll");
  g_original_module = LoadLibraryExW(path.c_str(), nullptr,
      LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
  if (!g_original_module) {
    Log("original load failed error=%lu", GetLastError());
    return false;
  }
  g_bink_open = ExportAddress<BinkOpenFn>(GetProcAddress(g_original_module, "_BinkOpen@8"));
  g_bink_close = ExportAddress<BinkCloseFn>(GetProcAddress(g_original_module, "_BinkClose@4"));
  g_bink_copy = ExportAddress<BinkCopyFn>(GetProcAddress(g_original_module, "_BinkCopyToBuffer@28"));
  if (!g_bink_open || !g_bink_close || !g_bink_copy) {
    Log("original missing required Bink exports");
    return false;
  }
  Log("original=bink2w32_original.dll loaded by full path");
  return true;
}
bool InitializeAss() {
  if (g_ass.renderer) return true;
  if (g_ass_failed) return false;
  const std::wstring library_path = Join(Join(g_runtime_root, L"lib"), L"libass-5.dll");
  g_ass.module = LoadLibraryExW(library_path.c_str(), nullptr,
      LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32);
  if (!g_ass.module || !LoadAssExport("ass_library_init", &g_ass.library_init) ||
      !LoadAssExport("ass_library_done", &g_ass.library_done) ||
      !LoadAssExport("ass_renderer_init", &g_ass.renderer_init) ||
      !LoadAssExport("ass_renderer_done", &g_ass.renderer_done) ||
      !LoadAssExport("ass_read_memory", &g_ass.read_memory) ||
      !LoadAssExport("ass_free_track", &g_ass.free_track) ||
      !LoadAssExport("ass_set_frame_size", &g_ass.set_frame_size) ||
      !LoadAssExport("ass_set_fonts", &g_ass.set_fonts) ||
      !LoadAssExport("ass_set_fonts_dir", &g_ass.set_fonts_dir) ||
      !LoadAssExport("ass_add_font", &g_ass.add_font) ||
      !LoadAssExport("ass_render_frame", &g_ass.render_frame)) {
    g_ass_failed = true;
    Log("libass load failed error=%lu; decoder pass-through", GetLastError());
    return false;
  }
  g_ass.library = g_ass.library_init();
  if (!g_ass.library) { g_ass_failed = true; Log("libass library init failed; decoder pass-through"); return false; }
  const std::wstring font_dir = Join(g_runtime_root, L"fonts");
  g_font_directory_utf8 = Utf8(font_dir);
  g_ass.set_fonts_dir(g_ass.library, g_font_directory_utf8.c_str());
  if (!ReadFileBytes(Join(font_dir, L"NotoSansCJK-Regular.ttc"), &g_font_bytes)) {
    g_ass_failed = true; Log("font missing; decoder pass-through"); return false;
  }
  char font_name[] = "NotoSansCJK-Regular.ttc";
  g_ass.add_font(g_ass.library, font_name, reinterpret_cast<char*>(g_font_bytes.data()),
                 static_cast<int>(g_font_bytes.size()));
  g_ass.renderer = g_ass.renderer_init(g_ass.library);
  if (!g_ass.renderer) { g_ass_failed = true; Log("libass renderer init failed; decoder pass-through"); return false; }
  g_ass.set_fonts(g_ass.renderer, nullptr, "Noto Sans CJK SC", kAssFontProviderAutodetect, nullptr, 1);
  Log("renderer=libass-5.dll font=NotoSansCJK-Regular.ttc");
  return true;
}
bool ValidBink(const Bink* bink) {
  return bink && bink->width > 0 && bink->width <= 3840 && bink->height > 0 && bink->height <= 2160 &&
         bink->frame_rate > 0 && bink->frame_rate_divisor > 0;
}
const wchar_t* SubtitleFile(sgmde::SubtitleKind kind) {
  return kind == sgmde::SubtitleKind::kOpening ? L"OP01.zh.ass" : L"prologue01.zh.ass";
}
const char* SubtitleName(sgmde::SubtitleKind kind) {
  return kind == sgmde::SubtitleKind::kOpening ? "OP01.zh.ass" : "prologue01.zh.ass";
}
void BlendAssImage(const AssImage* image, unsigned char* destination, std::int32_t pitch,
                   std::uint32_t width, std::uint32_t height, bool preserve_alpha) {
  if (!image || !image->bitmap || image->width <= 0 || image->height <= 0) return;
  const int left = image->dst_x < 0 ? 0 : image->dst_x;
  const int top = image->dst_y < 0 ? 0 : image->dst_y;
  const int right = image->dst_x + image->width > static_cast<int>(width) ? static_cast<int>(width) : image->dst_x + image->width;
  const int bottom = image->dst_y + image->height > static_cast<int>(height) ? static_cast<int>(height) : image->dst_y + image->height;
  if (right <= left || bottom <= top) return;
  for (int y = top; y < bottom; ++y) {
    const unsigned char* source = image->bitmap + (y - image->dst_y) * image->stride + left - image->dst_x;
    unsigned char* target = destination + y * pitch + left * 4;
    for (int x = left; x < right; ++x, ++source, target += 4) {
      sgmde::BlendAssBgraPixel(target, image->color, *source, preserve_alpha);
    }
  }
}
Bink* __stdcall BinkOpenHook(const char* name, std::uint32_t flags) {
  if (!InitializeOriginal()) return nullptr;
  Bink* bink = g_bink_open(name, flags);
  const sgmde::SubtitleKind kind = sgmde::SelectSubtitle(name);
  if (kind == sgmde::SubtitleKind::kNone || !ValidBink(bink)) return bink;
  EnterCriticalSection(&g_lock);
  if (!InitializeAss()) { LeaveCriticalSection(&g_lock); return bink; }
  std::vector<unsigned char> subtitle;
  const std::wstring path = Join(g_runtime_root, SubtitleFile(kind));
  if (!ReadFileBytes(path, &subtitle)) { Log("open subtitle=%s missing; decoder pass-through", SubtitleName(kind)); LeaveCriticalSection(&g_lock); return bink; }
  AssTrack* track = g_ass.read_memory(g_ass.library, reinterpret_cast<char*>(subtitle.data()), subtitle.size(), nullptr);
  if (track) {
    g_sessions[bink] = {track, kind, false, false, false};
    Log("open subtitle=%s resolution=%ux%u frameRate=%u/%u", SubtitleName(kind), bink->width, bink->height, bink->frame_rate, bink->frame_rate_divisor);
  } else Log("open subtitle=%s parse failed; decoder pass-through", SubtitleName(kind));
  LeaveCriticalSection(&g_lock);
  return bink;
}
void __stdcall BinkCloseHook(Bink* bink) {
  EnterCriticalSection(&g_lock);
  const auto found = g_sessions.find(bink);
  if (found != g_sessions.end()) { g_ass.free_track(found->second.track); Log("close subtitle=%s", SubtitleName(found->second.kind)); g_sessions.erase(found); }
  LeaveCriticalSection(&g_lock);
  if (InitializeOriginal()) g_bink_close(bink);
}
std::int32_t __stdcall BinkCopyHook(Bink* bink, void* destination, std::int32_t pitch,
                                    std::uint32_t destination_height, std::uint32_t destination_x,
                                    std::uint32_t destination_y, std::uint32_t surface) {
  if (!InitializeOriginal()) return 0;
  const std::int32_t result = g_bink_copy(bink, destination, pitch, destination_height, destination_x, destination_y, surface);
  if (!destination || pitch <= 0 || destination_x != 0 || destination_y != 0 || !ValidBink(bink)) return result;
  EnterCriticalSection(&g_lock);
  const auto found = g_sessions.find(bink);
  if (found == g_sessions.end() || !g_ass.renderer) { LeaveCriticalSection(&g_lock); return result; }
  if ((surface != kBinkSurface32 && surface != kBinkSurface32A) ||
      pitch % 4 != 0 || !destination_height) {
    if (!found->second.unsupported_logged) { Log("copy unsupported surface=%u pitch=%d height=%u; subtitle skipped", surface, pitch, destination_height); found->second.unsupported_logged = true; }
    LeaveCriticalSection(&g_lock); return result;
  }
  const std::uint32_t width = static_cast<std::uint32_t>(pitch / 4);
  g_ass.set_frame_size(g_ass.renderer, static_cast<int>(width), static_cast<int>(destination_height));
  const std::uint32_t milliseconds = sgmde::FrameToMilliseconds(bink->frame_number, bink->frame_rate, bink->frame_rate_divisor);
  int changed = 0;
  AssImage* image = g_ass.render_frame(g_ass.renderer, found->second.track, milliseconds, &changed);
  const bool visible = image != nullptr;
  for (AssImage* item = image; item; item = item->next) BlendAssImage(item, static_cast<unsigned char*>(destination), pitch, width, destination_height, surface == kBinkSurface32A);
  if (!found->second.rendered || found->second.visible != visible) {
    Log("frame=%u time_ms=%u visible=%d", bink->frame_number, milliseconds, visible ? 1 : 0);
    found->second.rendered = true; found->second.visible = visible;
  }
  LeaveCriticalSection(&g_lock);
  return result;
}
}  // namespace

extern "C" Bink* __stdcall BinkOpen(const char*, std::uint32_t);
extern "C" Bink* __stdcall BinkOpen(const char* name, std::uint32_t flags) { return BinkOpenHook(name, flags); }
extern "C" void __stdcall BinkClose(Bink*);
extern "C" void __stdcall BinkClose(Bink* bink) { BinkCloseHook(bink); }
extern "C" std::int32_t __stdcall BinkCopyToBuffer(Bink*, void*, std::int32_t, std::uint32_t, std::uint32_t, std::uint32_t, std::uint32_t);
extern "C" std::int32_t __stdcall BinkCopyToBuffer(Bink* bink, void* destination, std::int32_t pitch, std::uint32_t height, std::uint32_t x, std::uint32_t y, std::uint32_t surface) { return BinkCopyHook(bink, destination, pitch, height, x, y, surface); }

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
  if (reason == DLL_PROCESS_ATTACH) {
    DisableThreadLibraryCalls(instance);
    InitializeCriticalSection(&g_lock);
    g_game_root = ModuleDirectory(instance);
    g_runtime_root = Join(g_game_root, L"sgmde-video-subs");
    wchar_t value[2] = {};
    g_logging_enabled = GetEnvironmentVariableW(L"SGMDE_VIDEO_SUBS_LOG", value, 2) == 1 && value[0] == L'1';
  }
  return TRUE;
}

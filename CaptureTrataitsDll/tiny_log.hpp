// ===================== Light_Log.hpp =====================
#pragma once
#include <chrono>
#include <cstdio>
#include "fast_reconstruct.h"        // 仍然假定：using LogFn = void(*)(const char*)

namespace fr::log {

    /*--- 日志标签 --------------------------------------------------------*/
    enum class Tag : uint8_t {
        Begin,   // 🔄
        Grid,    // 📦
        Timer,   // ⏱️
        Ok,      // ✅
        Warn,    // ⚠️
        Error    // ❌
    };

    /*--- Tag → UTF-8 Emoji ----------------------------------------------*/
    inline constexpr const char* emoji(Tag t) noexcept
    {
        switch (t) {
        case Tag::Begin:  return "\xF0\x9F\x94\x84";             // 🔄
        case Tag::Grid:   return "\xF0\x9F\x93\xA6";             // 📦
        case Tag::Timer:  return "\xE2\x8F\xB1";     // ⏱️
        case Tag::Ok:     return "\xE2\x9C\x85";                 // ✅
        case Tag::Warn:   return "\xE2\x9A\xA0\xEF\xB8\x8F";     // ⚠️
        case Tag::Error:  return "\xE2\x9D\x8C";                 // ❌
        default:          return "";
        }
    }

    /*--- 轻量日志打印 ----------------------------------------------------*/
    inline void print(LogFn fn, Tag tag, const char* msg, double ms = -1.0)
    {
        if (!fn) return;

        char buf[256];
        if (ms >= 0.0)
            std::snprintf(buf, sizeof(buf), "%s %-20s %.2f ms", emoji(tag), msg, ms);
        else
            std::snprintf(buf, sizeof(buf), "%s %s", emoji(tag), msg);

        fn(buf);   // 仍保持旧的「只传一串 char*」回调
    }

    /*--- RAII 计时器 ----------------------------------------------------*/
    class ScopedTimer {
        const char* lbl;
        LogFn       log;
        Tag         tag;
        std::chrono::high_resolution_clock::time_point t0;
    public:
        ScopedTimer(const char* label, LogFn fn, Tag tag = Tag::Timer) noexcept
            : lbl(label), log(fn), tag(tag),
            t0(std::chrono::high_resolution_clock::now()) {
        }

        ~ScopedTimer() {
            double ms = std::chrono::duration<double, std::milli>(
                std::chrono::high_resolution_clock::now() - t0).count();
            print(log, tag, lbl, ms);
        }
    };

} // namespace fr::log

/*--- 兼容旧代码宏 ----------------------------------------------------*/
using T = fr::log::ScopedTimer;

// 旧 _log(...) 依然可用：_log(logFn, fr::log::Tag::Begin, "msg");
inline void _log(LogFn fn, fr::log::Tag tag, const char* msg, double ms = -1.0)
{
    fr::log::print(fn, tag, msg, ms);
}

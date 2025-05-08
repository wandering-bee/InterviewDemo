// ===================== Light_Log.hpp =====================
#pragma once
#include <chrono>
#include <cstdio>
#include "fast_reconstruct.h"   // 提供 LogFn

namespace fr::log {
    // 【方法】 轻量日志（Emoji 标记 + 可选耗时）
    inline void print(LogFn fn, const char* emoji, const char* msg, double ms = -1.0) {
        if (!fn) return;
        char buf[256];
        if (ms >= 0.0)
            std::snprintf(buf, sizeof(buf), "%s %-12s %.2f ms", emoji, msg, ms);
        else
            std::snprintf(buf, sizeof(buf), "%s %s", emoji, msg);
        fn(buf);
    }

    // 【方法】 RAII 计时器
    class ScopedTimer {
        const char* tag; LogFn log;
        std::chrono::high_resolution_clock::time_point t0;
    public:
        ScopedTimer(const char* tag, LogFn log) : tag(tag), log(log),
            t0(std::chrono::high_resolution_clock::now()) {
        }
        ~ScopedTimer() {
            double ms = std::chrono::duration<double, std::milli>(
                std::chrono::high_resolution_clock::now() - t0).count();
            print(log, "🔄", tag, ms);
        }
    };
} // namespace fr::log

// 兼容旧代码
using T = fr::log::ScopedTimer;
inline void _log(LogFn f, const char* e, const char* m, double ms = -1.0) {
    fr::log::print(f, e, m, ms);
}
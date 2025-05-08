// ===================== Grid_Hash.hpp =====================
#pragma once
#include <utility>
#include <cmath>
#include <cstdint>

namespace fr::grid {
    using Key = std::pair<int, int>;

    // 【方法】 pair<int,int> → size_t
    struct PairHash {
        std::size_t operator()(const Key& p) const noexcept {
            return (static_cast<size_t>(static_cast<uint32_t>(p.first)) << 32) ^
                static_cast<uint32_t>(p.second);
        }
    };

    // 【方法】 坐标 → 网格键（传入 invCell = 1 / cellSize）
    inline Key makeKey(double x, double y, double invCell) {
        return { int(std::llround(x * invCell)), int(std::llround(y * invCell)) };
    }
} // namespace fr::grid
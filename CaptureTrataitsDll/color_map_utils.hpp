// ===================== color_map_utils.hpp =====================
#pragma once
#include <array>
#include <algorithm>
#include <cmath>
#include "fast_reconstruct.h"   // Vec3f

namespace fr::colormap {
    constexpr int CSTEP = 255;              // ÿ��ɫ��
    constexpr int TOTAL = 5 * CSTEP;        // Turbo ɫ���� 1275 ��

    namespace detail {
        // �������� band / off �� Vec3f
        constexpr Vec3f makeColor(int band, int off) {
            int r = 0, g = 0, b = 0;
            switch (band) {
            case 0: b = off;                break;
            case 1: g = off;  b = 255;      break;
            case 2: g = 255; b = 255 - off; break;
            case 3: r = off;  g = 255;      break;
            case 4: r = 255; g = 255 - off; break;
            }
            return { r / 255.f, g / 255.f, b / 255.f };
        }

        template<std::size_t... I>
        constexpr std::array<Vec3f, TOTAL> build(std::index_sequence<I...>) {
            return { makeColor(I / CSTEP, I % CSTEP)... };
        }

        static constexpr auto LUT = build(std::make_index_sequence<TOTAL>{});
    } // namespace detail

    // �������� t �� [0,1] �� ��ɫ
    inline const Vec3f& mapTurbo(float t) {
        t = std::clamp(t, 0.f, 1.f);
        int idx = static_cast<int>(std::nearbyint(t * (TOTAL - 1)));
        return detail::LUT[idx];
    }

    // ���ݾ����� mapColor(double)
    inline const Vec3f& mapColor(double t) { return mapTurbo(static_cast<float>(t)); }
} // namespace fr::colormap
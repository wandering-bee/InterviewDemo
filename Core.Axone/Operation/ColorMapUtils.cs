using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace Axone.Core {
    public static class ColorMapUtils {

        private const int COLORS_PER_STEP = 255;
        private const int TOTAL_COLORS = 1275;               // 5 × 255

        /// <summary>查表：长度 1275，已归一化到 0-1。</summary>
        private static readonly Vector3[ ] _lut = BuildLut();

        /// <summary>
        /// 把 <paramref name="value"/> 映射到颜色。<br/>
        /// 事先把 <c>invRange = 1 / (vmax - vmin)</c> 算好，循环里只做乘法。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetColorLut(double val , double vmin , double vmax) {
            double t = (val - vmin) / (vmax - vmin);
            return _lut[IdxClamp(t)];
        }

        // 调大循环性能时用：pre-compute invRange
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetColorLutFast(double val , double vmin ,
                                               double invRange) {
            double t = (val - vmin) * invRange;
            return _lut[IdxClamp(t)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IdxClamp(double t) {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return (int)Math.Round(t * (TOTAL_COLORS - 1) ,
                                   MidpointRounding.ToEven);
        }

        /// <summary>一次性构 LUT，启动时花 0.02 ms，常驻 ~30 KB。</summary>
        private static Vector3[ ] BuildLut() {
            var tbl = new Vector3[TOTAL_COLORS];
            const double inv255 = 1.0 / 255;

            for (int i = 0 ; i < TOTAL_COLORS ; ++i) {
                int band = i / COLORS_PER_STEP;          // 0-4
                int offset = i - band * COLORS_PER_STEP;   // 同 % 但更快

                int r = 0, g = 0, b = 0;
                switch (band) {
                    case 0: b = offset; break;             // 黑→蓝
                    case 1: g = offset; b = 255; break;             // 蓝→青
                    case 2: g = 255; b = 255 - offset; break;       // 青→绿
                    case 3: r = offset; g = 255; break;             // 绿→黄
                    case 4: r = 255; g = 255 - offset; break;       // 黄→红
                }
                tbl[i] = new Vector3((float)(r * inv255) , (float)(g * inv255) , (float)(b * inv255));
            }
            return tbl;
        }
    

    }

}

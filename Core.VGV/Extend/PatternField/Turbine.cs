using System;
using System.Collections.Generic;
using System.Text.Json;
using OpenCvSharp;

namespace Extend
{

    /// <summary>
    /// 生成残余应力‑致厚度扰动场；并提供 Mat 融合扩展方法。
    /// </summary>
    public static class Turbine
    {
        /* ————————— 常量 ————————— */
        private const double ESubstrateMpa = 130_000; // 硅 E 130 GPa
        private const double NuSubstrate = 0.28;
        private static readonly double KStoney = 6 * (1 - NuSubstrate) / ESubstrateMpa; // ✅

        /* ============================ 主公开 API ============================ */
        /// <summary>
        /// 生成厚度扰动场 (double[,]) 及 JSON 元数据。
        /// </summary>
        public static (double[,], string) Generate(SimFild p)
        {
            // ⚠️ 边界判断
            if (p.WaferRadiusUm <= 0) throw new ArgumentException("WaferRadiusUm <= 0");

            /* —— 网格尺寸 & 预分配 —— */
            double pitchUm = p.GridPitchUm > 0 ? p.GridPitchUm : 100; // fallback
            int size = (int)Math.Ceiling(2 * p.WaferRadiusUm / pitchUm) + 1;
            int ctr = size / 2;
            double[,] sigma = new double[size, size];
            double[,] deltaT = new double[size, size];

            /* —— Perlin 噪声实例 —— */
            var perlin = new PerlinNoise(p.Seed);
            double maxAbsS = 1e-9;                // 避免除零
            double theta0 = p.AnisotropyTheta0Deg * Math.PI / 180.0;

            /* —— Pass‑1：生成 σ —— */
            for (int j = 0; j < size; j++)
            {
                double y = (j - ctr) * pitchUm;
                for (int i = 0; i < size; i++)
                {
                    double x = (i - ctr) * pitchUm;
                    double s = 0;
                    foreach (var layer in p.PerlinLayers)
                    {
                        double nx = x / p.WaferRadiusUm * layer.Frequency;
                        double ny = y / p.WaferRadiusUm * layer.Frequency;
                        s += layer.Weight * perlin.Noise(nx, ny);
                    }
                    sigma[i, j] = s;
                    double absS = Math.Abs(s); if (absS > maxAbsS) maxAbsS = absS;
                }
            }

            /* —— Pass‑2：归一化 + 各向异性 + κ→Δt —— */
            double filmT = p.FilmThicknessUm;
            double subT2 = p.SubstrateThicknessUm * p.SubstrateThicknessUm;
            for (int j = 0; j < size; j++)
            {
                double y = (j - ctr) * pitchUm;
                for (int i = 0; i < size; i++)
                {
                    double x = (i - ctr) * pitchUm;
                    double r = Math.Sqrt(x * x + y * y);
                    if (r > p.WaferRadiusUm) { deltaT[i, j] = -1e6; continue; }

                    double s = sigma[i, j] / maxAbsS * p.StressMaxMpa;
                    double theta = Math.Atan2(y, x);
                    s *= (1 + p.AnisotropyA * Math.Cos(2 * (theta - theta0)));

                    double kappa = KStoney * s * filmT / subT2;
                    deltaT[i, j] = -0.5 * kappa * r * r;           // µm
                }
            }

            string metaJson = JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true });
            return (deltaT, metaJson);
        }

        /// <summary>
        /// 直接生成 OpenCV Mat (CV_64F) 与 JSON metas。
        /// </summary>
        public static (Mat, string) GenerateMat(SimFild p)
        {
            var (buf, json) = Generate(p);
            int rows = buf.GetLength(1), cols = buf.GetLength(0);
            var mat = new Mat(rows, cols, MatType.CV_64F);
            unsafe
            {
                double* ptr = (double*)mat.Data.ToPointer();
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++, ptr++) *ptr = buf[i, j];
            }
            return (mat, json);
        }

        /// <summary>
        /// 扩展方法：在已有厚度场 Mat 上叠加残余应力扰动。
        /// </summary>
        /// <remarks>
        /// 基准 <paramref name="baseField"/> 必须为 CV_64F 方阵，且尺寸与参数一致。
        /// </remarks>
        public static Mat ApplyResidualStress(this Mat baseField, SimFild p)
        {
            /* 【边界判断】 */
            if (baseField.Empty() || baseField.Type() != MatType.CV_64F)
                throw new ArgumentException("baseField 必须是 CV_64F");
            if (!baseField.IsContinuous()) baseField = baseField.Clone(); // 保证连续

            int N = baseField.Rows;
            if (baseField.Cols != N) throw new ArgumentException("baseField 必须为方阵");

            // —— 若未设 pitchUm，则自动推导 ——
            double pitchAuto = 2 * p.WaferRadiusUm / (N - 1);
            var pUse = p.GridPitchUm > 0 ? p : p with { GridPitchUm = pitchAuto };
            if (Math.Abs(pUse.GridPitchUm - pitchAuto) > 1e-6)
                throw new ArgumentException("参数 GridPitchUm 与 baseField 尺寸不匹配。");

            var (deltaT, _) = Generate(pUse);
            var result = baseField.Clone();

            unsafe
            {
                double* resPtr = (double*)result.Data.ToPointer();
                int idx = 0;
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++, idx++)
                    {
                        if (resPtr[idx] < -1e5) continue; // 圆外 mask
                        resPtr[idx] += deltaT[i, j];
                    }
            }
            return result;
        }

        /* ======================= 内部：2D Perlin 实现 ======================= */
        private sealed class PerlinNoise
        {
            private readonly int[] perm = new int[512];
            public PerlinNoise(int seed)
            {
                var rnd = new Random(seed);
                int[] p = new int[256];
                for (int i = 0; i < 256; i++) p[i] = i;
                for (int i = 255; i > 0; i--) { int s = rnd.Next(i + 1); (p[i], p[s]) = (p[s], p[i]); }
                for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            }
            public double Noise(double x, double y)
            {
                int xi = FastFloor(x) & 255; int yi = FastFloor(y) & 255;
                double xf = x - Math.Floor(x); double yf = y - Math.Floor(y);
                double u = Fade(xf); double v = Fade(yf);
                int aa = perm[perm[xi] + yi]; int ab = perm[perm[xi] + yi + 1];
                int ba = perm[perm[xi + 1] + yi]; int bb = perm[perm[xi + 1] + yi + 1];
                double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
                double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
                return Lerp(x1, x2, v); // [-1,1]
            }
            private static int FastFloor(double t) => (t >= 0 ? (int)t : (int)t - 1);
            private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
            private static double Lerp(double a, double b, double t) => a + t * (b - a);
            private static double Grad(int h, double x, double y)
            {
                int g = h & 3; double u = g < 2 ? x : y; double v = g < 2 ? y : x;
                return ((g & 1) == 0 ? u : -u) + ((g & 2) == 0 ? v : -v);
            }
        }
    }
}


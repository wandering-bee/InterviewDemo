using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Extend
{
    public static class FieldSeed
    {
        /// <summary>
        /// 生成 200×200 晶圆厚度场（nm），圆外 = -999999。
        /// 自 2025-05-05 起：
        ///  ① 引入低频 Bias（材料非均匀）；
        ///  ② 叠加随机方向温度梯度；
        ///  ③ 叠加加工条纹伪影。
        /// </summary>
        public static Mat BuildCircleField(SimFild fild)
        {
            /* —— 随机宏观曲面参数 —— */
            var rnd = new Random();
            double bowAmp = rnd.NextDouble() * 4 - 2;          // -2 … +2 µm
            double tiltX = rnd.NextDouble() * 1 - 0.5;        // -0.5 … +0.5 µm
            double tiltY = rnd.NextDouble() * 1 - 0.5;
            double saddle = rnd.NextDouble() * 1.6 - 0.8;      // -0.8 … +0.8 µm

            const double edgeAmp = -0.35;   // Edge roll-off
            const int edgeWidth = 16;

            /* —— 中尺度高斯 + 微观粗糙参数（原有） —— */
            const int gaussCnt = 4;
            const double gaussMax = 1.0;    // ±1 µm
            const double microAmp = 0.1;    // ±0.1 µm

            /* —— 新增参数 —— */
            const double biasAmp = 0.2;   // 材料非均匀 ±0.2 µm
            const double tempAmp = 0.5;   // 温度梯度 ±0.5 µm
            const double stripeAmp = 0.05;  // 加工纹理 ±0.05 µm
            const double stripePeriod = 30.0;  // 周期 ≈30 pix
            double tempDir = rnd.NextDouble() * 2 * Math.PI;
            double stripeDir = rnd.NextDouble() * 2 * Math.PI;
            double cosT = Math.Cos(tempDir), sinT = Math.Sin(tempDir);
            double cosS = Math.Cos(stripeDir), sinS = Math.Sin(stripeDir);
            const double TWO_PI = Math.PI * 2.0;

            /* ===== 0. 初始化 ===== */
            var field = new Mat(fild.Size, fild.Size, MatType.CV_64F, Scalar.All(fild.Mid));
            var mask = new Mat(fild.Size, fild.Size, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Circle(mask, new(fild.Size / 2, fild.Size / 2), fild.Radius, Scalar.All(255), -1);

            /* ===== 1. 宏观曲面 + Edge + 温度梯度 + 条纹 ===== */
            double c = (fild.Size - 1) * 0.5;
            double RR = fild.Radius * fild.Radius;

            unsafe
            {
                double* fBase = (double*)field.Data.ToPointer();
                byte* mBase = (byte*)mask.Data.ToPointer();
                int step = (int)mask.Step();

                for (int y = 0; y < fild.Size; y++)
                {
                    double* fRow = fBase + y * fild.Size;
                    byte* mRow = mBase + y * step;
                    double dy = y - c;

                    for (int x = 0; x < fild.Size; x++)
                    {
                        if (mRow[x] == 0) { fRow[x] = -999999d; continue; }

                        double dx = x - c;
                        double r2 = dx * dx + dy * dy;
                        double r = Math.Sqrt(r2);
                        double rNorm = r2 / RR;

                        /* —— 宏观基础形变 —— */
                        double h = bowAmp * (1 - rNorm)                 // 凸/凹碗
                                 + tiltX * dx / fild.Radius + tiltY * dy / fild.Radius      // 楔形
                                 + saddle * (dx * dx - dy * dy) / RR;   // 鞍形

                        /* —— Edge roll-off —— */
                        int distEdge = fild.Radius - (int)r;
                        if (distEdge < edgeWidth)
                        {
                            double t = distEdge / (double)edgeWidth;
                            h += edgeAmp * 0.5 * (1 + Math.Cos(t * Math.PI));
                        }

                        /* —— 温度梯度 —— */
                        h += tempAmp * (dx * cosT + dy * sinT) / fild.Radius;

                        /* —— 加工条纹 —— */
                        double proj = dx * cosS + dy * sinS;
                        h += stripeAmp * Math.Sin(proj * TWO_PI / stripePeriod);

                        fRow[x] += h;
                    }
                }
            }

            /* ===== 1.5 低频 Bias（材料非均匀） ===== */
            var bias = new Mat(fild.Size, fild.Size, MatType.CV_64F);
            Cv2.Randu(bias, -biasAmp, biasAmp);                  // Uniform noise
            int k = (fild.Size / 10) | 1;                             // 确保奇数核
            Cv2.GaussianBlur(bias, bias, new(k, k), 0);     // 大核模糊 → 低频
            Cv2.Add(field, bias, field, mask: mask);             // 只加在圆内
            bias.Dispose();

            /* ===== 2. 中尺度高斯（原有逻辑，未改） ===== */
            var centers = new List<(int x, int y, double s)>();
            int posNeed = gaussCnt / 2, negNeed = gaussCnt - posNeed;
            int placed = 0;
            while (placed < gaussCnt)
            {
                double sigma = rnd.Next(20, 36);                 // 20–35 pix
                int cx, cy;
                do { cx = rnd.Next(fild.Size); cy = rnd.Next(fild.Size); }
                while (mask.At<byte>(cy, cx) == 0);

                double dx0 = cx - c, dy0 = cy - c;
                if (Math.Sqrt(dx0 * dx0 + dy0 * dy0) + 2 * sigma > fild.Radius) continue;

                bool tooClose = false;
                foreach (var (ox, oy, os) in centers)
                {
                    double d = Math.Sqrt((cx - ox) * (cx - ox) + (cy - oy) * (cy - oy));
                    if (d < sigma + os) { tooClose = true; break; }
                }
                if (tooClose) continue;

                double amp = (posNeed == 0) ? -(rnd.NextDouble() * gaussMax)
                           : (negNeed == 0) ? (rnd.NextDouble() * gaussMax)
                           : (rnd.NextDouble() * 2 - 1) * gaussMax;
                if (amp > 0) posNeed--; else negNeed--;

                for (int y = Math.Max(0, cy - 3 * (int)sigma); y <= Math.Min(fild.Size - 1, cy + 3 * (int)sigma); y++)
                {
                    double dy = y - cy;
                    for (int x = Math.Max(0, cx - 3 * (int)sigma); x <= Math.Min(fild.Size - 1, cx + 3 * (int)sigma); x++)
                    {
                        if (mask.At<byte>(y, x) == 0) continue;
                        double dx = x - cx;
                        double g = Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                        field.Set(y, x, field.At<double>(y, x) + amp * g);
                    }
                }

                centers.Add((cx, cy, sigma));
                placed++;
            }

            /* ===== 3. 微观粗糙（原有） ===== */
            var micro = new Mat(fild.Size, fild.Size, MatType.CV_64F);
            Cv2.Randu(micro, -microAmp, microAmp);
            Cv2.GaussianBlur(micro, micro, new(3, 3), 0);
            Cv2.Add(field, micro, field, mask: mask);
            micro.Dispose();

            return field;
        }
    }
}

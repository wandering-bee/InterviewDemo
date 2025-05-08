using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Demo.Showcase
{
    public sealed partial class LatencyHistogram : UserControl
    {
        public LatencyHistogram() => InitializeComponent();

        /* 目标样本总数：在开始压测时由外部赋值 */
        public int TargetSamples { get; set; } = 0;   // 0 = 自动 barMax

        /*  —— HSV → RGB 工具，用于柔和渐变 ——  */
        private static Color HsvToRgb(double h, double s, double v)
        {
            // h 0-360, s/v 0-1
            int i = (int)Math.Floor(h / 60) % 6;
            double f = h / 60 - i;
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb(200,
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255));
        }

        public void Update(IReadOnlyList<double> latencies)
        {
            /* ────── 区间（6 桶）────── */
            var ranges = new (double lo, double hi, string label)[]
            {
                (   0.0 ,   100.0 , "<100µs"),
                ( 100.0 ,   250.0 , "100-250µs"),
                ( 250.0 ,   500.0 , "250-500µs"),
                ( 500.0 ,  1000.0 , "0.5-1ms"),
                (1000.0 ,  5000.0 , "1-5ms"),
                (5000.0 , double.MaxValue, ">5ms")
            };

            /* ────── 统计进桶 ────── */
            int[] bucket = new int[ranges.Length];
            foreach (double t in latencies)
                for (int i = 0; i < ranges.Length; i++)
                    if (t >= ranges[i].lo && t < ranges[i].hi) { bucket[i]++; break; }

            /* ────── 柱长度按 TargetSamples 比例 ────── */
            int denom = TargetSamples > 0 ? TargetSamples : bucket.Max();
            if (denom == 0) denom = 1;

            /* ────── 动态配色：相对 min / max ────── */
            int maxBucket = bucket.Max();
            int minBucket = bucket.Where(b => b > 0).DefaultIfEmpty(0).Min();
            if (maxBucket == 0) maxBucket = 1;                // 全零兜底

            /* ────── 布局尺寸 ────── */
            const int LABEL_W = 140;
            const int PADDING_B = 25;

            double w = ActualWidth > 0 ? ActualWidth : 600;
            double h = ActualHeight > 0 ? ActualHeight : 220;
            double chartW = w - LABEL_W - 10;
            double chartH = h - PADDING_B - 10;
            double rowH = chartH / ranges.Length;

            Root.Children.Clear();
            if (latencies.Count == 0) return;

            /* 背景网格 */
            var bg = new Rectangle
            {
                Width = chartW,
                Height = chartH,
                Fill = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
            };
            Canvas.SetLeft(bg, LABEL_W);
            Canvas.SetTop(bg, 5);
            Root.Children.Add(bg);

            SolidColorBrush emptyBrush = new(Color.FromArgb(90, 90, 90, 90));

            /* 绘柱 + 标签 */
            for (int i = 0; i < ranges.Length; i++)
            {
                /* —— 柱长度 —— */
                double lenRatio = bucket[i] / (double)denom;
                double barLen = chartW * lenRatio;

                /* —— 颜色：相对 min-max → Hue 220 → 0 —— */
                SolidColorBrush brush;
                if (bucket[i] == 0)
                {
                    brush = emptyBrush;                     // 空桶灰
                }
                else
                {
                    double norm = (bucket[i] - minBucket) / (double)(maxBucket - minBucket + 1e-9);
                    norm = Math.Clamp(norm, 0, 1);          // 0 = 蓝, 1 = 红
                    Color c = HsvToRgb(220 - 220 * norm, 0.75, 0.75);
                    brush = new SolidColorBrush(c);
                }

                /* —— 绘制柱 —— */
                var bar = new Rectangle
                {
                    Width = barLen,
                    Height = rowH - 6,
                    Fill = brush,
                    RadiusX = 3,
                    RadiusY = 3
                };
                double top = 8 + i * rowH;
                Canvas.SetLeft(bar, LABEL_W);
                Canvas.SetTop(bar, top);
                Root.Children.Add(bar);

                /* —— 右对齐标签 —— */
                var txt = new TextBlock
                {
                    Width = LABEL_W - 8,
                    Text = $"{ranges[i].label}  {bucket[i]:N0}",
                    TextAlignment = TextAlignment.Right,
                    Foreground = new SolidColorBrush(Colors.WhiteSmoke),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };
                Canvas.SetLeft(txt, 0);
                Canvas.SetTop(txt, top + 2);
                Root.Children.Add(txt);
            }

            /* X 轴 */
            var axis = new Line
            {
                X1 = LABEL_W,
                X2 = w - 5,
                Y1 = h - PADDING_B,
                Y2 = h - PADDING_B,
                Stroke = new SolidColorBrush(Colors.Gainsboro),
                StrokeThickness = 1
            };
            Root.Children.Add(axis);
        }
    }
}

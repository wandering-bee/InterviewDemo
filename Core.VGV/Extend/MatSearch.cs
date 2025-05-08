using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extend {
    public static class MatSearch {

        public struct Features(double min , double smin , double max , double ave) {
            public double Min { get; set; } = min; public double SecondMin { get; set; } = smin; public double Max { get; set; } = max;
            public double Average { get; set; } = ave;

            public readonly bool Invalid {
                get => Min == 0 && SecondMin == 0 && Max == 0 && Average == 0;
            }
        }

        /// <summary>
        /// 生成单行调试字符串，方便 Debug/日志。
        /// </summary>
        public static string ToDebugString(this in Features f, string? tag = null) =>
            $"{tag ?? "Features"} | Min={f.Min:F3}, 2ndMin={f.SecondMin:F3}, " +
            $"Max={f.Max:F3}, Avg={f.Average:F3}, Invalid={f.Invalid}";

        /// <summary>
        /// 直接输出到 Debug 窗口（仅 Debug 构建有效）。
        /// </summary>
        [Conditional("DEBUG")]
        public static void Dump(this in Features f, string? tag = null) =>
            Debug.WriteLine(f.ToDebugString(tag));

        /// <summary>
        /// 检索黑色标记值 - Ez 版（使用传入数据的 0 , 0 坐标的值作为黑色标记值）
        /// <para>理论上这个方法在任何尺寸的晶圆数据上都不会出错</para>
        /// </summary>
        /// <param name="src">需要检索的数据 Mat</param>
        /// <returns>黑色标记值</returns>
        public static double FindDarkEz(this Mat src) {
            return src.At<double>(0 , 0);
        }

        /// <summary>
        /// 在传入的 src 为空的情况下 , 它会返回自动机的标准 Dark 值 -999999 。
        /// </summary>
        /// <param name="src">需要检索的数据 Mat</param>
        /// <returns>黑色标记值</returns>
        public static double FindDark(this Mat src) {
            if (src.Empty()) return -999999d;

            double min = double.MaxValue;

            for (int iY = 0 ; iY < src.Rows ; iY++) {
                for (int iX = 0 ; iX < src.Cols ; iX++) {
                    double value = src.At<double>(iY , iX);
                    min = value < min ? value : min;
                }
            }

            return min;
        }

        public static Features GetFeatures(this Mat mat , double? ignore = -999999) {
            if (mat.Empty()) return new(0 , 0 , 0 , 0);

            double min = double.MaxValue;
            double secondMin = double.MaxValue;
            double max = double.MinValue;
            double sum = 0;
            int count = 0;  // 用于计算有效值的个数

            for (int iY = 0 ; iY < mat.Rows ; iY++) {
                for (int iX = 0 ; iX < mat.Cols ; iX++) {
                    double value = mat.Get<double>(iY , iX);

                    if (ignore.HasValue && value == ignore.Value) {
                        continue;  // 忽略指定的值
                    }

                    sum += value;
                    count++;

                    if (value < min) {
                        secondMin = min;
                        min = value;
                    } else if (value < secondMin && value != min) {
                        secondMin = value;
                    }

                    if (value > max) { max = value; }
                }
            }

            double average;

            if (count != 0) {
                average = sum / count;
            } else { average = 0; }

            return new Features {
                Min = Math.Round(min , 3) ,
                SecondMin = Math.Round(secondMin , 3) ,
                Max = Math.Round(max , 3) ,
                Average = Math.Round(average , 3)
            };
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenTK.Mathematics;

namespace Extend
{
    public static class FieldBuilder
    {

        public static List<Vector3> MatDoubleTo3F_Circular(this Mat src, double radiusMultiplier = 1)
        {
            List<Vector3> vectorList = [];
            int rows = src.Rows;
            int cols = src.Cols;

            double centerX = cols / 2.0;
            double centerY = rows / 2.0;
            double maxR = Math.Min(centerX, centerY) * radiusMultiplier;
            double maxRSqr = maxR * maxR;

            for (int iY = 0; iY < rows; iY++)
            {
                for (int iX = 0; iX < cols; iX++)
                {
                    double Z = src.At<double>(iY, iX);

                    if (Z == -999999) continue;

                    double dx = iX + 0.5 - centerX;
                    double dy = iY + 0.5 - centerY;
                    double distSqr = dx * dx + dy * dy;

                    if (distSqr > maxRSqr) continue; // ❌ 裁剪：超出圆

                    // 以 um 单位保存 , 重要 ！
                    vectorList.Add(new Vector3((float)dx * 1000, (float)-dy * 1000, (float)Z));
                }
            }

            return vectorList;
        }


    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace Core.VGV.Extend
{
    public static class Inter
    {

        public static Mat BilinearInterp(Mat src, CvSize newSize, double ignore)
        {
            Mat resized = new Mat(newSize, src.Type(), Scalar.All(ignore));

            double scaleX = (double)src.Width / newSize.Width;
            double scaleY = (double)src.Height / newSize.Height;

            for (int iY = 0; iY < newSize.Height; iY++)
            {
                double Y = (iY + 0.5) * scaleY - 0.5;
                int Y0 = (int)Math.Floor(Y);
                int Y1 = Y0 + 1;
                double dY = Y - Y0;

                Y0 = Math.Clamp(Y0, 0, src.Height - 1);
                Y1 = Math.Clamp(Y1, 0, src.Height - 1);

                for (int iX = 0; iX < newSize.Width; iX++)
                {
                    double X = (iX + 0.5) * scaleX - 0.5;
                    int X0 = (int)Math.Floor(X);
                    int X1 = X0 + 1;
                    double dX = X - X0;

                    X0 = Math.Clamp(X0, 0, src.Width - 1);
                    X1 = Math.Clamp(X1, 0, src.Width - 1);

                    double α = (1 - dX) * (1 - dY);
                    double β = dX * (1 - dY);
                    double γ = (1 - dX) * dY;
                    double δ = dX * dY;

                    double Pα = src.At<double>(Y0, X0);
                    double Pβ = src.At<double>(Y0, X1);
                    double Pγ = src.At<double>(Y1, X0);
                    double Pδ = src.At<double>(Y1, X1);

                    double numerator = 0;
                    double denominator = 0;

                    void Accumulate(double P, double w)
                    {
                        if (P != ignore)
                        {
                            numerator += P * w;
                            denominator += w;
                        }
                    }

                    Accumulate(Pα, α);
                    Accumulate(Pβ, β);
                    Accumulate(Pγ, γ);
                    Accumulate(Pδ, δ);

                    resized.Set(iY, iX, denominator > 0 ? numerator / denominator : ignore);
                }
            }

            return resized;
        }

        public static Mat OwnBilinearInterp(this Mat src, CvSize newSize, double ignore)
        {
            var resized = BilinearInterp(src, newSize, ignore);
            src.GiveBack(resized);
            return src;
        }

        public static Mat BilinearInterp(Mat src, double scale, double ignore)
        {
            var newSize = new CvSize((src.Width * scale), (src.Height * scale));
            return BilinearInterp(src, newSize, ignore);
        }

        public static Mat OwnBilinearInterp(this Mat src, double scale, double ignore)
        {
            var resized = BilinearInterp(src, scale, ignore);
            src.GiveBack(resized);
            return src;
        }

        /// <summary>
        /// 将 <paramref name="cache"/> 的内容深拷贝到 <paramref name="src"/>，使其成为从 <paramref name="cache"/> 获得的完整副本。
        /// <para>如果 <paramref name="src"/> 的属性（如尺寸或类型）与 <paramref name="cache"/> 不一致，将用 <paramref name="cache"/> 的属性覆盖 <paramref name="src"/>。</para>
        /// </summary>
        /// <param name="src">用于接收深拷贝的内容。</param>
        /// <param name="cache">提供要拷贝的内容。</param>
        public static void GiveBack(this Mat src, Mat cache)
        {
            cache.CopyTo(src);
            cache.Dispose();
        }

    }
}

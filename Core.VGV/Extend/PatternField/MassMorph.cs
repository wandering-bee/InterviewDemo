using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Extend
{
    public static class MassMorph
    {

        public static Mat SimulateThreePointSag(Mat ideal , SimFild fild)
        {
            if (ideal.Empty() || ideal.Type() != MatType.CV_64F)
                throw new ArgumentException("ideal 必须是 CV_64F");

            int N = ideal.Rows;
            double waferDiaUm = fild.Radius * 2 * 1000.0;
            double pitchUm = waferDiaUm / (N - 1);

            int Rp = N / 2 - 1;
            double R_m = (Rp * pitchUm) * 1e-6;
            double cx = (N - 1) * 0.5, cy = cx;

            double t_m = fild.SubstrateThicknessUm * 1e-6;
            double E_Pa = fild.ElasticModulusMPa * 1e6;
            double D = E_Pa * Math.Pow(t_m, 3) / (12 * (1 - fild.PoissonRatio * fild.PoissonRatio));
            double q = fild.Rho * 9.80665 * t_m;
            double K = q / (64.0 * D);

            const double SIN60 = 0.866025403784, COS60 = 0.5;
            double Rs_px = (Rp - fild.PadRadial_mm * 0.5 * 1000.0 / pitchUm);
            (double x, double y)[] Ctr = {
        (cx, cy - Rs_px),
        (cx - Rs_px * SIN60, cy + Rs_px * COS60),
        (cx + Rs_px * SIN60, cy + Rs_px * COS60)
    };

            double hx = fild.PadTangential_mm * 1000.0 / pitchUm * 0.5;
            double hy = fild.PadRadial_mm * 1000.0 / pitchUm * 0.5;

            var sag = new Mat(N, N, MatType.CV_64F);
            double Rp2 = Rp * Rp;
            const double umPerM = 1e6;

            unsafe
            {
                double* sPtr = (double*)sag.Data.ToPointer();
                for (int y = 0; y < N; y++)
                {
                    double dy = y - cy, dy2 = dy * dy;
                    for (int x = 0; x < N; x++, sPtr++)
                    {
                        double dx = x - cx, r2 = dx * dx + dy2;
                        if (r2 > Rp2) { *sPtr = 0; continue; }

                        double r̂ = Math.Sqrt(r2) / Rp;
                        double φ0 = 1 - r̂ * r̂; φ0 *= φ0;
                        *sPtr = -K * Math.Pow(R_m, 4) * φ0 * umPerM;
                    }
                }
            }

            double r̂s = Rs_px / Rp;
            double φ0s = Math.Pow(1 - r̂s * r̂s, 2);
            double ψs = Math.Pow(r̂s, 3) * φ0s;
            double w0s = -K * Math.Pow(R_m, 4) * φ0s * umPerM;
            double A = -w0s / ψs;

            unsafe
            {
                double* sPtr = (double*)sag.Data.ToPointer();
                for (int y = 0; y < N; y++)
                {
                    double dy = y - cy;
                    for (int x = 0; x < N; x++, sPtr++)
                    {
                        double dx = x - cx;
                        double r2 = dx * dx + dy * dy;
                        if (r2 > Rp2) continue;

                        double r̂ = Math.Sqrt(r2) / Rp;
                        double ψ = Math.Pow(r̂, 3) * Math.Pow(1 - r̂ * r̂, 2);
                        double θ = Math.Atan2(-dy, dx) + Math.PI / 2.0;
                        *sPtr += A * ψ * Math.Cos(3 * θ);
                    }
                }
            }

            double[] zPad = new double[3];
            for (int k = 0; k < 3; k++)
            {
                double sum = 0; int cnt = 0;
                int cxk = (int)Math.Round(Ctr[k].x);
                int cyk = (int)Math.Round(Ctr[k].y);

                for (int y = cyk - (int)hy; y <= cyk + (int)hy; y++)
                {
                    if (y < 0 || y >= N) continue;
                    for (int x = cxk - (int)hx; x <= cxk + (int)hx; x++)
                    {
                        if (x < 0 || x >= N) continue;
                        sum += sag.At<double>(y, x);
                        cnt++;
                    }
                }
                zPad[k] = cnt == 0 ? 0 : sum / cnt;
            }

            double[,] M = {
        { Ctr[0].x, Ctr[0].y, 1 },
        { Ctr[1].x, Ctr[1].y, 1 },
        { Ctr[2].x, Ctr[2].y, 1 }
    };
            double det = M[0, 0] * (M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1])
                       - M[0, 1] * (M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0])
                       + M[0, 2] * (M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]);
            double a = ((M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1]) * zPad[0]
                      - (M[0, 1] * M[2, 2] - M[0, 2] * M[2, 1]) * zPad[1]
                      + (M[0, 1] * M[1, 2] - M[0, 2] * M[1, 1]) * zPad[2]) / det;
            double b = -((M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0]) * zPad[0]
                      - (M[0, 0] * M[2, 2] - M[0, 2] * M[2, 0]) * zPad[1]
                      + (M[0, 0] * M[1, 2] - M[0, 2] * M[1, 0]) * zPad[2]) / det;
            double c = ((M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]) * zPad[0]
                      - (M[0, 0] * M[2, 1] - M[0, 1] * M[2, 0]) * zPad[1]
                      + (M[0, 0] * M[1, 1] - M[0, 1] * M[1, 0]) * zPad[2]) / det;

            var measured = ideal.Clone();
            unsafe
            {
                double* mPtr = (double*)measured.Data.ToPointer();
                double* sPtr = (double*)sag.Data.ToPointer();

                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < N; x++, mPtr++, sPtr++)
                    {
                        if (*mPtr < -1e5) continue;
                        double plane = a * x + b * y + c;
                        double wRel = *sPtr - plane;
                        *mPtr += wRel;
                    }
                }
            }

            sag.Dispose();
            return measured;
        }


    }
}

using System;
using System.Collections.Generic;
using OpenCvSharp;
using Extend;

namespace Extend
{

    public record class SimFild
    {
        // 全局控制参数
        public int Size { get; set; } = 200;
        public double Mid { get; set; } = 775.0;
        public int Radius { get; set; } = 98;

        // MassMorph 参数
        public double SubstrateThicknessUm { get; set; } = 775.0;
        public double Rho { get; set; } = 2330;
        public double ElasticModulusMPa { get; set; } = 130e3;
        public double PoissonRatio { get; set; } = 0.28;
        public double PadTangential_mm { get; set; } = 10.0;
        public double PadRadial_mm { get; set; } = 5.0;

        // Turbine（残余应力）参数
        public double WaferRadiusUm { get; init; } = 98_000;   // µm
        public double GridPitchUm { get; init; } = 0;       // µm/px (≤0 ⇒ 自动推导)
        public double FilmThicknessUm { get; init; } = 1.2;    // µm
        public double StressMaxMpa { get; init; } = 65;        // 0‑100 MPa
        public List<PerlinLayer> PerlinLayers { get; init; } = new()
{
    new PerlinLayer(1,   0.5),
    new PerlinLayer(4,   0.3),
    new PerlinLayer(16,  0.2),
    new PerlinLayer(32,  0.1),
};
        public double AnisotropyA { get; init; } = 0.15;       // 0‑1
        public double AnisotropyTheta0Deg { get; init; } = 270;// °
        public int Seed { get; init; } = Environment.TickCount;

        public record struct PerlinLayer(int Frequency, double Weight);


        // 构造函数：初始化默认值
        public SimFild()
        {

        }


        public Mat Generate()
        {
            return new();
        }


    }

}

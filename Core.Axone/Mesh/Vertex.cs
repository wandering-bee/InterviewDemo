using System.Runtime.InteropServices;
using OpenTK.Mathematics;


namespace Axone.Meshing
{

    /*――― double 精度（高精度计算用）―――*/
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexD
    {

        public Vector3d Position;
        public Vector3d Normal;

        /// <summary>
        /// 0~1 Double
        /// </summary>
        public Vector3d Color;
    }

    /// <summary>
    /// 16 B 紧凑顶点：half3 位置 + packed 法线/颜色。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct VertexP16
    {
        public Vector3h Position;   // 0–5   (half*3)
        private ushort _pad;        // 6–7   对齐到 4 字节

        public uint Normal;         // 8–11  SNORM 2_10_10_10
        public uint Color;          // 12–15 UNORM 2_10_10_10

        public VertexP16(Vector3 pos, Vector3 n, Vector3 rgb)
        {
            Position = new Vector3h(pos);

            _pad = 0;

            Normal = PackSnorm10_10_10(n);
            Color = PackUnorm10_10_10(rgb);
        }

        /* --- 打包工具 (略简化) --- */
        static uint PackSnorm10_10_10(in Vector3 v)
        {
            int x = (int)MathF.Round(MathF.Max(-1, MathF.Min(1, v.X)) * 511);
            int y = (int)MathF.Round(MathF.Max(-1, MathF.Min(1, v.Y)) * 511);
            int z = (int)MathF.Round(MathF.Max(-1, MathF.Min(1, v.Z)) * 511);
            return (uint)((x & 0x3FF) |
                           ((y & 0x3FF) << 10) |
                           ((z & 0x3FF) << 20));
        }
        static uint PackUnorm10_10_10(in Vector3 v)
        {
            int x = (int)MathF.Round(MathF.Max(0, MathF.Min(1, v.X)) * 1023);
            int y = (int)MathF.Round(MathF.Max(0, MathF.Min(1, v.Y)) * 1023);
            int z = (int)MathF.Round(MathF.Max(0, MathF.Min(1, v.Z)) * 1023);
            return (uint)((x & 0x3FF) |
                           ((y & 0x3FF) << 10) |
                           ((z & 0x3FF) << 20));
        }
    }

    /*――― float 精度（渲染用）―――*/
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexF(Vector3 position, Vector3 normal, Vector3 color)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;

        /// <summary>
        /// 0~1 float
        /// </summary>
        public Vector3 Color = color;
    }

    /*――― 法线可视化顶点（线段）―――*/
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalViewF(Vector3 position, Vector3 color)
    {

        public Vector3 Position = position;
        public Vector3 Color = color;
    }


}

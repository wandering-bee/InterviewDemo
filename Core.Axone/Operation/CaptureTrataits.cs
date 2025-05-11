using Extend;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using OpenCvSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Axone.Meshing;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;


namespace Axone.Core {

    public enum ReconstructMode { Subdiv, Topology }

    public static class CaptureTrataits {
        public static Task<GLMesh<VertexF>> ReconstructAB(
    IReadOnlyList<Vector3> pointCloud,
    ReconstructMode mode = ReconstructMode.Subdiv,
    double topoTolerance = 0.1,
    Action<string>? log = null)
    => Task.Run(() => ReconstructAB(pointCloud.ToList(), mode, topoTolerance, log));


        /* ---------- CaptureTrataits.cs 追加 ---------- */
        public static GLMesh<VertexF> ReconstructAB(
            List<Vector3> cloud,
            ReconstructMode mode = ReconstructMode.Subdiv,
            double topoTolerance = 0.1,
            Action<string>? log = null)
        {
            /*【边界判断】空云直接回零网格 */
            if (cloud is null || cloud.Count == 0)
                return new GLMesh<VertexF>(Array.Empty<VertexF>());

            /* 1️⃣ 点云 → Vec3f[] */
            var pts = new Meshing.Vec3f[cloud.Count];
            for (int i = 0; i < pts.Length; ++i)
            {
                Vector3 p = cloud[i];
                pts[i] = new Meshing.Vec3f { x = p.X, y = p.Y, z = p.Z };
            }

            var sw = Stopwatch.StartNew();

            /* 2️⃣ P/Invoke 调用 */
            Native.LogFn? logFn = log is null ? null : s => log(s);
            Err err = Native.ReconstructAA(pts, (uint)pts.Length,
                                           mode, topoTolerance,
                                           out var nativeMesh, logFn);

            if (err != Err.Ok)
                throw new InvalidOperationException($"ReconstructAA failed: {err}");

            /* 3️⃣ 非托管 → 托管数组 */
            var verts = new VertexF[nativeMesh.vCnt];
            var idx = nativeMesh.iCnt > 0 ? new uint[nativeMesh.iCnt] : null;

            unsafe
            {
                fixed (VertexF* pDst = verts)
                    Buffer.MemoryCopy((void*)nativeMesh.verts, pDst,
                                       verts.Length * sizeof(VertexF),
                                       verts.Length * sizeof(VertexF));

                if (idx != null)
                    fixed (uint* pIdx = idx)
                        Buffer.MemoryCopy((void*)nativeMesh.idx, pIdx,
                                           idx.Length * sizeof(uint),
                                           idx.Length * sizeof(uint));
            }

            /* 4️⃣ 归还原始缓冲 */
            Native.FreeMesh(ref nativeMesh);

            /* 5️⃣ 封装为 GLMesh 并返回 */
            return new GLMesh<VertexF>(verts, idx);
        }


        // ───────────────────────── 公 API ─────────────────────────
        public static Task<GLMesh<VertexF>> ReconstructAA(
            IReadOnlyList<Vector3> pointCloud ,
            ReconstructMode mode = ReconstructMode.Subdiv ,
            double topoTolerance = 0.1 ,
            Action<string>? log = null)
            => Task.Run(() => Reconstruct(pointCloud , mode , topoTolerance , log));


        /// <summary>同步重建入口（含分阶段耗时统计）。</summary>
        public static GLMesh<VertexF> Reconstruct(
            IReadOnlyList<Vector3> pointCloud ,
            ReconstructMode mode = ReconstructMode.Subdiv ,
            double topoTolerance = 0.1 ,
            Action<string>? log = null) {
            if (pointCloud.Count < 3) return new GLMesh<VertexF>(Array.Empty<VertexF>());
            log ??= _ => { };


            /* ── 1. Delaunay ─────────────────────────────────────── */
            var sw = Stopwatch.StartNew();
            int[ ] triIdx = mode switch {
                ReconstructMode.Subdiv => DelaunaySubdivIndices(pointCloud),
                ReconstructMode.Topology => DelaunayTopologyIndices(pointCloud , topoTolerance),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
            log($"Triangulation({mode}) {sw.ElapsedMilliseconds} ms  faces={triIdx.Length / 3}");

            /* ── 2. 构建顶点 / EBO ───────────────────────────────── */
            sw.Restart();
            int faceCnt = triIdx.Length / 3;
            int maxVerts = pointCloud.Count;

            int[ ] mapOld2New = new int[maxVerts];
            int[ ] mapNew2Old = new int[maxVerts];
            Vector3[ ] positions = new Vector3[maxVerts];
            Vector3[ ] normalSum = new Vector3[maxVerts];
            Array.Fill(mapOld2New , -1);

            uint[ ] ebo = new uint[triIdx.Length];
            int uniqVertCnt = 0;

            for (int f = 0, i = 0 ; f < faceCnt ; ++f) {
                int ia = triIdx[i++], ib = triIdx[i++], ic = triIdx[i++];
                Vector3 va = pointCloud[ia], vb = pointCloud[ib], vc = pointCloud[ic];

                Vector3 fn = CalcUnitNormal(in va , in vb , in vc);

                // —— 去重 / 填坐标表 ——
                ref int ma = ref mapOld2New[ia];
                if (ma == -1) {
                    ma = uniqVertCnt;
                    positions[uniqVertCnt] = va;
                    mapNew2Old[uniqVertCnt] = ia;
                    ++uniqVertCnt;
                }
                ref int mb = ref mapOld2New[ib];
                if (mb == -1) {
                    mb = uniqVertCnt;
                    positions[uniqVertCnt] = vb;
                    mapNew2Old[uniqVertCnt] = ib;
                    ++uniqVertCnt;
                }
                ref int mc = ref mapOld2New[ic];
                if (mc == -1) {
                    mc = uniqVertCnt;
                    positions[uniqVertCnt] = vc;
                    mapNew2Old[uniqVertCnt] = ic;
                    ++uniqVertCnt;
                }

                // —— 写 EBO ——
                ebo[i - 3] = (uint)ma;
                ebo[i - 2] = (uint)mb;
                ebo[i - 1] = (uint)mc;

                // —— 面法线累加 ——
                normalSum[ma] += fn;
                normalSum[mb] += fn;
                normalSum[mc] += fn;
            }

            /* ── 3. 归一化法线 + 坐标中心化 + 颜色映射 ───────────────── */
            var vF = new VertexF[uniqVertCnt];

            /* ① 真·包围盒（只看已用顶点）*/
            Vector3 vMin = positions[0], vMax = positions[0];
            for (int i = 1; i < uniqVertCnt; ++i)
            {
                vMin = Vector3.ComponentMin(vMin, positions[i]);
                vMax = Vector3.ComponentMax(vMax, positions[i]);
            }

            /* —— XY 中心 —— */
            float cx = (vMin.X + vMax.X) * 0.5f;
            float cy = (vMin.Y + vMax.Y) * 0.5f;

            /* ② Z 对称中心 & 半幅 */
            double zMid = 0.5 * (vMin.Z + vMax.Z);           // ⭐ 对称中心
            double zHalf = 0.5 * (vMax.Z - vMin.Z);           // 半幅 (≥0)
            double invRange = zHalf > 0 ? 1.0 / (2.0 * zHalf) : 0.0; // 1/(max-min)

            /* ③ 填顶点 */
            for (int i = 0; i < uniqVertCnt; ++i)
            {

                /* — 去中心 — */
                Vector3 pos = positions[i];
                pos.X -= cx;
                pos.Y -= cy;
                pos.Z -= (float)zMid;                         // 对称化后 Z ∈ [-zHalf, +zHalf]

                /* — 法线 — */
                Vector3 n = normalSum[mapNew2Old[i]];
                n.Normalize();

                /* — 颜色 — */
                Vector3 col = ColorMapUtils.GetColorLutFast(
                                  pos.Z,                      // val
                                  -zHalf,                     // vmin  (-half)
                                  invRange);                  // 1/(max-min)

                vF[i] = new VertexF(pos, n, col);

                if (i == 0)
                    log($"pos0 = ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F9})");
            }

            log($"BuildMesh {sw.ElapsedMilliseconds} ms  verts={uniqVertCnt}");
            return new GLMesh<VertexF>(vF, ebo);

        }



        /// <summary>单位法线计算；标量路径，JIT 可自动 SIMD 化。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 CalcUnitNormal(in Vector3 a , in Vector3 b , in Vector3 c) {

#if NET8_0_OR_GREATER && AVX2_ENABLED
        // AVX2 分支：仅在编译器定义 AVX2_ENABLED 时启用，避免无谓依赖
        if (Avx2.IsSupported) {
            // 构造 (x,y,z,0)
            Vector256<double> ab = Vector256.Create(b.X - a.X , b.Y - a.Y , b.Z - a.Z , 0d);
            Vector256<double> ac = Vector256.Create(c.X - a.X , c.Y - a.Y , c.Z - a.Z , 0d);

            // 旋转得到 y,z,x
            const byte permYZX = 0b_10_01_00_11; // {1,2,0,3}
            const byte permZXY = 0b_01_00_10_11; // {2,0,1,3}
            Vector256<double> abYZX = Avx.Permute4x64(ab , permYZX);
            Vector256<double> abZXY = Avx.Permute4x64(ab , permZXY);
            Vector256<double> acYZX = Avx.Permute4x64(ac , permYZX);
            Vector256<double> acZXY = Avx.Permute4x64(ac , permZXY);

            Vector256<double> cross = Avx.Subtract(Avx.Multiply(abYZX , acZXY) , Avx.Multiply(abZXY , acYZX));

            double nX = cross.GetElement(0) , nY = cross.GetElement(1) , nZ = cross.GetElement(2);
            double sign = Math.CopySign(1.0 , nZ);
            nX *= sign; nY *= sign; nZ *= sign;
            double invLen = 1.0 / Math.Sqrt(nX * nX + nY * nY + nZ * nZ);
            return new Vector3(nX * invLen , nY * invLen , nZ * invLen);
        }
#endif

            Vector3 n = Vector3.Cross(b - a , c - a);
            if (n.Z < 0) n = -n;
            n.Normalize();
            return n;
        }

        /// <summary>Subdiv2D Delaunay 三角索引（XY 平面）</summary>
        private static int[ ] DelaunaySubdivIndices(IReadOnlyList<Vector3> pc) {
            int n = pc.Count;
            var pts = new Point2f[n];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            for (int iP = 0 ; iP < n ; iP++) {
                float x = (float)pc[iP].X, y = (float)pc[iP].Y;
                pts[iP] = new Point2f(x , y);
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }

            var rect = new Rect((int)MathF.Floor(minX) - 1 , (int)MathF.Floor(minY) - 1 ,
                                 Math.Max(2 , (int)MathF.Ceiling(maxX) - (int)MathF.Floor(minX) + 2) ,
                                 Math.Max(2 , (int)MathF.Ceiling(maxY) - (int)MathF.Floor(minY) + 2));

            using var subdiv = new Subdiv2D(rect);
            subdiv.Insert(pts);
            Vec6f[ ] tri = subdiv.GetTriangleList();

            const float cellSize = 1e-3f;
            static (int, int) Key(float x , float y) => ((int)MathF.Round(x / cellSize), (int)MathF.Round(y / cellSize));

            var map = new Dictionary<(int, int) , int>(n);
            for (int iP = 0 ; iP < n ; iP++) map[Key(pts[iP].X , pts[iP].Y)] = iP;

            int[ ] idx = new int[tri.Length * 3];
            int k = 0; float rX = rect.X + rect.Width, rY = rect.Y + rect.Height;

            foreach (var v in tri) {
                Point2f p1 = new(v.Item0 , v.Item1), p2 = new(v.Item2 , v.Item3), p3 = new(v.Item4 , v.Item5);
                bool inside =
                    p1.X >= rect.X && p1.X <= rX && p1.Y >= rect.Y && p1.Y <= rY &&
                    p2.X >= rect.X && p2.X <= rX && p2.Y >= rect.Y && p2.Y <= rY &&
                    p3.X >= rect.X && p3.X <= rX && p3.Y >= rect.Y && p3.Y <= rY;
                if (!inside) continue;
                idx[k++] = map[Key(p1.X , p1.Y)];
                idx[k++] = map[Key(p2.X , p2.Y)];
                idx[k++] = map[Key(p3.X , p3.Y)];
            }
            Array.Resize(ref idx , k);
            return idx;
        }



        /// <summary>NTS Delaunay 三角索引（XY 平面）</summary>
        private static int[ ] DelaunayTopologyIndices(IReadOnlyList<Vector3> pc , double tol) {
            int n = pc.Count;
            var coordList = new List<Coordinate>(n);
            const double cellSize = 1e-4;
            static (int, int) Key(double x , double y) => ((int)Math.Round(x / cellSize), (int)Math.Round(y / cellSize));
            var map = new Dictionary<(int, int) , int>(n);

            for (int iP = 0 ; iP < n ; iP++) {
                var p = pc[iP];
                coordList.Add(new Coordinate(p.X , p.Y));
                map[Key(p.X , p.Y)] = iP;
            }

            var builder = new DelaunayTriangulationBuilder { Tolerance = tol };
            builder.SetSites(coordList);
            var tris = builder.GetSubdivision().GetTriangleVertices(false);

            int[ ] idx = new int[tris.Count * 3];
            int k = 0;
            foreach (var tri in tris) {
                idx[k++] = map[Key(tri[0].X , tri[0].Y)];
                idx[k++] = map[Key(tri[1].X , tri[1].Y)];
                idx[k++] = map[Key(tri[2].X , tri[2].Y)];
            }
            return idx;
        }

    }

}

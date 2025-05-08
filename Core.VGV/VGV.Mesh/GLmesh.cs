using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using Axone.Core;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace Axone.Meshing;

/* —— 与 C++ fast_reconstruct.h 完全对齐 —— */
[StructLayout(LayoutKind.Sequential)]
internal struct Vec3f { public float x, y, z; }

[StructLayout(LayoutKind.Sequential)]
internal struct Mesh
{
    public IntPtr verts;   // VertexF*
    public IntPtr idx;     // uint*
    public uint vCnt;
    public uint iCnt;
}

internal enum Err : uint { Ok = 0, EmptyInput = 1, AllocFail = 2 }

internal static partial class Native
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LogFn([MarshalAs(UnmanagedType.LPStr)] string msg);

    [DllImport("CaptureTrataitsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern Err ReconstructAA(
        [In] Vec3f[] pc, uint count,
        ReconstructMode mode, double topoTol,
        out Mesh mesh, LogFn log);

    [DllImport("CaptureTrataitsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FreeMesh(ref Mesh mesh);
}

public sealed class GLMesh<TVertex> where TVertex : unmanaged {
    public TVertex[ ] Vertices { get; private set; } = Array.Empty<TVertex>();
    public uint[ ]? Indices { get; private set; }

    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Vector3 Center => (BoundsMin + BoundsMax) * 0.5f;

    public GLMesh(TVertex[ ] verts , uint[ ]? idx = null) {
        Vertices = verts;
        Indices = idx;
        ComputeBounds();
    }

    /* ---------- Factory: PointCloud → VertexF Mesh ---------- */
    public static async Task<GLMesh<VertexF>> FromPointCloudAsync(
        List<Vector3> cloud ,
        ReconstructMode mode = ReconstructMode.Subdiv ,
        double topoTolerance = 0.1 ,
        Action<string>? log = null) {

        if (cloud is null || cloud.Count == 0)
            return new GLMesh<VertexF>(Array.Empty<VertexF>());

        return await Task.Run(() =>
            CaptureTrataits.ReconstructAB(
                cloud, mode, topoTolerance,
                log ?? (s => System.Diagnostics.Debug.WriteLine(s))));
        //return await CaptureTrataits.ReconstructAA(
        //    cloud , mode , topoTolerance , log ?? (s => System.Diagnostics.Debug.WriteLine(s)));
    }

    /* ---------- Bounds ---------- */
    void ComputeBounds() {
        if (Vertices.Length == 0) return;

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        foreach (ref readonly var v in Vertices.AsSpan()) {
            Vector3 p = ToVec3(v);         // <— 改回使用辅助
            min = Vector3.ComponentMin(min , p);
            max = Vector3.ComponentMax(max , p);
        }
        BoundsMin = min;
        BoundsMax = max;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector3 ToVec3(in TVertex v) => v switch {
        VertexF f => f.Position,   // 唯一合法分支
        _ => Vector3.Zero   // 若误用其它类型，给个零防止编译错
    };

}

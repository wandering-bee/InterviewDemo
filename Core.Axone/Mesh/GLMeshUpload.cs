using System;
using OpenTK.Graphics.OpenGL4;
using Axone.Resources;
using System.Runtime.CompilerServices;

namespace Axone.Meshing
{
    /// <summary>
    /// 集中管理 Mesh → GPU 的上传过程
    /// </summary>
    public static class GLMeshUpload
    {
        /// <summary>
        /// GPU 侧已上传网格的统一句柄。封装 VAO 以及其在「顶点 / 索引」流式缓冲中的偏移信息。
        /// </summary>
        /// <typeparam name="TVertex">顶点结构类型（需与 <paramref name="Vao"/> 的属性布局一致）。</typeparam>
        /// <param name="Vao">顶点数组对象：保存属性格式 + 绑定信息。</param>
        /// <param name="EPool">索引流式缓冲池；若为非索引绘制则为 <c>null</c>。</param>
        /// <param name="VPool">顶点流式缓冲池（持久映射 Ring-Buffer）。</param>
        /// <param name="BaseVertex">本网格顶点数据在 <paramref name="VPool"/> 内的元素偏移。</param>
        /// <param name="IndexOffsetBytes">本网格索引数据在 <paramref name="EPool"/> 内的字节偏移。</param>
        /// <remarks>
        /// 兼容旧代码，保留 <see cref="VaoHandle"/> / <see cref="VboHandle"/> 供直接绑定句柄使用。
        /// </remarks>
        public sealed record GpuHandle<TVertex>(
            VertexArrayObject<TVertex> Vao,
            GPUStreamBuffer<uint>? EPool,
            GPUStreamBuffer<TVertex> VPool,
            int BaseVertex,
            int IndexOffsetBytes,
            int Gen)
            where TVertex : unmanaged
        {
            public int VaoHandle => Vao.Handle;
            public uint VboHandle => VPool.Handle;
        }

        #region 全局流式缓冲池
        const int VERT_POOL_BYTES = 8 * 1024 * 1024;   // 8 MiB 顶点
        const int IDX_POOL_BYTES = 4 * 1024 * 1024;   // 4 MiB 索引
        #endregion

        static readonly GPUStreamBuffer<VertexF> sVertPool =
            new(VERT_POOL_BYTES / Unsafe.SizeOf<VertexF>(),
                BufferTarget.ArrayBuffer);

        static readonly GPUStreamBuffer<uint> sIdxPool =
            new(IDX_POOL_BYTES / sizeof(uint),
                BufferTarget.ElementArrayBuffer);


        /// <summary>
        /// 零复制流式上传（针对 VertexF 类型）
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static GpuHandle<VertexF> UploadStreamed(this GLMesh<VertexF> m)
        {
            /* —— ① 先确保容量 —— */
            EnsureCapacity(m.Vertices.Length, m.Indices?.Length ?? 0);

            /* —— ② 正常写入 —— */
            var vSpan = sVertPool.Allocate(m.Vertices.Length, out int vOffElem);
            m.Vertices.AsSpan().CopyTo(vSpan);
            sVertPool.CommitLast(m.Vertices.Length);

            int iOffElem = 0;
            if (m.Indices is { Length: > 0 })
            {
                var iSpan = sIdxPool.Allocate(m.Indices.Length, out iOffElem);
                m.Indices.AsSpan().CopyTo(iSpan);
                sIdxPool.CommitLast(m.Indices.Length);
            }

            /* —— ③ VAO —— */
            var vao = new VertexArrayObject<VertexF>();
            vao.AttachVertexBuffer(
                sVertPool.Handle,
                binding: 0,
                offsetBytes: vOffElem * Unsafe.SizeOf<VertexF>());

            if (m.Indices is { Length: > 0 })
                vao.AttachElementBuffer((int)sIdxPool.Handle);

            /* —— ④ 带上 Gen —— */
            return new GpuHandle<VertexF>(
                vao,
                m.Indices is { Length: > 0 } ? sIdxPool : null,
                sVertPool,
                BaseVertex: vOffElem,
                IndexOffsetBytes: iOffElem * sizeof(uint),
                Gen: sVertPool.Generation);
        }



        /// <summary>
        /// 旧式一次性不可变缓冲（已过时）
        /// </summary>
        /// <typeparam name="TVertex"></typeparam>
        /// <param name="m"></param>
        /// <returns></returns>
        public static GpuHandle<TVertex> UploadImmutable<TVertex>(this GLMesh<TVertex> m)
            where TVertex : unmanaged
        {
            // 顶点
            var vbo = GPUBuffer<TVertex>.CreatePersistent(
                BufferTarget.ArrayBuffer, m.Vertices.Length);

            m.Vertices.AsSpan().CopyTo(vbo.AsSpan(0, m.Vertices.Length));
            GL.MemoryBarrier(MemoryBarrierFlags.ClientMappedBufferBarrierBit);

            // 索引（可选）
            GPUBuffer<uint>? ebo = null;
            if (m.Indices is { Length: > 0 })
            {
                ebo = GPUBuffer<uint>.CreatePersistent(
                    BufferTarget.ElementArrayBuffer, m.Indices.Length);

                m.Indices.AsSpan().CopyTo(ebo.AsSpan(0, m.Indices.Length));
                GL.MemoryBarrier(MemoryBarrierFlags.ClientMappedBufferBarrierBit);
            }

            // VAO
            var vao = new VertexArrayObject<TVertex>();
            vao.AttachVertexBuffer(vbo.Handle);
            if (ebo is not null) vao.AttachElementBuffer((int)ebo.Handle);

            return new GpuHandle<TVertex>(vao, null,
                // 包一层适配：Immutable 仍返回独立 VBO 句柄
                VPool: new GPUStreamBuffer<TVertex>(m.Vertices.Length,
                                                    BufferTarget.ArrayBuffer),
                BaseVertex: 0,
                IndexOffsetBytes: 0,
                 Gen: 0);
        }


        /// <summary>按是否有 EBO 自动选择 Draw 调用。</summary>
        public static void Draw(this GpuHandle<VertexF> h, GLMesh<VertexF> mesh)
        {
            /* —— ① 代际校验 —— */
            if (h.Gen != h.VPool.Generation) return;   // 跨代直接跳过

            h.Vao.Bind();

            if (h.EPool is null)
            {
                GL.DrawArrays(PrimitiveType.Triangles,
                              h.BaseVertex,
                              mesh.Vertices.Length);
            }
            else
            {
                GL.DrawElementsBaseVertex(PrimitiveType.Triangles,
                                           mesh.Indices!.Length,
                                           DrawElementsType.UnsignedInt,
                                           (IntPtr)h.IndexOffsetBytes,
                                           h.BaseVertex);
            }
        }


        static void EnsureCapacity(int vertNeed, int idxNeed)
        {
            sVertPool.EnsureCapacity(vertNeed);
            sIdxPool.EnsureCapacity(idxNeed);
        }

        public static void ResetPools()        // 场景切换时调用
        {
            sVertPool.Reset();
            sIdxPool.Reset();
        }

        public static void BeginFrame()        // 每帧最早调用
        {
            sVertPool.FrameBegin();
            sIdxPool.FrameBegin();
        }

    }
}

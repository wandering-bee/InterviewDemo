using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Axone.Resources;

/// <summary>
/// 极速版 VAO：DSA + 反射缓存 + Normalized/Biding/Divisor 扩展
/// </summary>
public sealed class VertexArrayObject<TVertex> : IDisposable where TVertex : unmanaged {
    public readonly int Handle;
    bool _disposed;

    /*--------- ① 每种顶点类型只解析一次 ----------*/
    static readonly IReadOnlyList<AttribInfo> _layout = VertexLayoutCache.For<TVertex>();
    /*---------------------------------------------*/

    public VertexArrayObject() {
        GL.CreateVertexArrays(1 , out Handle);
        ConfigureAttributes();
#if DEBUG
        DumpLayout();
#endif
    }

    /*---------- 公开 API ----------*/

    /// <summary>把 VBO 绑到 binding index；可指定 stride/offset & instance divisor</summary>
    public void AttachVertexBuffer(uint vboHandle ,
                                   int binding = 0 ,
                                   int offsetBytes = 0 ,
                                   int? strideBytes = null ,
                                   int divisor = 0) {
        GL.VertexArrayVertexBuffer(
            Handle , binding , (int)vboHandle ,
            (IntPtr)offsetBytes ,
            strideBytes ?? Unsafe.SizeOf<TVertex>());

        if (divisor != 0)
            GL.VertexArrayBindingDivisor(Handle , binding , divisor);
    }

    /// <summary>可选：绑定索引缓冲</summary>
    public void AttachElementBuffer(int eboHandle) =>
        GL.VertexArrayElementBuffer(Handle , eboHandle);

    /// <remarks>仅为兼容旧式代码；DSA 场景通常不再需要显式 Bind。</remarks>
    public void Bind() => GL.BindVertexArray(Handle);

    public void Dispose() {
        if (!_disposed) {
            GL.DeleteVertexArray(Handle);
            _disposed = true;
        }
    }

    /*========== 私有：一次性配置属性 & Divisor ==========*/

    void ConfigureAttributes() {
        foreach (var a in _layout) {
            GL.VertexArrayAttribBinding(Handle , a.Index , a.Binding);

            switch (a.Kind)
            {
                case AttrKind.Float:
                    GL.VertexArrayAttribFormat(Handle, a.Index,
                    a.Components, a.FloatType, a.Normalized, a.RelativeOffset);
                    break;

                case AttrKind.Double:
                    GL.VertexArrayAttribLFormat(Handle, a.Index,
                    a.Components, VertexAttribType.Double, a.RelativeOffset);
                    break;
            }

            GL.EnableVertexArrayAttrib(Handle , a.Index);

            if (a.Divisor != 0)
                GL.VertexArrayBindingDivisor(Handle , a.Binding , a.Divisor);
        }
    }

#if DEBUG
    void DumpLayout() {
        foreach (var a in _layout)
            Debug.WriteLine($"[VAO] attr{a.Index}@binding{a.Binding} : {a.Components}×{a.Kind} " +
                            $"ofs={a.RelativeOffset} norm={a.Normalized} div={a.Divisor}");
    }
#endif

    /*============================================
     *            内部结构 / 反射缓存
     *==========================================*/

    /*---- 1. 允许字段上添加修饰 ----*/
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class VertexAttribAttribute : Attribute {
        public bool Normalized { get; init; } = false;
        public int Binding { get; init; } = 0;
        public int Divisor { get; init; } = 0;
    }

    enum AttrKind { Float, Double }

    readonly record struct AttribInfo(
        int Index ,
        int Components ,
        bool Normalized ,
        int RelativeOffset ,
        int Binding ,
        int Divisor ,
        AttrKind Kind ,
        VertexAttribType FloatType = VertexAttribType.Float);

    /*---- 2. 反射缓存 ----*/
    static unsafe class VertexLayoutCache {
        static readonly ConcurrentDictionary<Type , IReadOnlyList<AttribInfo>> _cache = new();

        public static IReadOnlyList<AttribInfo> For<T>() where T : unmanaged
            => _cache.GetOrAdd(typeof(T) , _ => {
                var list = new List<AttribInfo>();
                int attrIndex = 0;

                foreach (var f in typeof(T).GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                    var attr = f.GetCustomAttribute<VertexAttribAttribute>();
                    int relOfs = (int)Marshal.OffsetOf<T>(f.Name);

                    if (GenerateInfosForField(f.FieldType , ref attrIndex , relOfs , attr , list))
                        continue;

                    throw new NotSupportedException($"Unsupported vertex field type: {f.FieldType}");
                }
                return list;
            });

        /*--- 3. 类型映射表 ---*/
        static readonly Dictionary<Type, (int comp,
                                           AttrKind kind,
                                           VertexAttribType type)> _map =
            new()
            {
        /* ---- 标准浮点 ---- */
        { typeof(float)   , (1, AttrKind.Float , VertexAttribType.Float) },
        { typeof(Vector2) , (2, AttrKind.Float , VertexAttribType.Float) },
        { typeof(Vector3) , (3, AttrKind.Float , VertexAttribType.Float) },
        { typeof(Vector4) , (4, AttrKind.Float , VertexAttribType.Float) },

        /* ---- 紧凑整数 → 按浮点格式上传（GPU 自动位宽扩展） ---- */
        { typeof(byte)    , (1, AttrKind.Float , VertexAttribType.UnsignedByte ) },
        { typeof(sbyte)   , (1, AttrKind.Float , VertexAttribType.Byte         ) },
        { typeof(ushort)  , (1, AttrKind.Float , VertexAttribType.UnsignedShort) },
        { typeof(short)   , (1, AttrKind.Float , VertexAttribType.Short        ) },
        { typeof(uint)    , (1, AttrKind.Float , VertexAttribType.UnsignedInt  ) },
        { typeof(int)     , (1, AttrKind.Float , VertexAttribType.Int          ) },

        /* ---- 双精度 ---- */
        { typeof(Vector3d), (3, AttrKind.Double, VertexAttribType.Double       ) },
            };


        /*-- 4. 生成属性信息（Matrix4 拆 4 vec4）--*/
        /*-- 4. 生成属性信息（Matrix4 拆 4×vec4） --*/
        static bool GenerateInfosForField(
            Type t,
            ref int idx,
            int rel,
            VertexAttribAttribute? flag,
            List<AttribInfo> dst)
        {
            /* —— 矩阵字段：拆成四条 vec4 —— */
            if (t == typeof(Matrix4))
            {
                int colSize = Unsafe.SizeOf<Vector4>();   // 16 bytes per column
                for (int c = 0; c < 4; ++c)
                {
                    dst.Add(new AttribInfo(
                        idx++,                 // Index
                        4,                     // Components
                        flag?.Normalized ?? false,
                        rel + c * colSize,     // Relative offset
                        flag?.Binding ?? 0,
                        flag?.Divisor ?? 1,    // 常用于 instancing，默认 divisor = 1
                        AttrKind.Float,
                        VertexAttribType.Float));
                }
                return true;
            }

            /* —— 普通标量 / 向量字段 —— */
            if (_map.TryGetValue(t, out var m))
            {
                dst.Add(new AttribInfo(
                    idx++,                 // Index
                    m.comp,                // Components
                    flag?.Normalized ?? false,
                    rel,                   // Relative offset
                    flag?.Binding ?? 0,
                    flag?.Divisor ?? 0,
                    m.kind,                // AttrKind (Float / Double)
                    m.type));              // VertexAttribType
                return true;
            }

            /* —— 不支持的类型 —— */
            return false;
        }

    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Axone.Engine
{
    public sealed class ShaderProgram : IDisposable
    {
        /********************** 构造 / 资源 ************************/
        public int Handle { get; }

        public ShaderProgram(string vsSource, string fsSource)
        {
            int vs = Compile(ShaderType.VertexShader, vsSource);
            int fs = Compile(ShaderType.FragmentShader, fsSource);

            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vs);
            GL.AttachShader(Handle, fs);
            GL.LinkProgram(Handle);

            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0)
                throw new InvalidOperationException(
                    $"Program link error:\n{GL.GetProgramInfoLog(Handle)}");

            GL.DetachShader(Handle, vs);
            GL.DetachShader(Handle, fs);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
        }

        static int Compile(ShaderType type, string src)
        {
            int h = GL.CreateShader(type);
            GL.ShaderSource(h, src);
            GL.CompileShader(h);
            GL.GetShader(h, ShaderParameter.CompileStatus, out int ok);
            string log = GL.GetShaderInfoLog(h);
            if (!string.IsNullOrWhiteSpace(log))
                Debug.WriteLine($"[{type}] {log}");
            if (ok == 0)
                throw new InvalidOperationException($"{type} compile failed.");
            return h;
        }

        /********************** Uniform 缓存 ************************/
        readonly ConcurrentDictionary<string, int> _uCache = new();

        int U(string name)
        {
            if (_uCache.TryGetValue(name, out int loc)) return loc;
            loc = GL.GetUniformLocation(Handle, name);
            if (loc == -1)
                throw new ArgumentException($"Uniform '{name}' not found.", nameof(name));
            _uCache[name] = loc;
            return loc;
        }

        /********************** 绑定 / 设置 ************************/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Use() => GL.UseProgram(Handle);

        /* ---- fluent Set ---- */
        public ShaderProgram Set(string n, int v) { GL.Uniform1(U(n), v); return this; }
        public ShaderProgram Set(string n, float v) { GL.Uniform1(U(n), v); return this; }
        public ShaderProgram Set(string n, bool v) { GL.Uniform1(U(n), v ? 1 : 0); return this; }

        public ShaderProgram Set(string n, in Vector2 v) { GL.Uniform2(U(n), v); return this; }
        public ShaderProgram Set(string n, in Vector3 v) { GL.Uniform3(U(n), v); return this; }
        public ShaderProgram Set(string n, in Vector4 v) { GL.Uniform4(U(n), v); return this; }
        public ShaderProgram Set(string n, Matrix4 m, bool transpose = false)
        {
            GL.UniformMatrix4(U(n), transpose, ref m);
            return this;
        }

        /********************** 清理 ************************/
        public void Dispose()
        {
            if (Handle != 0) GL.DeleteProgram(Handle);
        }
    }
}

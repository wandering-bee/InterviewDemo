using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axone.Engine {
    // 定义Shader类，实现IDisposable接口以便资源回收
    public class Shader : IDisposable {
        protected int _handle; // 保护的句柄变量，用于存储着色器程序的引用

        // 使用当前的着色器程序
        // 使用当前的着色器程序（带安全检查）
        public void Use() {
            GL.UseProgram(_handle);
            GL.GetInteger(GetPName.CurrentProgram , out int cur);

            if (cur != _handle)
                throw new InvalidOperationException($"glUseProgram failed: CurrentProgram={cur}, expected={_handle}");
        }


        // 获取给定名称的统一变量位置
        public int GetUniformLocation(string name) {
            int location = GL.GetUniformLocation(_handle , name);
            if (location == -1)
                throw new InvalidOperationException(
                    $"Uniform '{name}' not found or optimized out (program {_handle}).");
            return location;
        }


        // 设置布尔值统一变量
        public static void SetBool(int location , bool value) {
            GL.Uniform1(location , value ? 1 : 0);
        }

        // 设置整数值统一变量
        public static void SetInt(int location , int value) {
            GL.Uniform1(location , value);
        }

        // 设置浮点值统一变量
        public static void SetFloat(int location , float value) {
            GL.Uniform1(location , value);
        }

        // 设置2分量向量统一变量
        public static void SetVector(int location , Vector2 value) {
            GL.Uniform2(location , value.X , value.Y);
        }

        // 设置3分量向量统一变量
        public static void SetVector(int location , Vector3 value) {
            GL.Uniform3(location , value.X , value.Y , value.Z);
        }

        // 设置3分量向量统一变量
        public static void SetVector(int location , Vector3d value) {
            GL.Uniform3(location , value.X , value.Y , value.Z);
        }

        // 设置4分量向量统一变量
        public static void SetVector(int location , Vector4 value) {
            GL.Uniform4(location , value.X , value.Y , value.Z , value.W);
        }

        // 设置4x4矩阵统一变量（使用OpenTK.Mathematics中的Matrix4类型）
        public static void SetMatrix(int location , Matrix4 value) {
            GL.UniformMatrix4(location , false , ref value);
        }

        // 释放着色器资源
        public void Dispose() => GL.DeleteProgram(_handle);

        // 重写Equals方法，以便比较着色器的句柄
        public override bool Equals(object? val) {
            if (val == null || !GetType().Equals(val.GetType())) {
                return false;
            } else {
                Shader t = (Shader)val;
                return _handle == t._handle;
            }
        }

        // 重写GetHashCode方法，使用句柄生成散列码
        public override int GetHashCode() {
            return _handle.GetHashCode();
        }
    }


    /// <summary>
    /// ShaderCompiler 类继承自Shader类 , 用于从源代码字符串创建顶点和片段着色器程序
    /// </summary>
    public class ShaderCompiler : Shader {

        /// <summary>
        /// ShaderCompiler 方法编译读出的 GLSL 代码写的着色器程序
        /// </summary>
        /// <param name="vertSrc">顶点着色器代码</param>
        /// <param name="fragSrc">片段着色器代码</param>
        public ShaderCompiler(string vertSrc , string fragSrc) {
            // 加载顶点着色器并获取其句柄
            int Vertex = LoadShader(ShaderType.VertexShader , vertSrc);
            // 加载片段着色器并获取其句柄
            int Fragment = LoadShader(ShaderType.FragmentShader , fragSrc);

            // 创建着色器程序并获取其句柄
            _handle = GL.CreateProgram();

            // 将顶点和片段着色器附加到着色器程序
            GL.AttachShader(_handle , Vertex);
            GL.AttachShader(_handle , Fragment);

            // 链接着色器程序
            GL.LinkProgram(_handle);

            // 检查链接状态
            GL.GetProgram(_handle , GetProgramParameterName.LinkStatus , out var status);
            if (status == 0) // 链接失败时，抛出异常
            {
                throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(_handle)}");
            }

            // 分离和删除顶点和片段着色器，因为它们现在已经链接到程序，不再需要
            GL.DetachShader(_handle , Vertex);
            GL.DetachShader(_handle , Fragment);
            GL.DeleteShader(Vertex);
            GL.DeleteShader(Fragment);
        }

        // 加载并编译着色器的私有方法
        private static int LoadShader(ShaderType type , string src) {
            int handle = GL.CreateShader(type);
            GL.ShaderSource(handle , src);
            GL.CompileShader(handle);

            // ① 判断编译状态
            GL.GetShader(handle , ShaderParameter.CompileStatus , out int ok);

            // ② 获取完整信息日志（无论成功失败都会有）
            string log = GL.GetShaderInfoLog(handle);
            if (!string.IsNullOrWhiteSpace(log))
                Console.WriteLine($"[Shader] {type} compile log:\n{log}");

            if (ok == 0)
                throw new InvalidOperationException($"{type} compile failed:\n{log}");

            return handle;
        }

    }
}

using Axone.Meshing;
using Axone.Resources;
using Axone.SrcManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axone.Engine {
    public sealed class Renderer : IDisposable {
        public Texture2D? Texture;
        public VertexArrayObject<VertexF>? Vao;
        public GPUBuffer<VertexF>? Vbo;
        public GPUBuffer<uint>? Ebo;
        public GLMesh<VertexF>? Mesh;
        public Shader? Shader;

        /* 法线可视化 */
        public VertexArrayObject<NormalViewF>? VaoNorm;
        public GPUBuffer<NormalViewF>? VboNorm;
        public Shader? NormalShader;

        public ShaderParams Params = new();

        public void Dispose() {
            Texture?.Dispose();
            Vbo?.Dispose(); Ebo?.Dispose();
            Vao?.Dispose();
            VboNorm?.Dispose(); VaoNorm?.Dispose();
            Shader?.Dispose(); NormalShader?.Dispose();
        }
    }

    public sealed class ShaderParams {
        public int Model, View, PMatrix, ZMultiplier;
        public int LightSwitch, LightPos, CamPos, LightColor, Metallic, Shininess;
    }

}

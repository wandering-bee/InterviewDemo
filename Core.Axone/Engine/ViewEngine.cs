using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using Axone.Meshing;                // Reconstruct / Upload
using Axone.Resources;
using Axone.Core;
using Axone.SrcManager;
using OpenTK.GLControl;
using System.Windows.Forms;


namespace Axone.Engine
{

    public sealed class ViewEngine : IDisposable
    {

        /* ────── 视口尺寸 ────── */
        public int Width { get; private set; } = 1;
        public int Height { get; private set; } = 1;

        /* ────── 相机轨道 ────── */
        Vector3 eye = new(0, 0, 3.5f);
        Vector3 target = Vector3.Zero;
        readonly Vector3 up = Vector3.UnitY;

        Matrix4 mView, mProj;

        Color4 clear = new(0.102f, 0.110f, 0.125f, 1f);

        private GLView _Control;

        private int _lastVertCount = -1;
        private int _lastIdxCount = -1;

        /* ────── Z 拉伸因子 ────── */
        float _zMul = 1f;

        /*【方法】ZMultiplier（公开给 UI）*/
        public float ZMultiplier
        {
            get => _zMul;
            set
            {
                _zMul = Math.Clamp(value, 0f, 110f) * 100;
                _Control?.Invalidate();
            }
        }


        /* ────── GPU 资源 ────── */
        readonly Renderer src = new();

        bool disposed;

        /*【方法】Viewport.InitGL */
        void InitGL()
        {
            /* --- 基础渲染态 --- */
            GL.Enable(EnableCap.DepthTest);
            GL.FrontFace(FrontFaceDirection.Ccw);

            /* --- 抗锯齿快餐包 --- */
            GL.Enable(EnableCap.Multisample);             // MSAA
            GL.Enable(EnableCap.SampleAlphaToCoverage);   // 让透明/渐变也走 MSAA
            GL.Hint(HintTarget.MultisampleFilterHintNv,   // NV/Intel 驱动对 MSAA 质量的暗示
                    HintMode.Nicest);
            GL.ClearColor(clear);
        }


        /* ───────── ViewEngine.BindControl（完整替换）───────── */
        public void BindControl(GLView control)
        {
            _Control = control;              // 记住引用
            bool glInitialized = false;      // 防重入

            /* 内联函数：只在第一次调用时真正做 InitGL / Resize */
            void EnsureInit()
            {
                if (glInitialized) return;

                _Control.MakeCurrent();                       // 绑定上下文
                InitGL();                                     // 开启深度、剔除、设 ClearColor
                Resize(_Control.Width, _Control.Height);      // 视口 / 投影
                glInitialized = true;
            }

            /* ① 若控件句柄已创建（窗体 Designer 里早就 Load 过）→ 立刻初始化 */
            if (control.IsHandleCreated)
                EnsureInit();
            else
                /* ② 否则等 Load 事件来调用一次 */
                control.Load += (_, _) => EnsureInit();

            /* ---------- Resize ---------- */
            control.Resize += (_, _) =>
            {
                if (!glInitialized) return;   // 尚未初始化时忽略
                _Control.MakeCurrent();
                Resize(_Control.Width, _Control.Height);
            };

            /* ---------- Paint ---------- */
            control.Paint += (_, _) =>
            {
                if (!glInitialized) return;
                _Control.MakeCurrent();
                Update();
                Draw();
                _Control.SwapBuffers();
            };
        }


        /// <summary>一次性组装 & 上传点云 → GPU（等价旧 Model.Assemble* ）</summary>
        public async Task BuildAsync(
            List<Vector3> cloud,
            ReconstructMode mode = ReconstructMode.Subdiv,
            double tol = 0.1,
            Action<string>? log = null)
        {
            // ⚠️ 未绑定 GLControl
            if (_Control is null || _Control.IsDisposed)
                throw new InvalidOperationException("未绑定 GLControl 控件！");

            /* ---------- 离线重建网格 ---------- */
            src.Mesh = await GLMesh<VertexF>.FromPointCloudAsync(cloud, mode, tol, log)
                                           .ConfigureAwait(false);

            if (src.Mesh.Vertices.Length == 0)
                throw new InvalidOperationException("网格重建失败 ...");

            /* ---------- 回到 GL 线程 ---------- */
            await _Control.InvokeAsync(() =>
            {
                _Control.MakeCurrent();                // 当前线程持有 GL 上下文

                /* ---------- 按需清洗环形缓冲 ---------- */
                bool sizeChanged =
                    src.Mesh.Vertices.Length != _lastVertCount ||
                    (src.Mesh.Indices?.Length ?? 0) != _lastIdxCount;

                if (sizeChanged)
                {
                    GLMeshUpload.ResetPools();         // 规格切换 → 清洗
                    _lastVertCount = src.Mesh.Vertices.Length;
                    _lastIdxCount = src.Mesh.Indices?.Length ?? 0;
                }

                /* ---------- 上传缓冲 ---------- */
                var gpu = src.Mesh.UploadStreamed();   // 这里会自动复用 / 分配

                src.Vao = gpu.Vao;
                src.Vbo = gpu.VPool;
                src.Ebo = gpu.EPool;

                /* ---------- 编译 / 链接 Shader ---------- */
                string vs = File.ReadAllText("GlslShaders/VS_WAFER.glsl");
                string fs = File.ReadAllText("GlslShaders/FS_WAFER.glsl");
                src.Shader = new ShaderCompiler(vs, fs);

                AssignShaderUniformLocations(src.Shader, src.Params);

                /* ---------- 更新矩阵后首帧 ---------- */
                Resize(Width, Height);
                Update();
                _Control.Invalidate();                 // 触发重绘
            });
        }

        /*【方法】平移（屏幕 Δ → 世界）*/
        public void Pan(float dx, float dy)
        {
            Vector3 viewDir = target - eye;
            Vector3 right = Vector3.Normalize(Vector3.Cross(viewDir, up));
            Vector3 upDir = Vector3.Normalize(Vector3.Cross(right, viewDir));

            Vector3 offset = right * dx + upDir * dy;
            eye += offset;
            target += offset;
        }

        /*【方法】缩放（沿视线正向移动）*/
        public void Zoom(float amount)
        {
            Vector3 dir = target - eye;
            float len = dir.Length - amount;
            len = Math.Clamp(len, 0.05f, 100f);     // ⚠️ 距离范围可按需调
            eye = target - dir.Normalized() * len;
        }

        public void Reset()
        {
            eye = new Vector3(0, 0, 3.5f);
            target = Vector3.Zero;
            Update();
        }


        /// <summary>Orbit 相机（鼠标 Δx/Δy 已换算成弧度）</summary>
        public void Orbit(float dxRad, float dyRad)
        {
            Vector3 dir = eye - target;
            dir = Matrix3.CreateRotationY(dxRad) * (Matrix3.CreateRotationX(dyRad) * dir);
            eye = target + dir;
        }

        public void Resize(int w, int h)
        {
            Width = Math.Max(1, w);
            Height = Math.Max(1, h);
            GL.Viewport(0, 0, Width, Height);
            float aspect = (float)Width / Height;
            mProj = Matrix4.CreatePerspectiveFieldOfView(
                        MathHelper.DegreesToRadians(45f), aspect, 0.01f, 100f);
        }

        public void Update() => mView = Matrix4.LookAt(eye, target, up);

        public void Draw()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (src.Shader is null || src.Vao is null) return;

            var sh = src.Shader;
            sh.Use();


            GL.UniformMatrix4(src.Params.View, false, ref mView);
            GL.UniformMatrix4(src.Params.PMatrix, false, ref mProj);
            GL.Uniform1(src.Params.ZMultiplier, _zMul);

            Matrix4 mModel = Matrix4.Identity;
            GL.UniformMatrix4(src.Params.Model, false, ref mModel);

            src.Vao.Bind();

            if (src.Ebo is null)
                GL.DrawArrays(PrimitiveType.Triangles, 0, src.Mesh!.Vertices.Length);
            else
                GL.DrawElements(PrimitiveType.Triangles,
                                src.Mesh!.Indices!.Length,
                                DrawElementsType.UnsignedInt,
                                IntPtr.Zero);
        }

        /* ───────── 私有工具 ───────── */

        static void AssignShaderUniformLocations(Shader sh, ShaderParams p)
        {
            p.Model = sh.GetUniformLocation("cModel");
            p.View = sh.GetUniformLocation("cView");
            p.PMatrix = sh.GetUniformLocation("cPMatrix");
            p.ZMultiplier = sh.GetUniformLocation("zMultiplier");

            p.LightSwitch = sh.GetUniformLocation("LightSwitch");
            p.LightPos = sh.GetUniformLocation("lightPos");
            p.CamPos = sh.GetUniformLocation("camPos");
            p.LightColor = sh.GetUniformLocation("lightColor");
            p.Metallic = sh.GetUniformLocation("metallic");
            p.Shininess = sh.GetUniformLocation("shininess");
        }

        /* ───────── 资源释放 ───────── */
        public void Dispose()
        {
            if (!disposed)
            {
                src.Dispose();
                disposed = true;
            }
        }
    }
}

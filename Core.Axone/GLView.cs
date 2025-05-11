using System.ComponentModel;
using OpenTK.GLControl;
using OpenTK.Windowing.Common;

namespace Axone.Engine
{
    public class GLView : GLControl
    {
        private static readonly GLControlSettings Settings = new()
        {
            // OpenGL 4.6 Core
            API = ContextAPI.OpenGL,
            APIVersion = new Version(4, 6),
            Profile = ContextProfile.Core,

            // 缓冲格式
            // DepthBits = 24,
            // StencilBits = 8,

            // 多重采样
            NumberOfSamples = 16,

        };

        public GLView() : base(Settings) { }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Version APIVersion           // ← 隐藏同名属性
        {
            get => base.APIVersion;
            set                                    // 防御性：丢弃 -1
            {
                if (value.Build < 0 || value.Revision < 0)
                    base.APIVersion = new Version(value.Major, value.Minor);
                else
                    base.APIVersion = value;
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (!IsHandleCreated || !InvokeRequired)   // 已在 UI 线程
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>(
                          TaskCreationOptions.RunContinuationsAsynchronously);

            BeginInvoke((MethodInvoker)(() =>
            {
                try { action(); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            }));

            return tcs.Task;
        }
    }

}



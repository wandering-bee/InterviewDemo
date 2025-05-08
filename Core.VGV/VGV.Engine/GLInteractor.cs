using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axone.Engine
{

    /// <summary>📦 鼠标交互控制器（独立于渲染引擎）</summary>
    public sealed class GLInteractor : IDisposable
    {
        readonly ViewEngine eng;
        readonly GLView view;

        Point last;                 // 上一帧鼠标位置
        MouseButtons btn = MouseButtons.None;

        /* --- 灵敏度（可按需微调）--- */
        const float ROT_SENS = 0.01f;   // 弧度 / 像素
        const float PAN_SENS = 0.005f;  // 世界单位 / 像素
        const float ZOOM_SENS = 0.001f;  // 世界单位 / Delta

        public GLInteractor(ViewEngine engine, GLView control)
        {
            eng = engine;
            view = control;

            view.MouseDown += OnMouseDown;
            view.MouseMove += OnMouseMove;
            view.MouseUp += OnMouseUp;
            view.MouseWheel += OnMouseWheel;
            view.MouseDoubleClick += OnDoubleClick;
        }

        void OnMouseDown(object? _, MouseEventArgs e)
        {
            btn = e.Button;
            last = e.Location;
        }

        void OnMouseMove(object? _, MouseEventArgs e)
        {
            if (btn == MouseButtons.None) return;

            int dx = e.X - last.X;
            int dy = e.Y - last.Y;
            last = e.Location;

            switch (btn)
            {
                case MouseButtons.Left:      // 🔄 轨道旋转
                    eng.Orbit(dx * ROT_SENS, dy * ROT_SENS);
                    break;

                case MouseButtons.Right:     // 🔄 平移
                    eng.Pan(-dx * PAN_SENS, dy * PAN_SENS);
                    break;
            }
            view.Invalidate();               // ✅ 请求重绘
        }

        void OnMouseUp(object? _, MouseEventArgs __) => btn = MouseButtons.None;

        void OnMouseWheel(object? _, MouseEventArgs e)
        {
            eng.Zoom(e.Delta * ZOOM_SENS);   // 🔄 缩放（滚轮向前缩小 distance）
            view.Invalidate();
        }

        void OnDoubleClick(object? _, MouseEventArgs __)
        {
            eng.Reset();
            view.Invalidate();
        }

        public void Dispose()
        {
            view.MouseDown -= OnMouseDown;
            view.MouseMove -= OnMouseMove;
            view.MouseUp -= OnMouseUp;
            view.MouseWheel -= OnMouseWheel;
        }
    }

}

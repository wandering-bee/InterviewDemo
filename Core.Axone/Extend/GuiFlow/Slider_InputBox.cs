using System.Runtime.InteropServices;

namespace Extend {
    /// <summary>
    /// 为 Button + TextBox 提供“滑动切换”扩展
    /// </summary>
    public static class Slider_InputBox {
        /* ---------- 字段 ---------- */
        private static readonly Dictionary<Button, SliderData> sliderMap = new();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int EM_SETMARGINS = 0xd3;
        private const int EC_LEFTMARGIN = 0x1;
        private const int EC_RIGHTMARGIN = 0x2;

        /* ---------- 公开绑定接口 ---------- */

        /// <summary>可滑动版</summary>
        public static void BindSlider(this Button btn, TextBox txt, bool state = false)
            => BindSliderCore(btn, txt, state, enableToggle: true);

        /// <summary>永久锁定版：始终 ON，不可解锁</summary>
        public static void BindSliderLocked(this Button btn, TextBox txt, bool state = true)
            => BindSliderCore(btn, txt, state, enableToggle: false);

        /* ---------- 核心绑定 ---------- */
        private static void BindSliderCore(
            Button btn,
            TextBox txt,
            bool startLocked,
            bool enableToggle)
        {
            if (sliderMap.ContainsKey(btn)) return;

            txt.AutoSize = false;
            txt.Multiline = true;
            txt.Height = btn.Height;
            SetTextBoxPadding(txt, 3, 3);

            var sd = new SliderData
            {
                Btn = btn,
                TBx = txt,
                Locked = startLocked,
                IsAnimating = false,
                Progress = 0f
            };
            sliderMap[btn] = sd;

            InitState(sd, startLocked);

            if (!enableToggle) return;

            btn.Click += async (_, __) =>
            {
                if (sd.IsAnimating) return;

                sd.SourceLocked = sd.Locked;
                sd.Locked = !sd.Locked;
                sd.Progress = 0f;
                sd.IsAnimating = true;

                sd.Btn.Enabled = false;
                sd.TBx.Enabled = false;

                if (!sd.SourceLocked)
                    sd.TBx.Visible = false;

                /* 🔄 启动异步动画 */
                await AnimateAsync(sd);
            };
        }

        /*==========================================================
         * 🧠 AnimateAsync —— 单实例动画协程
         *=========================================================*/
        private static async Task AnimateAsync(SliderData sd)
        {
            const int DURATION_MS = 165;   // 🎚 总动画时长 —— 改这里即可整体加速/减速
            const int FRAME_MS = 15;    // 目标帧间隔
            var startTick = Environment.TickCount;

            while (true)
            {
                // 1. 计算归一化进度
                int elapsed = Environment.TickCount - startTick;
                float t = elapsed / (float)DURATION_MS;
                if (t >= 1f) t = 1f;                 // 钳制
                sd.Progress = t;

                // 2. 根据当前进度插值位置
                bool toLocked = sd.Locked;
                Point btnStart = toLocked ? sd.BtnPos.Unlocked : sd.BtnPos.Locked;
                Point btnEnd = toLocked ? sd.BtnPos.Locked : sd.BtnPos.Unlocked;
                Point txtStart = toLocked ? sd.TBxPos.Unlocked : sd.TBxPos.Locked;
                Point txtEnd = toLocked ? sd.TBxPos.Locked : sd.TBxPos.Unlocked;

                sd.Btn.Location = Lerp(btnStart, btnEnd, t);
                sd.TBx.Location = Lerp(txtStart, txtEnd, t);
                sd.Btn.BringToFront();

                // 3. 结束判断
                if (t >= 1f) break;

                // 4. 让出 UI 线程；帧间隔动态补偿（防止帧跳）
                int nextDelay = FRAME_MS - (Environment.TickCount - startTick - elapsed);
                if (nextDelay < 1) nextDelay = 1;
                await Task.Delay(nextDelay).ConfigureAwait(true);
            }

            /* ✅ 收尾 */
            ApplyLockedState(sd);
            UpdateBadge(sd);
            sd.Btn.Enabled = true;
            sd.IsAnimating = false;
        }


        /* ---------- 位置插值 ---------- */
        private static Point Lerp(Point start, Point end, float t)
        {
            int x = (int)(start.X + (end.X - start.X) * t);
            int y = (int)(start.Y + (end.Y - start.Y) * t);
            return new Point(x, y);
        }

        private static void UpdateBadge(SliderData sd) {
            var badge = sd.Badge;
            if (badge == null) return;

            badge.Text = sd.Locked ? "ON" : "OFF";
            badge.ForeColor = sd.Locked ? ON_LABEL_COLOR : OFF_LABEL_COLOR;

            badge.Location = sd.Locked
                ? new Point(sd.Pane.Right - badge.PreferredWidth - PADDING ,
                            sd.Pane.Top - badge.PreferredHeight - PADDING)
                : new Point(sd.Pane.Left + PADDING ,
                            sd.Pane.Top - badge.PreferredHeight - PADDING);
        }

        private static void InitState(SliderData sd , bool locked) {
            sd.Locked = locked;
            var btn = sd.Btn;
            var txt = sd.TBx;
            var parent = btn.Parent!;

            var union = Rectangle.Union(btn.Bounds , txt.Bounds);
            var pane = new Panel {
                Size = new Size(union.Width + PADDING * 2 ,
                                     union.Height + PADDING * 2) ,
                Location = new Point(union.Left - PADDING ,
                                      union.Top - PADDING) ,
                BackColor = txt.BackColor
            };

            parent.Controls.Remove(btn);
            parent.Controls.Remove(txt);
            parent.Controls.Add(pane);
            pane.Controls.Add(btn);
            pane.Controls.Add(txt);

            Size offset = new(union.Left - PADDING , union.Top - PADDING);
            Point Rel(Point p) => new(p.X - offset.Width , p.Y - offset.Height);

            Point btnRel = Rel(btn.Location);
            Point txtRel = Rel(txt.Location);

            bool buttonIsLeft = btnRel.X < txtRel.X;

            sd.BtnPos = new SliderData.PosPair
            {
                Unlocked = buttonIsLeft ? btnRel : txtRel ,
                Locked = buttonIsLeft ? txtRel : btnRel
            };

            sd.TBxPos = new SliderData.PosPair
            {
                Unlocked = buttonIsLeft ? txtRel : btnRel ,
                Locked = buttonIsLeft ? btnRel : txtRel
            };

            sd.C = new SliderData.Palette {
                PaneBack = PANE_UNLOCK_BACK
            };

            sd.Pane = pane;

            ApplyLockedState(sd);

            var badge = new Label {
                AutoSize = true ,
                Text = locked ? "ON" : "OFF" ,
                ForeColor = locked ? ON_LABEL_COLOR : OFF_LABEL_COLOR ,
                BackColor = Color.Transparent
            };
            parent.Controls.Add(badge);
            badge.BringToFront();

            badge.Location = locked
                ? new Point(pane.Right - badge.PreferredWidth - PADDING ,
                            pane.Top - badge.PreferredHeight - PADDING)
                : new Point(pane.Left + PADDING ,
                            pane.Top - badge.PreferredHeight - PADDING);

            sd.Badge = badge;
        }

        private static void ApplyLockedState(SliderData sd) {
            var p = sd.Locked ? sd.BtnPos.Locked : sd.BtnPos.Unlocked;
            var t = sd.Locked ? sd.TBxPos.Locked : sd.TBxPos.Unlocked;

            sd.Btn.Location = p;
            sd.TBx.Location = t;

            if (sd.Locked)
            {
                sd.TBx.Enabled = false;
                sd.TBx.BackColor = TXT_DISABLED_BACK;
                sd.TBx.ForeColor = TXT_DISABLED_FORE;

                sd.Btn.BackColor = BTN_LOCKED_BACK;
                sd.Btn.ForeColor = BTN_LOCKED_FORE;

                sd.Pane.BackColor = PANE_LOCKED_BACK;
            } else
              {
                sd.TBx.Enabled = true;
                sd.TBx.BackColor = TXT_ENABLED_BACK;
                sd.TBx.ForeColor = TXT_ENABLED_FORE;

                sd.Btn.BackColor = BTN_UNLOCK_BACK;
                sd.Btn.ForeColor = BTN_UNLOCK_FORE;

                sd.Pane.BackColor = PANE_UNLOCK_BACK;
            }

            sd.TBx.Visible = true;
        }

        // 设置 TextBox 左右内边距
        private static void SetTextBoxPadding(TextBox txt, int left, int right)
        {
            int lParam = (left & 0xFFFF) | (right << 16);
            SendMessage(txt.Handle, EM_SETMARGINS,
                (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)lParam);
        }

        /// ========= 数据结构 =========
        private sealed class SliderData {
            public Button Btn;     // 按钮
            public TextBox TBx;     // 文本框
            public Panel Pane;    // 基底面板
            public Label Badge;   // ON / OFF 角标

            public bool IsAnimating;     // 是否动画中

            // ① 位置 (相对 Pane 左上角)
            public struct PosPair {
                public Point Unlocked;
                public Point Locked;
            }

            public PosPair BtnPos, TBxPos;

            //public struct Pos {
            //    public Point BtnUnlocked, BtnLocked;
            //    public Point TxtUnlocked, TxtLocked;
            //}
            //public Pos P;

            public struct Palette {
                public Color PaneBack;
            }
            public Palette C;

            public bool Locked;         // 目标状态：true = ON
            public bool SourceLocked;   // 动画起点状态
            public float Progress;       // 0 → 1，动画进度
        }

        /// ========= 常量 =========

        private const int PADDING = 2;
        private static readonly Color ON_LABEL_COLOR = Color.Green;
        private static readonly Color OFF_LABEL_COLOR = Color.Red;
        private static readonly Color BTN_LOCKED_BACK = ColorTranslator.FromHtml("#66BB6A");
        private static readonly Color BTN_UNLOCK_BACK = Color.FromArgb(130, 135, 140);
        private static readonly Color BTN_LOCKED_FORE = Color.White;
        private static readonly Color BTN_UNLOCK_FORE = SystemColors.ControlText;
        private static readonly Color TXT_ENABLED_BACK = Color.White;
        private static readonly Color TXT_ENABLED_FORE = SystemColors.ControlText;
        private static readonly Color TXT_DISABLED_BACK = ColorTranslator.FromHtml("#D0E7FF");
        private static readonly Color TXT_DISABLED_FORE = ColorTranslator.FromHtml("#606060");
        private static readonly Color PANE_LOCKED_BACK = ColorTranslator.FromHtml("#2E7D32");
        private static readonly Color PANE_UNLOCK_BACK = TXT_ENABLED_BACK;
    }
}

using System.Diagnostics;
using SYSPoint = System.Drawing.Point;

namespace Extend.GuiFlow
{
    public static class BS {

        private static bool isDragging;
        private static SYSPoint mouseDownPosition;

        /// <summary>
        /// 绑定主程序界面
        /// </summary>
        /// <param name="cForm">通常直接传入 this</param>
        /// <param name="cBackground">作为主界面的 panel</param>
        /// <param name="isOld">是否使用旧版本逻辑 , 默认不使用</param>
        public static void BindBackground(Form cForm, Panel cBackground, bool isOld = false) {

            BindDraggable(cForm, cBackground);

            cBackground.Enabled = true;
            cBackground.TabStop = true;

            cBackground.Click += (sender, e) => {
                cBackground.Focus();
            };

            if (isOld) {
                cForm.Activated += (sender, e) => cBackground.BackColor = Color.OrangeRed;
                cForm.Deactivate += (sender, e) => cBackground.BackColor = Color.FromArgb(40, 45, 50);
            }
            else {
                cForm.Activated += (sender, e) => cForm.BackColor = Color.FromArgb(85, 15, 150);
                cForm.Deactivate += (sender, e) => cForm.BackColor = Color.FromArgb(192, 192, 192);
            }
        }

        /// <summary>
        /// 绑定程序的关闭按钮
        /// </summary>
        /// <param name="cForm">通常直接传入 this</param>
        /// <param name="closeButton">关闭按钮的实例</param>
        public static void BindCloseButton(Form cForm, Button closeButton) {
            closeButton.Click += (sender, e) => {
                cForm.Close();
            };
        }

        /// <summary>
        /// 绑定程序窗口拖动功能
        /// </summary>
        /// <param name="form">通常直接传入 this</param>
        /// <param name="controls">需要绑定的控件们</param>
        public static void BindDraggable(Form form, params Control[] controls) {
            foreach (var control in controls) {
                control.MouseDown += (sender, e) =>
                {
                    isDragging = true;
                    mouseDownPosition = Control.MousePosition;
                };

                control.MouseMove += (sender, e) =>
                {
                    if (isDragging) {
                        var currentMousePosition = Control.MousePosition;
                        form.Location = new SYSPoint(
                            form.Left + currentMousePosition.X - mouseDownPosition.X,
                            form.Top + currentMousePosition.Y - mouseDownPosition.Y
                        );
                        mouseDownPosition = currentMousePosition;
                    }
                };

                control.MouseUp += (sender, e) =>
                {
                    isDragging = false;
                };
            }
        }

        private static Button? LastSelected = null;
        public static void BindButtons(params Button[] buttons) {
            foreach (var button in buttons) {
                button.Click += (sender, e) =>
                {
                    if (sender is Button btn) {
                        if (LastSelected != null) {
                            LastSelected.BackColor = Color.FromArgb(224, 224, 224) ;
                        }

                        btn.BackColor = Color.PaleGreen;
                        LastSelected = btn;
                    }
                };
            }
        }

        public static void DisableButtonFocus(params Button[] buttons) {
            foreach (var button in buttons) {
                button.GotFocus += (sender, e) =>
                {
                    if (sender is Button btn) {
                        btn.NotifyDefault(false);
                    }
                };

                button.LostFocus += (sender, e) =>
                {
                    if (sender is Button btn) {
                        btn.NotifyDefault(false);
                    }
                };
            }
        }



        /// <summary>
        /// 动态生成按钮并添加到指定容器中
        /// </summary>
        /// <param name="container">容器控件，如Panel或Form</param>
        /// <param name="PreText">按钮文本的前缀</param>
        /// <param name="count">要生成的按钮数量</param>
        /// <param name="unitSize">每个按钮的尺寸</param>
        public static void GenBtnList(Control container, string PreText, int count, Size unitSize) {
            int margin = 2;
            int Xpos = 3;
            int totalHeight = 0;

            Font buttonFont = new("Microsoft YaHei", 10, FontStyle.Regular, GraphicsUnit.Point);

            container.Controls.Clear();

            Button? LastSelected = null;

            for (int i = 0; i < count; i++) {
                Button nb = new Button {
                    Text = $"{PreText} {i + 1}",
                    Size = unitSize,
                    Location = new SYSPoint(Xpos, i * (unitSize.Height + margin)+ margin),
                    FlatStyle = FlatStyle.Flat,
                    Font = buttonFont,
                    BackColor = Color.White,
                    FlatAppearance = { BorderSize = 0 }
                };

                nb.Click += (sender, e) => {
                    if (sender is Button btn) {
                        if (LastSelected != null) {
                            LastSelected.BackColor = Color.White;
                        }
                        btn.BackColor = Color.LightBlue;
                        LastSelected = btn;
                    }
                };

                nb.GotFocus += (sender, e) => {
                    if (sender is Button btn) {
                        btn.NotifyDefault(false);
                    }
                };
                nb.LostFocus += (sender, e) => {
                    if (sender is Button btn) {
                        btn.NotifyDefault(false);
                    }
                };

                container.Controls.Add(nb);
                totalHeight = nb.Bottom + margin;
            }

            container.Size = new Size(unitSize.Width + Xpos * 2, totalHeight);
            Debug.WriteLine(totalHeight);
        }


        /// <summary>
        /// 创建一个Panel，位于控件底部使其作为控件的边框。
        /// </summary>
        /// <param name="CT">参考控件</param>
        /// <param name="Argb">边框颜色，默认为黑色</param>
        /// <param name="bw">边框宽度，默认为3像素</param>
        public static void AddPnBorder(this Control CT, Color Argb , int bw = 3) {
            Panel border = new() {
                Size = new Size(CT.Width + 2 * bw, CT.Height + 2 * bw),
                Location = new SYSPoint(CT.Location.X - bw, CT.Location.Y - bw),
                BackColor = Argb
            };

            if (CT.Parent != null) {
                CT.Parent.Controls.Add(border);
                border.SendToBack();
            }

            border.Controls.Add(CT);
            CT.Location = new SYSPoint(bw, bw);
        }


    } // EndClass
} // EndName

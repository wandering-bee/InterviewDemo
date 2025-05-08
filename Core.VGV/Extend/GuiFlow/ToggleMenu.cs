using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extend.GuiFlow
{
    public class ToggleMenu
    {
        public Panel PanelBox;
        public bool IsCollapsed;
        public int AnimationStep;
        public int FrameDelayMs = 15; // 约 60 FPS
        public bool IsAnimating = false;
        public ToggleMenu? AssociatedMenuControl { get; set; }

        public ToggleMenu(Panel panelBox, int step = 20)
        {
            PanelBox = panelBox;
            AnimationStep = step;
            IsCollapsed = panelBox.Height == panelBox.MinimumSize.Height;
        }

        public ToggleMenu()
        {
            PanelBox = new Panel();
        }

        public async void Toggle()
        {
            if (IsAnimating) return;
            IsAnimating = true;

            int targetHeight = IsCollapsed ? PanelBox.MaximumSize.Height : PanelBox.MinimumSize.Height;
            int direction = IsCollapsed ? 1 : -1;

            while ((direction > 0 && PanelBox.Height < targetHeight) ||
                   (direction < 0 && PanelBox.Height > targetHeight))
            {

                PanelBox.Height += direction * AnimationStep;

                // Clamp
                if ((direction > 0 && PanelBox.Height > targetHeight) ||
                    (direction < 0 && PanelBox.Height < targetHeight))
                {
                    PanelBox.Height = targetHeight;
                }

                // 更新关联控件的位置
                if (AssociatedMenuControl != null)
                {
                    AssociatedMenuControl.PanelBox.Top = PanelBox.Bottom;
                }

                await Task.Delay(FrameDelayMs);
            }

            IsCollapsed = !IsCollapsed;
            IsAnimating = false;
        }
    }

}

using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Extend
{
    public static class TextBoxBiud
    {
        public static async void UpdateTbStatus(this Microsoft.UI.Xaml.Controls.TextBox TBx, Slider slider, Color color)
        {
            // 获取 Hex 颜色
            SolidColorBrush activeColor = new(Color.FromArgb(color.A, color.R, color.G, color.B));

            // 处理输入框的状态
            if (slider.Value == slider.Maximum)
            {
                TBx.IsEnabled = false;  // 禁用控件的所有交互功能
                TBx.IsTabStop = false;  // 禁止从导航获得焦点
                TBx.IsReadOnly = true;  // 锁定输入框
                await Task.Delay(50);
                TBx.Foreground = new SolidColorBrush(Colors.Black); // ⚡字体颜色设为黑色！
                TBx.Background = activeColor; // 激活颜色
                slider.Foreground = activeColor;
                slider.ApplyTemplate();
            }
            else
            {
                TBx.IsEnabled = true;    // 恢复交互功能
                TBx.IsTabStop = true;    // 恢复从导航获得焦点
                TBx.IsReadOnly = false;  // 解锁输入框
                await Task.Delay(50);
                TBx.Background = null;
                TBx.Foreground = null;
                slider.Foreground = null;
                slider.ApplyTemplate();
            }

        }


    }
}

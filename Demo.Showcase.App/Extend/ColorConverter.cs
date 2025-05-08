using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Extend
{
    public static class ColorConverter
    {
        public static Color GetColor(this string hex)
        {
            hex = hex.Replace("#", string.Empty);

            byte a = 255; // 默认不透明
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }
    }
}

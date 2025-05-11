using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Extend;

internal static partial class Win32
{
    internal static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);

    internal const int WS_POPUP = unchecked((int)0x80000000);
    internal const int WS_CAPTION_MASK = 0x00CF0000;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNA = 8;

    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal const int GWL_STYLE = -16;
    internal const int WS_CHILD = 0x40000000;
    internal const int WS_VISIBLE = 0x10000000;

    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_FRAMECHANGED = 0x0020;
    internal const int SW_SHOW = 5;
    internal const int WS_CLIPSIBLINGS = 0x04000000;

    internal struct POINT { public int X, Y; }
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = false)]
    internal static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")] internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int n);
    [DllImport("user32.dll")] internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int n, IntPtr v);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hInsAfter, int X, int Y, int W, int H, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc lp, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    internal delegate bool EnumProc(IntPtr hWnd, IntPtr l);
}

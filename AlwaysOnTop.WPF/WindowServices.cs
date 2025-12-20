using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AlwaysOnTop.WPF
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsTopMost { get; set; }
    }

    public static class WindowServices
    {
        #region P/Invoke

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        #endregion

        public static List<WindowInfo> GetVisibleWindows()
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);
                    var title = sb.ToString();

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        windows.Add(new WindowInfo
                        {
                            Handle = hWnd,
                            Title = title,
                            IsTopMost = IsTopMost(hWnd)
                        });
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public static bool IsTopMost(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            return (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;
        }

        public static void SetTopMost(IntPtr hWnd, bool enable)
        {
            SetWindowPos(hWnd, enable ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }
}

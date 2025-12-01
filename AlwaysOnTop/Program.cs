using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AlwaysOnTop
{
    class Program
    {
        #region Windows API 声明

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // SetWindowPos 参数
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        // GetWindowLong 参数
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        #endregion

        // 存储已置顶的窗口
        private static readonly HashSet<IntPtr> _pinnedWindows = new HashSet<IntPtr>();
        private static AppConfig _config;
        private static GlobalHotkey _globalHotkey;

        static void Main(string[] args)
        {
            // 加载配置
            _config = AppConfig.Load("config.json");

            // 初始化和注册全局热键
            _globalHotkey = new GlobalHotkey();
            _globalHotkey.OnPinHotkey += () => PinForegroundWindowImpl(delay: false);
            _globalHotkey.OnUnpinHotkey += () => UnpinForegroundWindowImpl(delay: false);

            Console.Title = "Window Pin Tool - 窗口置顶工具";
            Console.WriteLine("===========================================");
            Console.WriteLine("       窗口置顶工具 (Window Pin Tool)");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            // 输出快捷键配置
            Console.WriteLine("快捷键配置:");
            Console.WriteLine($"  置顶窗口:     {_config.Hotkeys.Pin.Modifiers} + {_config.Hotkeys.Pin.Key}");
            Console.WriteLine($"  取消置顶:     {_config.Hotkeys.Unpin.Modifiers} + {_config.Hotkeys.Unpin.Key}");
            Console.WriteLine();

            // 尝试注册全局热键
            if (_globalHotkey.Register(_config.Hotkeys.Pin, _config.Hotkeys.Unpin))
            {
                Console.WriteLine("✓ 全局热键注册成功！");
                Console.WriteLine("  可以在后台使用快捷键操作窗口置顶。");
                Console.WriteLine();

                // 启动热键监听线程
                var hotkeyThread = new System.Threading.Thread(() => _globalHotkey.StartListening())
                {
                    IsBackground = true,
                    Name = "HotkeyListener"
                };
                hotkeyThread.Start();
            }
            else
            {
                Console.WriteLine("✗ 全局热键注册失败，仅可通过菜单操作。");
                Console.WriteLine();
            }

            Console.WriteLine("命令列表:");
            Console.WriteLine("  1. 置顶当前活动窗口");
            Console.WriteLine("  2. 取消置顶当前活动窗口");
            Console.WriteLine("  3. 列出所有可见窗口");
            Console.WriteLine("  4. 按窗口编号置顶/取消置顶");
            Console.WriteLine("  5. 列出已置顶的窗口");
            Console.WriteLine("  6. 取消所有置顶");
            Console.WriteLine("  0. 退出");
            Console.WriteLine();

            while (true)
            {
                Console.Write("\n请输入命令 (0-6): ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        PinForegroundWindow();
                        break;
                    case "2":
                        UnpinForegroundWindow();
                        break;
                    case "3":
                        ListAllWindows();
                        break;
                    case "4":
                        TogglePinByIndex();
                        break;
                    case "5":
                        ListPinnedWindows();
                        break;
                    case "6":
                        UnpinAllWindows();
                        break;
                    case "0":
                        Console.WriteLine("再见！");
                        _globalHotkey?.Unregister();
                        return;
                    default:
                        Console.WriteLine("无效命令，请重新输入。");
                        break;
                }
            }
        }

        /// <summary>
        /// 判断窗口是否为置顶状态
        /// </summary>
        private static bool IsTopmost(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            return (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;
        }

        /// <summary>
        /// 置顶窗口
        /// </summary>
        private static bool PinWindow(IntPtr hWnd)
        {
            bool result = SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            if (result)
            {
                _pinnedWindows.Add(hWnd);
                Console.WriteLine($"✓ 窗口已置顶: {GetWindowTitle(hWnd)}");
            }
            else
            {
                Console.WriteLine($"✗ 置顶失败，错误码: {Marshal.GetLastWin32Error()}");
            }
            return result;
        }

        /// <summary>
        /// 取消置顶窗口
        /// </summary>
        private static bool UnpinWindow(IntPtr hWnd)
        {
            bool result = SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            if (result)
            {
                _pinnedWindows.Remove(hWnd);
                Console.WriteLine($"✓ 窗口已取消置顶: {GetWindowTitle(hWnd)}");
            }
            else
            {
                Console.WriteLine($"✗ 取消置顶失败，错误码: {Marshal.GetLastWin32Error()}");
            }
            return result;
        }

        /// <summary>
        /// 切换窗口置顶状态
        /// </summary>
        private static void TogglePin(IntPtr hWnd)
        {
            if (IsTopmost(hWnd))
            {
                UnpinWindow(hWnd);
            }
            else
            {
                PinWindow(hWnd);
            }
        }

        /// <summary>
        /// 获取窗口标题
        /// </summary>
        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return "(无标题)";

            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>
        /// 获取窗口所属进程名
        /// </summary>
        private static string GetProcessName(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return "(未知)";
            }
        }

        /// <summary>
        /// 置顶当前活动窗口
        /// </summary>
        private static void PinForegroundWindow()
        {
            PinForegroundWindowImpl(delay: true);
        }

        /// <summary>
        /// 置顶当前活动窗口的实现
        /// </summary>
        private static void PinForegroundWindowImpl(bool delay = false)
        {
            if (delay)
            {
                Console.WriteLine("\n请在3秒内切换到要置顶的窗口...");
                System.Threading.Thread.Sleep(3000);
            }

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("✗ 无法获取当前活动窗口");
                return;
            }

            PinWindow(hWnd);
        }

        /// <summary>
        /// 取消置顶当前活动窗口
        /// </summary>
        private static void UnpinForegroundWindow()
        {
            UnpinForegroundWindowImpl(delay: true);
        }

        /// <summary>
        /// 取消置顶当前活动窗口的实现
        /// </summary>
        private static void UnpinForegroundWindowImpl(bool delay = false)
        {
            if (delay)
            {
                Console.WriteLine("\n请在3秒内切换到要取消置顶的窗口...");
                System.Threading.Thread.Sleep(3000);
            }

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("✗ 无法获取当前活动窗口");
                return;
            }

            UnpinWindow(hWnd);
        }

        /// <summary>
        /// 列出所有可见窗口
        /// </summary>
        private static List<IntPtr> ListAllWindows()
        {
            var windows = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                int titleLength = GetWindowTextLength(hWnd);
                if (titleLength == 0) return true;

                windows.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            Console.WriteLine("\n可见窗口列表:");
            Console.WriteLine("-----------------------------------------------");
            for (int i = 0; i < windows.Count; i++)
            {
                var hWnd = windows[i];
                string title = GetWindowTitle(hWnd);
                string processName = GetProcessName(hWnd);
                string pinnedMark = IsTopmost(hWnd) ? " [置顶]" : "";

                // 截断过长的标题
                if (title.Length > 40)
                    title = title.Substring(0, 37) + "... ";

                Console.WriteLine($"  {i + 1,3}. [{processName}] {title}{pinnedMark}");
            }
            Console.WriteLine("-----------------------------------------------");

            return windows;
        }

        /// <summary>
        /// 按窗口编号置顶/取消置顶
        /// </summary>
        private static void TogglePinByIndex()
        {
            var windows = ListAllWindows();

            Console.Write("\n请输入窗口编号: ");
            if (int.TryParse(Console.ReadLine(), out int index) && index >= 1 && index <= windows.Count)
            {
                TogglePin(windows[index - 1]);
            }
            else
            {
                Console.WriteLine("✗ 无效的窗口编号");
            }
        }

        /// <summary>
        /// 列出已置顶的窗口
        /// </summary>
        private static void ListPinnedWindows()
        {
            // 清理已关闭的窗口
            _pinnedWindows.RemoveWhere(hWnd => !IsWindowVisible(hWnd));

            if (_pinnedWindows.Count == 0)
            {
                Console.WriteLine("\n当前没有已置顶的窗口。");
                return;
            }

            Console.WriteLine("\n已置顶的窗口:");
            Console.WriteLine("-----------------------------------------------");
            int i = 1;
            foreach (var hWnd in _pinnedWindows)
            {
                string title = GetWindowTitle(hWnd);
                string processName = GetProcessName(hWnd);
                Console.WriteLine($"  {i++}. [{processName}] {title}");
            }
            Console.WriteLine("-----------------------------------------------");
        }

        /// <summary>
        /// 取消所有置顶
        /// </summary>
        private static void UnpinAllWindows()
        {
            if (_pinnedWindows.Count == 0)
            {
                Console.WriteLine("\n当前没有已置顶的窗口。");
                return;
            }

            Console.WriteLine("\n正在取消所有窗口置顶...");
            foreach (var hWnd in _pinnedWindows.ToArray())
            {
                UnpinWindow(hWnd);
            }
        }
    }
}
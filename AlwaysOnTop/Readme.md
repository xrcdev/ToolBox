# AlwaysOnTop - 窗口置顶工具

一个简单的 Windows 窗口置顶工具，使用 C# 实现，灵感来源于 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 的 AlwaysOnTop 模块。

## 功能特性

- ✅ 置顶/取消置顶当前活动窗口
- ✅ 列出所有可见窗口
- ✅ 按编号选择窗口进行置顶操作
- ✅ 查看已置顶窗口列表
- ✅ 一键取消所有置顶

## 核心原理

使用 Windows API `SetWindowPos` 函数，配合 `HWND_TOPMOST` 标志来设置窗口的 Z-Order：

```csharp
// 置顶窗口
SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

// 取消置顶
SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
```

## 系统要求

- Windows 10/11
- .NET 8.0 或更高版本

## 快速开始

### 编译运行

```bash
git clone https://github. com/xrcdev/AlwaysOnTop.git
cd AlwaysOnTop/src/WindowPinTool
dotnet run
```

### 使用方法

运行程序后，根据菜单选择操作：

```
命令列表:
  1. 置顶当前活动窗口
  2. 取消置顶当前活动窗口
  3. 列出所有可见窗口
  4.  按窗口编号置顶/取消置顶
  5. 列出已置顶的窗口
  6. 取消所有置顶
  0. 退出
```

## 项目结构

```
AlwaysOnTop/
├── README.md
├── LICENSE
├── . gitignore
└── src/
    └── WindowPinTool/
        ├── WindowPinTool. csproj
        ├── Program. cs
        └── app.manifest
```

## 参考资料

- [Microsoft PowerToys - AlwaysOnTop](https://github.com/microsoft/PowerToys/tree/main/src/modules/alwaysontop)
- [SetWindowPos function (winuser.h)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)


 
## License

MIT License




根据 PowerToys 的 Always On Top 模块实现原理，我来帮你用 C# 实现一个简单的窗口置顶工具。

## 核心原理

PowerToys 的窗口置顶核心是使用 Windows API `SetWindowPos` 函数，配合 `HWND_TOPMOST` 标志来设置窗口的 Z-Order：

```cpp
// 来自 PowerToys AlwaysOnTop. cpp
SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);  // 置顶
SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE); // 取消置顶
```

## C# 实现

下面是一个完整的 C# 窗口置顶工具实现：

```csharp name=WindowPinTool/Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowPinTool
{
    class Program
    {
        #region Windows API 声明

        [DllImport("user32. dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32. dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32. dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32. dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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

        static void Main(string[] args)
        {
            Console.Title = "Window Pin Tool - 窗口置顶工具";
            Console.WriteLine("===========================================");
            Console.WriteLine("       窗口置顶工具 (Window Pin Tool)");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("命令列表:");
            Console.WriteLine("  1. 置顶当前活动窗口");
            Console.WriteLine("  2. 取消置顶当前活动窗口");
            Console.WriteLine("  3. 列出所有可见窗口");
            Console.WriteLine("  4. 按窗口编号置顶/取消置顶");
            Console. WriteLine("  5.  列出已置顶的窗口");
            Console.WriteLine("  6. 取消所有置顶");
            Console. WriteLine("  0. 退出");
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
                return process. ProcessName;
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
            Console.WriteLine("\n请在3秒内切换到要置顶的窗口...");
            System.Threading.Thread. Sleep(3000);

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
            Console.WriteLine("\n请在3秒内切换到要取消置顶的窗口...");
            System.Threading.Thread. Sleep(3000);

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr. Zero)
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
                if (! IsWindowVisible(hWnd)) return true;

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
                if (title. Length > 40)
                    title = title. Substring(0, 37) + "... ";

                Console. WriteLine($"  {i + 1,3}. [{processName}] {title}{pinnedMark}");
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
            if (int.TryParse(Console. ReadLine(), out int index) && index >= 1 && index <= windows.Count)
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
            _pinnedWindows. RemoveWhere(hWnd => ! IsWindowVisible(hWnd));

            if (_pinnedWindows. Count == 0)
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
            Console. WriteLine("-----------------------------------------------");
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
            foreach (var hWnd in _pinnedWindows. ToArray())
            {
                UnpinWindow(hWnd);
            }
        }
    }
}
```

```xml name=WindowPinTool/WindowPinTool. csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
</Project>
```

```xml name=WindowPinTool/app.manifest
<? xml version="1. 0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm. v1">
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- 以管理员权限运行可置顶更多窗口（包括提升权限的窗口） -->
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

## 使用方法

1. 创建项目文件夹并保存上述文件
2. 运行 `dotnet build` 编译
3. 运行程序，根据菜单选择操作

## 核心 API 说明

| API | 用途 |
|-----|------|
| `SetWindowPos(hWnd, HWND_TOPMOST, ...)` | 将窗口设置为置顶状态 |
| `SetWindowPos(hWnd, HWND_NOTOPMOST, ...)` | 取消窗口置顶状态 |
| `GetWindowLong(hWnd, GWL_EXSTYLE)` | 检查窗口是否已置顶 (`WS_EX_TOPMOST`) |
| `GetForegroundWindow()` | 获取当前活动窗口句柄 |
| `EnumWindows()` | 枚举所有顶层窗口 |

## 扩展建议

如需更完整的功能，可参考 PowerToys 实现添加：
- **全局热键**：使用 `RegisterHotKey` 注册快捷键（如 Win+Ctrl+T）
- **边框高亮**：为置顶窗口绘制彩色边框（参考 [`WindowBorder`](https://github. com/microsoft/PowerToys/tree/main/src/modules/alwaysontop/AlwaysOnTop) 类）
- **声音反馈**：置顶/取消时播放提示音
- **排除应用**：支持配置不允许置顶的应用列表

> **注意**：搜索结果可能不完整，仅显示了 10 个结果。[点击此处查看更多 AlwaysOnTop 相关代码](https://github.com/search?q=repo%3Amicrosoft%2FPowerToys+AlwaysOnTop&type=code)
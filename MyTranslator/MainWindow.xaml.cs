using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Tmt.V20180321;
using TencentCloud.Tmt.V20180321.Models;
using WinForms = System.Windows.Forms;

namespace MyTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int HotKeyId = 9001;
        private const byte VkControl = 0x11;
        private const byte VkMenu = 0x12;
        private const byte VkC = 0x43;
        private const byte VkShift = 0x10;
        private const byte VkLWin = 0x5B;
        private const byte VkRWin = 0x5C;
        private const uint KeyEventKeyUp = 0x0002;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        // UIA 向上查找支持 TextPattern 祖先节点的最大层数
        private const int MaxUiAutomationAncestorDepth = 5;
        private const int UiAutomationTimeoutMs = 800;
        // Chromium 系浏览器首次 UIA 查询只负责触发其无障碍树激活，需重试一次
        private const int UiAutomationMaxAttempts = 2;
        private const int UiAutomationRetryDelayMs = 300;
        private const int ModifierReleaseTimeoutMs = 600;
        private const int ClipboardCopyTimeoutMs = 1200;
        // 腾讯云 TextTranslate 单次请求上限约 2000 字符，留出余量
        private const int MaxSourceTextLength = 1800;

        private uint _currentModifiers;
        private uint _currentVirtualKey;
        private bool _isTranslating;
        private string _lastCaptureMethod = string.Empty;
        private WinForms.NotifyIcon? _notifyIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private Drawing.Icon? _appIcon;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            SetDefaultHotKeyEditor();
            SetStatus("就绪。默认快捷键：Ctrl+Shift+1");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);

            ApplyHotKeyRegistration();
        }

        private void SetDefaultHotKeyEditor()
        {
            CtrlCheckBox.IsChecked = true;
            ShiftCheckBox.IsChecked = true;
            AltCheckBox.IsChecked = false;
            WinCheckBox.IsChecked = false;
            KeyTextBox.Text = "1";
        }

        private void InitializeTrayIcon()
        {
            _appIcon = CreateApplicationIcon();
            Icon = CreateWindowIconSource(_appIcon);

            _trayMenu = new WinForms.ContextMenuStrip();
            _trayMenu.Items.Add("打开", null, (_, _) => RestoreFromTray());
            _trayMenu.Items.Add("退出", null, (_, _) => ExitFromTray());

            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "MyTranslator",
                Icon = _appIcon,
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        }

        private void ApplyHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyHotKeyRegistration();
        }

        private void ApplyHotKeyRegistration()
        {
            if (!TryGetHotKeySettings(out var modifiers, out var virtualKey, out var displayText))
            {
                return;
            }

            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero)
            {
                _currentModifiers = modifiers;
                _currentVirtualKey = virtualKey;
                SetStatus($"快捷键已设置为：{displayText}");
                return;
            }

            WindowServices.UnregisterHotKey(helper.Handle, HotKeyId);

            if (!WindowServices.RegisterHotKey(helper.Handle, HotKeyId, modifiers, virtualKey))
            {
                SetStatus($"注册快捷键失败：{displayText} 可能已被占用。");
                System.Windows.MessageBox.Show(this, $"注册快捷键失败：{displayText} 可能已被其他程序占用。", "提示");
                return;
            }

            _currentModifiers = modifiers;
            _currentVirtualKey = virtualKey;
            SetStatus($"快捷键已注册：{displayText}");
        }

        private bool TryGetHotKeySettings(out uint modifiers, out uint virtualKey, out string displayText)
        {
            modifiers = 0;
            virtualKey = 0;
            displayText = string.Empty;

            if (CtrlCheckBox.IsChecked == true)
            {
                modifiers |= ModControl;
            }
            if (ShiftCheckBox.IsChecked == true)
            {
                modifiers |= ModShift;
            }
            if (AltCheckBox.IsChecked == true)
            {
                modifiers |= ModAlt;
            }
            if (WinCheckBox.IsChecked == true)
            {
                modifiers |= ModWin;
            }

            if (modifiers == 0)
            {
                SetStatus("请至少选择一个快捷键修饰键。");
                return false;
            }

            var keyText = KeyTextBox.Text.Trim().ToUpperInvariant();
            if (!TryParseVirtualKey(keyText, out virtualKey))
            {
                SetStatus("按键无效，请输入单个字母、数字或 F1-F12。");
                return false;
            }

            displayText = BuildHotKeyDisplayText(modifiers, keyText);
            return true;
        }

        private static string BuildHotKeyDisplayText(uint modifiers, string keyText)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & ModControl) != 0)
            {
                parts.Add("Ctrl");
            }
            if ((modifiers & ModShift) != 0)
            {
                parts.Add("Shift");
            }
            if ((modifiers & ModAlt) != 0)
            {
                parts.Add("Alt");
            }
            if ((modifiers & ModWin) != 0)
            {
                parts.Add("Win");
            }

            parts.Add(keyText);
            return string.Join("+", parts);
        }

        private static bool TryParseVirtualKey(string keyText, out uint virtualKey)
        {
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(keyText))
            {
                return false;
            }

            if (keyText.Length == 1)
            {
                var c = keyText[0];
                if (char.IsLetterOrDigit(c))
                {
                    virtualKey = c;
                    return true;
                }
            }

            if (keyText.StartsWith("F", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<Key>(keyText, true, out var functionKey)
                && functionKey >= Key.F1
                && functionKey <= Key.F24)
            {
                virtualKey = (uint)KeyInterop.VirtualKeyFromKey(functionKey);
                return true;
            }

            if (Enum.TryParse<Key>(keyText, true, out var key))
            {
                virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                return virtualKey != 0;
            }

            return false;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WindowServices.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
            {
                _ = TranslateSelectedTextAsync();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private async Task TranslateSelectedTextAsync()
        {
            if (_isTranslating)
            {
                SetStatus("正在翻译中，请稍后。");
                return;
            }

            _isTranslating = true;
            try
            {
                // 取词必须在本窗口置前之前完成，否则焦点被抢走后
                // UIA 读到的和模拟 Ctrl+C 发往的都会是翻译器自己
                SetStatus("正在读取选中文本...");
                var selectedText = await CaptureSelectedTextAsync();
                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    SetStatus("未读取到选中文本：UI 自动化与模拟复制均未成功。");
                    _notifyIcon?.ShowBalloonTip(1500, "MyTranslator", "未读取到选中文本，请先选中内容。", WinForms.ToolTipIcon.Warning);
                    return;
                }

                selectedText = selectedText.Trim();
                if (selectedText.Length > MaxSourceTextLength)
                {
                    selectedText = selectedText[..MaxSourceTextLength];
                    SetStatus($"选中文本过长，已截断为 {MaxSourceTextLength} 字符，正在调用翻译接口...");
                }
                else
                {
                    SetStatus("正在调用翻译接口...");
                }

                SelectedTextTextBox.Text = selectedText;

                var translatedText = await Task.Run(() => TranslateText(selectedText));
                TranslatedTextTextBox.Text = translatedText;
                SetStatus($"翻译完成。（取词方式：{_lastCaptureMethod}）");

                BringWindowToForeground();
            }
            catch (Exception ex)
            {
                SetStatus($"翻译失败：{ex.Message}");
                _notifyIcon?.ShowBalloonTip(2000, "MyTranslator", $"翻译失败：{ex.Message}", WinForms.ToolTipIcon.Error);
            }
            finally
            {
                _isTranslating = false;
            }
        }

        private async Task<string?> CaptureSelectedTextAsync()
        {
            for (var attempt = 1; attempt <= UiAutomationMaxAttempts; attempt++)
            {
                var uiAutomationText = await GetSelectedTextByUiAutomationAsync();
                if (!string.IsNullOrWhiteSpace(uiAutomationText))
                {
                    _lastCaptureMethod = "UI 自动化";
                    return uiAutomationText;
                }

                if (attempt < UiAutomationMaxAttempts)
                {
                    await Task.Delay(UiAutomationRetryDelayMs);
                }
            }

            var clipboardText = await CopySelectionByClipboardAsync();
            if (!string.IsNullOrWhiteSpace(clipboardText))
            {
                _lastCaptureMethod = "模拟复制";
            }

            return clipboardText;
        }

        private static async Task<string?> GetSelectedTextByUiAutomationAsync()
        {
            try
            {
                // UIA 查询可能被目标程序阻塞，放到后台线程并限时，超时则改走剪贴板
                var query = Task.Run(TryGetSelectedTextByUiAutomation);
                var winner = await Task.WhenAny(query, Task.Delay(UiAutomationTimeoutMs));
                return winner == query ? query.Result : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetSelectedTextByUiAutomation()
        {
            var node = AutomationElement.FocusedElement;
            for (var depth = 0; node is not null && depth <= MaxUiAutomationAncestorDepth; depth++)
            {
                try
                {
                    if (node.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
                    {
                        var builder = new StringBuilder();
                        foreach (var range in textPattern.GetSelection())
                        {
                            var text = range.GetText(-1);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (builder.Length > 0)
                                {
                                    builder.AppendLine();
                                }
                                builder.Append(text.Trim());
                            }
                        }

                        if (builder.Length > 0)
                        {
                            return builder.ToString();
                        }
                    }
                }
                catch
                {
                    // 个别程序在查询模式或选区时会抛 COM 异常，继续向父节点尝试
                }

                try
                {
                    node = TreeWalker.ControlViewWalker.GetParent(node);
                }
                catch
                {
                    break;
                }
            }

            return null;
        }

        private static async Task<string?> CopySelectionByClipboardAsync()
        {
            // WM_HOTKEY 到达时用户可能仍按着 Ctrl/Shift 等修饰键，
            // 此时模拟 Ctrl+C 会变成 Ctrl+Shift+C，先等修饰键物理松开
            await WaitForModifierKeysReleasedAsync();

            System.Windows.IDataObject? backup = null;
            try
            {
                backup = System.Windows.Clipboard.GetDataObject();
            }
            catch
            {
            }

            var clipboardChanged = false;
            try
            {
                var sequenceBeforeCopy = WindowServices.GetClipboardSequenceNumber();
                SimulateCopyShortcut();

                var deadline = Environment.TickCount64 + ClipboardCopyTimeoutMs;
                while (Environment.TickCount64 < deadline)
                {
                    await Task.Delay(25);
                    if (WindowServices.GetClipboardSequenceNumber() == sequenceBeforeCopy)
                    {
                        continue;
                    }

                    clipboardChanged = true;
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }

                    // 目标程序改写了剪贴板但没有文本（如复制了文件），视为取词失败
                    break;
                }
            }
            catch
            {
            }
            finally
            {
                if (clipboardChanged)
                {
                    RestoreClipboard(backup);
                }
            }

            return null;
        }

        private static async Task WaitForModifierKeysReleasedAsync()
        {
            var deadline = Environment.TickCount64 + ModifierReleaseTimeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                if (!IsKeyDown(VkControl) && !IsKeyDown(VkShift) && !IsKeyDown(VkMenu)
                    && !IsKeyDown(VkLWin) && !IsKeyDown(VkRWin))
                {
                    return;
                }

                await Task.Delay(15);
            }
        }

        private static bool IsKeyDown(byte virtualKey)
        {
            return (WindowServices.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static void RestoreClipboard(System.Windows.IDataObject? backup)
        {
            try
            {
                if (backup is not null)
                {
                    System.Windows.Clipboard.SetDataObject(backup, true);
                }
                else
                {
                    System.Windows.Clipboard.Clear();
                }
            }
            catch
            {
                // 恢复失败只能放弃，不能让剪贴板异常中断翻译流程
            }
        }

        private static void SimulateCopyShortcut()
        {
            keybd_event(VkControl, 0, 0, UIntPtr.Zero);
            keybd_event(VkC, 0, 0, UIntPtr.Zero);
            keybd_event(VkC, 0, KeyEventKeyUp, UIntPtr.Zero);
            keybd_event(VkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
        }

        private static string TranslateText(string sourceText)
        {
            var secretId = Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_ID");
            var secretKey = Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_KEY");
            if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("未找到腾讯云密钥环境变量，请先设置 TENCENTCLOUD_SECRET_ID 和 TENCENTCLOUD_SECRET_KEY。");
            }

            var cred = new Credential
            {
                SecretId = secretId,
                SecretKey = secretKey
            };

            var clientProfile = new ClientProfile();
            var httpProfile = new HttpProfile
            {
                Endpoint = "tmt.tencentcloudapi.com"
            };
            clientProfile.HttpProfile = httpProfile;

            var client = new TmtClient(cred, "ap-beijing", clientProfile);
            var req = new TextTranslateRequest
            {
                SourceText = sourceText,
                Source = "auto",
                Target = "zh",
                ProjectId = 0
            };

            var resp = client.TextTranslateSync(req);
            return string.IsNullOrWhiteSpace(resp.TargetText) ? "未返回翻译结果。" : resp.TargetText;
        }

        private void SetStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                Hide();
                SetStatus("程序已最小化到托盘。");
                _notifyIcon?.ShowBalloonTip(1000, "MyTranslator", "程序已最小化到托盘。", WinForms.ToolTipIcon.Info);
            }
        }

        private void BringWindowToForeground()
        {
            // 从托盘隐藏或最小化状态恢复
            Show();
            WindowState = WindowState.Normal;

            var helper = new WindowInteropHelper(this);
            var handle = helper.Handle;

            if (WindowServices.SetForegroundWindow(handle) && WindowServices.GetForegroundWindow() == handle)
            {
                Activate();
                Focus();
                return;
            }

            // 翻译耗时期间用户在别的程序里输入过，Windows 前台锁会拒绝普通进程置前；
            // 把本线程的输入队列临时挂到当前前台线程上即可获得置前资格
            var foregroundHandle = WindowServices.GetForegroundWindow();
            var foregroundThreadId = foregroundHandle == IntPtr.Zero
                ? 0u
                : WindowServices.GetWindowThreadProcessId(foregroundHandle, out _);
            var currentThreadId = WindowServices.GetCurrentThreadId();
            var attached = foregroundThreadId != 0
                && foregroundThreadId != currentThreadId
                && WindowServices.AttachThreadInput(currentThreadId, foregroundThreadId, true);

            try
            {
                WindowServices.BringWindowToTop(handle);
                WindowServices.SetForegroundWindow(handle);
            }
            finally
            {
                if (attached)
                {
                    WindowServices.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }

            Activate();
            Focus();
        }

        private void RestoreFromTray()
        {
            BringWindowToForeground();
            SetStatus("已从托盘恢复窗口。");
        }

        private void ExitFromTray()
        {
            Close();
        }

        private static ImageSource CreateWindowIconSource(Drawing.Icon icon)
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }

        private static Drawing.Icon CreateApplicationIcon()
        {
            using var bitmap = new Drawing.Bitmap(32, 32);
            using var graphics = Drawing.Graphics.FromImage(bitmap);
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Drawing.Color.Transparent);

            using var backgroundBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0, 120, 215));
            using var borderPen = new Drawing.Pen(Drawing.Color.White, 2);
            using var accentBrush = new Drawing.SolidBrush(Drawing.Color.White);
            using var arrowPen = new Drawing.Pen(Drawing.Color.White, 2)
            {
                StartCap = Drawing.Drawing2D.LineCap.Round,
                EndCap = Drawing.Drawing2D.LineCap.Round
            };

            using var backgroundPath = new Drawing.Drawing2D.GraphicsPath();
            const int radius = 6;
            backgroundPath.AddArc(2, 2, radius, radius, 180, 90);
            backgroundPath.AddArc(30 - radius, 2, radius, radius, 270, 90);
            backgroundPath.AddArc(30 - radius, 30 - radius, radius, radius, 0, 90);
            backgroundPath.AddArc(2, 30 - radius, radius, radius, 90, 90);
            backgroundPath.CloseFigure();

            graphics.FillPath(backgroundBrush, backgroundPath);
            graphics.DrawPath(borderPen, backgroundPath);
            graphics.FillEllipse(accentBrush, 6, 8, 8, 6);
            graphics.FillEllipse(accentBrush, 18, 18, 8, 6);
            graphics.DrawLine(arrowPen, 14, 11, 20, 18);
            graphics.DrawLine(arrowPen, 18, 10, 21, 10);
            graphics.DrawLine(arrowPen, 21, 10, 21, 13);

            var iconHandle = bitmap.GetHicon();
            try
            {
                using var tempIcon = Drawing.Icon.FromHandle(iconHandle);
                return (Drawing.Icon)tempIcon.Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            WindowServices.UnregisterHotKey(helper.Handle, HotKeyId);
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _trayMenu?.Dispose();
            _appIcon?.Dispose();
            base.OnClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    internal static class WindowServices
    {
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }
}
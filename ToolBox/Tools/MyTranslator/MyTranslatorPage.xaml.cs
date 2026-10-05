using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ToolBox.Shared;
using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Tmt.V20180321;
using TencentCloud.Tmt.V20180321.Models;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.MyTranslator
{
    /// <summary>
    /// 移植自 MyTranslator.MainWindow：全局热键划词翻译（UIA 取词 + 剪贴板兜底 + 腾讯云翻译），
    /// 也支持手动输入待翻译文本后点击“翻译”按钮。托盘、窗口置前与热键注册已上移到外壳
    /// MainWindow；本页通过 IShellHost 请求切页、弹托盘气泡。热键在“系统设置”页统一配置。
    /// </summary>
    public partial class MyTranslatorPage : UserControl, IToolPage, IGlobalHotkeyPage
    {
        private const byte VkControl = 0x11;
        private const byte VkMenu = 0x12;
        private const byte VkC = 0x43;
        private const byte VkShift = 0x10;
        private const byte VkLWin = 0x5B;
        private const byte VkRWin = 0x5C;
        private const uint KeyEventKeyUp = 0x0002;

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

        private bool _isTranslating;
        private string _lastCaptureMethod = string.Empty;

        /// <summary>外壳宿主，由 MainWindow 在构造页面后注入。</summary>
        internal IShellHost? Host { get; set; }

        public MyTranslatorPage()
        {
            InitializeComponent();
            SetStatus("就绪");
        }

        public string Title => "划词翻译";

        public void OnHostClosing()
        {
        }

        public void HandleGlobalHotkey()
        {
            _ = TranslateSelectedTextAsync();
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
                // 取词必须在外壳窗口置前之前完成，否则焦点被抢走后
                // UIA 读到的和模拟 Ctrl+C 发往的都会是翻译器自己
                SetStatus("正在读取选中文本...");
                var selectedText = await CaptureSelectedTextAsync();
                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    SetStatus("未读取到选中文本：UI 自动化与模拟复制均未成功。");
                    Host?.ShowTrayBalloon("ToolBox", "未读取到选中文本，请先选中内容。", warning: true);
                    return;
                }

                await TranslateCoreAsync(selectedText.Trim(), $"（取词方式：{_lastCaptureMethod}）");

                Host?.SelectPage(this);
                Host?.ActivateShell();
            }
            catch (Exception ex)
            {
                SetStatus($"翻译失败：{ex.Message}");
                Host?.ShowTrayBalloon("ToolBox", $"翻译失败：{ex.Message}", warning: true);
            }
            finally
            {
                _isTranslating = false;
            }
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTranslating)
            {
                SetStatus("正在翻译中，请稍后。");
                return;
            }

            var sourceText = SelectedTextTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                SetStatus("请先输入待翻译内容，或使用全局快捷键取词。");
                return;
            }

            _isTranslating = true;
            try
            {
                await TranslateCoreAsync(sourceText, statusSuffix: string.Empty);
            }
            catch (Exception ex)
            {
                SetStatus($"翻译失败：{ex.Message}");
                Host?.ShowTrayBalloon("ToolBox", $"翻译失败：{ex.Message}", warning: true);
            }
            finally
            {
                _isTranslating = false;
            }
        }

        /// <summary>截断过长文本、调用翻译接口并刷新两个文本框。</summary>
        private async Task TranslateCoreAsync(string sourceText, string statusSuffix)
        {
            if (sourceText.Length > MaxSourceTextLength)
            {
                sourceText = sourceText[..MaxSourceTextLength];
                SetStatus($"文本过长，已截断为 {MaxSourceTextLength} 字符，正在调用翻译接口...");
            }
            else
            {
                SetStatus("正在调用翻译接口...");
            }

            SelectedTextTextBox.Text = sourceText;

            var translatedText = await Task.Run(() => TranslateText(sourceText));
            TranslatedTextTextBox.Text = translatedText;
            SetStatus($"翻译完成。{statusSuffix}");
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
            WindowServices.keybd_event(VkControl, 0, 0, UIntPtr.Zero);
            WindowServices.keybd_event(VkC, 0, 0, UIntPtr.Zero);
            WindowServices.keybd_event(VkC, 0, KeyEventKeyUp, UIntPtr.Zero);
            WindowServices.keybd_event(VkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
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
    }
}

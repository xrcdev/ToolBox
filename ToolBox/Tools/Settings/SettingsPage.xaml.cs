using System.Windows;
using ToolBox.Shared;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.Settings;

/// <summary>
/// 系统设置页：统一编辑各功能的全局快捷键（划词翻译/调整窗口大小/窗口置顶）。
/// 编辑结果通过外壳 IShellHost.ApplyFunctionHotkey 即时注册并持久化到
/// %LocalAppData%\ToolBox\settings.json。
/// </summary>
public partial class SettingsPage : UserControl, IToolPage
{
    internal IShellHost? Host { get; set; }

    internal ShellSettings? Settings { get; set; }

    public SettingsPage()
    {
        InitializeComponent();
        // Host/Settings 在构造后由外壳注入，切换到本页时再回填编辑器
        Loaded += (_, _) => PopulateEditors();
    }

    public string Title => "系统设置";

    public void OnHostClosing()
    {
    }

    private void PopulateEditors()
    {
        if (Settings is null)
        {
            return;
        }

        PopulateRow(Settings.Translator, TranslatorCtrlCheckBox, TranslatorShiftCheckBox, TranslatorAltCheckBox, TranslatorWinCheckBox, TranslatorKeyTextBox);
        PopulateRow(Settings.Resize, ResizeCtrlCheckBox, ResizeShiftCheckBox, ResizeAltCheckBox, ResizeWinCheckBox, ResizeKeyTextBox);
        PopulateRow(Settings.TopMost, TopMostCtrlCheckBox, TopMostShiftCheckBox, TopMostAltCheckBox, TopMostWinCheckBox, TopMostKeyTextBox);
    }

    private static void PopulateRow(HotkeyConfig config, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox keyTextBox)
    {
        ctrl.IsChecked = config.Ctrl;
        shift.IsChecked = config.Shift;
        alt.IsChecked = config.Alt;
        win.IsChecked = config.Win;
        keyTextBox.Text = config.Key;
    }

    private void ApplyTranslator_Click(object sender, RoutedEventArgs e)
    {
        ApplyRow(HotkeyIds.Translator, "划词翻译", TranslatorCtrlCheckBox, TranslatorShiftCheckBox, TranslatorAltCheckBox, TranslatorWinCheckBox, TranslatorKeyTextBox);
    }

    private void ApplyResize_Click(object sender, RoutedEventArgs e)
    {
        ApplyRow(HotkeyIds.Resize, "调整窗口大小", ResizeCtrlCheckBox, ResizeShiftCheckBox, ResizeAltCheckBox, ResizeWinCheckBox, ResizeKeyTextBox);
    }

    private void ApplyTopMost_Click(object sender, RoutedEventArgs e)
    {
        ApplyRow(HotkeyIds.TopMost, "窗口置顶", TopMostCtrlCheckBox, TopMostShiftCheckBox, TopMostAltCheckBox, TopMostWinCheckBox, TopMostKeyTextBox);
    }

    private void ApplyRow(int hotkeyId, string functionName, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox keyTextBox)
    {
        if (Host is null || Settings is null)
        {
            return;
        }

        var config = new HotkeyConfig
        {
            Ctrl = ctrl.IsChecked == true,
            Shift = shift.IsChecked == true,
            Alt = alt.IsChecked == true,
            Win = win.IsChecked == true,
            Key = keyTextBox.Text.Trim()
        };

        if (!config.TryResolve(out _, out _, out var display))
        {
            SetStatus($"{functionName}：快捷键无效，请至少选择一个修饰键，按键支持字母/数字/F1-F24。");
            return;
        }

        if (ConflictsWithOther(hotkeyId, config, out var conflictName))
        {
            SetStatus($"{functionName}：快捷键 {display} 与“{conflictName}”重复，请更换。");
            return;
        }

        if (Host.ApplyFunctionHotkey(hotkeyId, config))
        {
            SetStatus($"{functionName} 快捷键已注册：{display}");
        }
        else
        {
            SetStatus($"{functionName} 快捷键注册失败：{display} 可能已被其他程序占用。");
            MessageBox.Show(Window.GetWindow(this), $"{functionName} 快捷键注册失败：{display} 可能已被其他程序占用。", "提示");
        }
    }

    private bool ConflictsWithOther(int hotkeyId, HotkeyConfig config, out string conflictName)
    {
        var functions = new[]
        {
            (HotkeyIds.Translator, "划词翻译"),
            (HotkeyIds.Resize, "调整窗口大小"),
            (HotkeyIds.TopMost, "窗口置顶")
        };

        foreach (var (otherId, otherName) in functions)
        {
            if (otherId == hotkeyId)
            {
                continue;
            }

            // 与其他功能“已生效”的快捷键比较，避免同一组合重复注册
            var other = Settings.ForId(otherId);
            if (other is not null
                && other.TryResolve(out var otherModifiers, out var otherVirtualKey, out _)
                && config.TryResolve(out var modifiers, out var virtualKey, out _)
                && otherModifiers == modifiers
                && otherVirtualKey == virtualKey)
            {
                conflictName = otherName;
                return true;
            }
        }

        conflictName = string.Empty;
        return false;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (Host is null || Settings is null)
        {
            return;
        }

        Settings.Translator = new HotkeyConfig { Ctrl = true, Shift = true, Key = "1" };
        Settings.Resize = new HotkeyConfig { Ctrl = true, Shift = true, Key = "2" };
        Settings.TopMost = new HotkeyConfig { Ctrl = true, Shift = true, Key = "3" };
        PopulateEditors();

        ApplyRow(HotkeyIds.Translator, "划词翻译", TranslatorCtrlCheckBox, TranslatorShiftCheckBox, TranslatorAltCheckBox, TranslatorWinCheckBox, TranslatorKeyTextBox);
        ApplyRow(HotkeyIds.Resize, "调整窗口大小", ResizeCtrlCheckBox, ResizeShiftCheckBox, ResizeAltCheckBox, ResizeWinCheckBox, ResizeKeyTextBox);
        ApplyRow(HotkeyIds.TopMost, "窗口置顶", TopMostCtrlCheckBox, TopMostShiftCheckBox, TopMostAltCheckBox, TopMostWinCheckBox, TopMostKeyTextBox);

        SetStatus("已恢复默认快捷键。");
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}

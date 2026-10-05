using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ToolBox.Shared;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.AdjustUISize;

/// <summary>
/// 移植自 AdjustUISize.MainWindow：维护窗口尺寸预设并应用到前台窗口。
/// 托盘与全局热键注册已上移到外壳 MainWindow，本页只接收热键触发；
/// 介绍文案中的快捷键在每次显示时从外壳读取当前配置（可在“系统设置”页修改）。
/// 预设文件路径优先级保持不变：程序目录 presets.json，其次 %LocalAppData%\AdjustUISize\presets.json。
/// </summary>
public partial class AdjustUISizePage : UserControl, IToolPage, IGlobalHotkeyPage
{
    /// <summary>外壳宿主，由 MainWindow 在构造页面后注入。</summary>
    internal IShellHost? Host { get; set; }

    private readonly string _appPresetFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "presets.json");
    private readonly string _systemPresetFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AdjustUISize",
        "presets.json");
    private string? _currentPresetFilePath;

    public ObservableCollection<WindowSizePreset> Presets { get; } = new();

    public WindowSizePreset? SelectedPreset
    {
        get => (WindowSizePreset?)GetValue(SelectedPresetProperty);
        set => SetValue(SelectedPresetProperty, value);
    }

    public static readonly DependencyProperty SelectedPresetProperty = DependencyProperty.Register(
        nameof(SelectedPreset),
        typeof(WindowSizePreset),
        typeof(AdjustUISizePage),
        new PropertyMetadata(null, OnSelectedPresetChanged));

    public AdjustUISizePage()
    {
        InitializeComponent();
        DataContext = this;
        LoadPresets();
        Loaded += (_, _) => HotkeyRun.Text = Host?.GetHotkeyDisplayText(HotkeyIds.Resize) ?? "未配置";
    }

    public string Title => "调整窗口大小";

    public void OnHostClosing()
    {
    }

    private static void OnSelectedPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var page = (AdjustUISizePage)d;
        page.SyncEditorFromSelection();
    }

    private void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildPresetFromEditor(out var preset))
        {
            return;
        }

        Presets.Add(preset);
        SelectedPreset = preset;
        SavePresets();
        SetStatus($"已新增预设：{preset.Name} ({preset.Width} x {preset.Height})");
    }

    private void UpdatePreset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset is null)
        {
            SetStatus("请先选择一个预设再更新。");
            return;
        }

        if (!TryBuildPresetFromEditor(out var preset))
        {
            return;
        }

        SelectedPreset.Name = preset.Name;
        SelectedPreset.Width = preset.Width;
        SelectedPreset.Height = preset.Height;
        PresetsDataGrid.Items.Refresh();
        SavePresets();
        SetStatus($"已更新预设：{preset.Name}");
    }

    private void RemovePreset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset is null)
        {
            SetStatus("请先选择要删除的预设。");
            return;
        }

        var presetName = SelectedPreset.Name;
        Presets.Remove(SelectedPreset);
        SelectedPreset = Presets.FirstOrDefault();
        SavePresets();
        SetStatus($"已删除预设：{presetName}");
    }

    private void ClearEditor_Click(object sender, RoutedEventArgs e)
    {
        PresetsDataGrid.SelectedItem = null;
        NameTextBox.Clear();
        WidthTextBox.Clear();
        HeightTextBox.Clear();
        SetStatus("已清空编辑区。");
    }

    private void ApplySelectedPreset_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedPresetToForegroundWindow();
    }

    public void HandleGlobalHotkey()
    {
        ApplySelectedPresetToForegroundWindow();
    }

    private void ApplySelectedPresetToForegroundWindow()
    {
        if (SelectedPreset is null)
        {
            SetStatus("请先选择一个窗口尺寸预设。");
            return;
        }

        var foregroundWindow = WindowServices.GetForegroundWindow();
        var hostWindow = Window.GetWindow(this);
        var currentWindowHandle = hostWindow is not null
            ? new WindowInteropHelper(hostWindow).Handle
            : IntPtr.Zero;

        if (foregroundWindow == IntPtr.Zero)
        {
            SetStatus("未获取到当前活动窗口。");
            return;
        }

        if (foregroundWindow == currentWindowHandle)
        {
            SetStatus("当前活动窗口是本程序，请先切换到目标应用后再按快捷键。");
            return;
        }

        if (WindowServices.TryResizeWindow(foregroundWindow, SelectedPreset.Width, SelectedPreset.Height))
        {
            SetStatus($"已应用预设：{SelectedPreset.Name} -> {SelectedPreset.Width} x {SelectedPreset.Height}");
        }
        else
        {
            SetStatus("调整窗口大小失败，请确认目标窗口允许被调整。");
        }
    }

    private bool TryBuildPresetFromEditor(out WindowSizePreset preset)
    {
        preset = new WindowSizePreset();

        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("请输入预设名称。");
            return false;
        }

        if (!int.TryParse(WidthTextBox.Text.Trim(), out var width) || width <= 0)
        {
            SetStatus("宽度必须是大于 0 的整数。");
            return false;
        }

        if (!int.TryParse(HeightTextBox.Text.Trim(), out var height) || height <= 0)
        {
            SetStatus("高度必须是大于 0 的整数。");
            return false;
        }

        preset.Name = name;
        preset.Width = width;
        preset.Height = height;
        return true;
    }

    private void SyncEditorFromSelection()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        NameTextBox.Text = SelectedPreset.Name;
        WidthTextBox.Text = SelectedPreset.Width.ToString();
        HeightTextBox.Text = SelectedPreset.Height.ToString();
    }

    private void LoadPresets()
    {
        var presetFilePath = GetPreferredPresetFilePath();
        _currentPresetFilePath = presetFilePath;

        if (File.Exists(presetFilePath))
        {
            try
            {
                var json = File.ReadAllText(presetFilePath);
                var presets = JsonSerializer.Deserialize<WindowSizePreset[]>(json);
                if (presets is { Length: > 0 })
                {
                    foreach (var preset in presets)
                    {
                        Presets.Add(preset);
                    }

                    SetStatus($"已加载配置：{presetFilePath}");
                }
            }
            catch
            {
                SetStatus("读取预设失败，已加载默认预设。");
            }
        }

        if (Presets.Count == 0)
        {
            Presets.Add(new WindowSizePreset { Name = "小窗办公", Width = 1280, Height = 720 });
            Presets.Add(new WindowSizePreset { Name = "文档阅读", Width = 1440, Height = 900 });
            Presets.Add(new WindowSizePreset { Name = "全高清", Width = 1920, Height = 1080 });
            SavePresets();
            SetStatus($"未找到配置文件，已创建默认配置：{_currentPresetFilePath}");
        }

        SelectedPreset = Presets.FirstOrDefault();
    }

    private void SavePresets()
    {
        _currentPresetFilePath ??= GetPreferredPresetFilePath();

        var directory = Path.GetDirectoryName(_currentPresetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(Presets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_currentPresetFilePath, json);
    }

    private string GetPreferredPresetFilePath()
    {
        if (File.Exists(_appPresetFilePath))
        {
            return _appPresetFilePath;
        }

        if (File.Exists(_systemPresetFilePath))
        {
            return _systemPresetFilePath;
        }

        return _appPresetFilePath;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace AdjustUISize;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int HotKeyId = 9002;
    private const uint Vk2 = 0x32;
    private WinForms.NotifyIcon? _notifyIcon;
    private WinForms.ContextMenuStrip? _trayMenu;
    private Drawing.Icon? _appIcon;
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
        typeof(MainWindow),
        new PropertyMetadata(null, OnSelectedPresetChanged));

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        DataContext = this;
        LoadPresets();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(HwndHook);

        if (!WindowServices.RegisterHotKey(
                helper.Handle,
                HotKeyId,
                WindowServices.MOD_CONTROL | WindowServices.MOD_SHIFT,
                Vk2))
        {
            SetStatus("注册快捷键 Ctrl+Shift+2 失败，可能已被其他程序占用。");
            System.Windows.MessageBox.Show(this, "注册快捷键 Ctrl+Shift+2 失败，可能已被其他程序占用。", "提示");
            return;
        }

        SetStatus("快捷键已注册：Ctrl+Shift+2");
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
            Text = "Adjust UI Size",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = _trayMenu
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private static void OnSelectedPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (MainWindow)d;
        window.SyncEditorFromSelection();
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

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowServices.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            ApplySelectedPresetToForegroundWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ApplySelectedPresetToForegroundWindow()
    {
        if (SelectedPreset is null)
        {
            SetStatus("请先选择一个窗口尺寸预设。");
            return;
        }

        var foregroundWindow = WindowServices.GetForegroundWindow();
        var currentWindowHandle = new WindowInteropHelper(this).Handle;

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

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized)
        {
            Hide();
            SetStatus("程序已最小化到托盘。");
            _notifyIcon?.ShowBalloonTip(1000, "Adjust UI Size", "程序已最小化到托盘。", WinForms.ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
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

        using var backgroundPath = new Drawing.Drawing2D.GraphicsPath();
        const int radius = 7;
        backgroundPath.AddArc(2, 2, radius, radius, 180, 90);
        backgroundPath.AddArc(30 - radius, 2, radius, radius, 270, 90);
        backgroundPath.AddArc(30 - radius, 30 - radius, radius, radius, 0, 90);
        backgroundPath.AddArc(2, 30 - radius, radius, radius, 90, 90);
        backgroundPath.CloseFigure();

        using var backgroundBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0, 120, 215));
        using var framePen = new Drawing.Pen(Drawing.Color.White, 2);
        using var arrowPen = new Drawing.Pen(Drawing.Color.White, 2)
        {
            StartCap = Drawing.Drawing2D.LineCap.Round,
            EndCap = Drawing.Drawing2D.LineCap.Round
        };

        graphics.FillPath(backgroundBrush, backgroundPath);
        graphics.DrawRectangle(framePen, 7, 7, 18, 13);

        graphics.DrawLine(arrowPen, 10, 24, 6, 28);
        graphics.DrawLine(arrowPen, 6, 28, 10, 28);
        graphics.DrawLine(arrowPen, 6, 28, 6, 24);

        graphics.DrawLine(arrowPen, 22, 24, 26, 28);
        graphics.DrawLine(arrowPen, 22, 28, 26, 28);
        graphics.DrawLine(arrowPen, 26, 24, 26, 28);

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

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
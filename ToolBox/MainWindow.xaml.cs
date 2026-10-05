using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToolBox.Shared;
using ToolBox.Tools.AdjustUISize;
using ToolBox.Tools.AlwaysOnTop;
using ToolBox.Tools.CopyFiles;
using ToolBox.Tools.FileSplitterAndMerge;
using ToolBox.Tools.MyTranslator;
using ToolBox.Tools.RemoveReadonly;
using ToolBox.Tools.Settings;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ToolBox;

/// <summary>
/// 工具箱外壳：左侧纵向导航切换各工具页面，统一承载托盘图标与全局热键
/// （热键组合可在“系统设置”页自定义，持久化到 %LocalAppData%\ToolBox\settings.json）。
/// 最小化隐藏到托盘；点 X 退出进程。
/// </summary>
public partial class MainWindow : Window, IShellHost
{
    private readonly ShellSettings _settings;
    private readonly ObservableCollection<ToolNavEntry> _navEntries = new();
    private readonly List<IToolPage> _pages = new();
    private readonly Dictionary<int, bool> _hotkeyState = new();
    private readonly MyTranslatorPage _translatorPage;
    private readonly AdjustUISizePage _adjustUISizePage;
    private readonly AlwaysOnTopPage _alwaysOnTopPage;
    private HwndSource? _hwndSource;
    private IntPtr _handle;

    // 导航选项卡拖拽排序的按下起点与被拖动条目
    private Point _navDragStartPoint;
    private ToolNavEntry? _navDragEntry;

    private WinForms.NotifyIcon? _notifyIcon;
    private WinForms.ContextMenuStrip? _trayMenu;
    private Drawing.Icon? _appIcon;
    private bool _minimizeBalloonShown;

    public MainWindow()
    {
        InitializeComponent();

        _settings = ShellSettingsStore.Load();

        // 热键路由需要页面引用；先构造再注册到导航
        _translatorPage = new MyTranslatorPage { Host = this };
        _adjustUISizePage = new AdjustUISizePage { Host = this };
        _alwaysOnTopPage = new AlwaysOnTopPage { Host = this };
        var copyFilesPage = new CopyFilesPage();
        var splitterPage = new FileSplitterPage();
        var removeReadonlyPage = new RemoveReadonlyPage();
        var settingsPage = new SettingsPage { Host = this, Settings = _settings };

        _pages.Add(_translatorPage);
        _pages.Add(_alwaysOnTopPage);
        _pages.Add(_adjustUISizePage);
        _pages.Add(copyFilesPage);
        _pages.Add(splitterPage);
        _pages.Add(removeReadonlyPage);
        _pages.Add(settingsPage);

        _navEntries.Add(new ToolNavEntry("🌐", _translatorPage.Title, _translatorPage));
        _navEntries.Add(new ToolNavEntry("📌", _alwaysOnTopPage.Title, _alwaysOnTopPage));
        _navEntries.Add(new ToolNavEntry("📐", _adjustUISizePage.Title, _adjustUISizePage));
        _navEntries.Add(new ToolNavEntry("📄", copyFilesPage.Title, copyFilesPage));
        _navEntries.Add(new ToolNavEntry("✂", splitterPage.Title, splitterPage));
        _navEntries.Add(new ToolNavEntry("🔓", removeReadonlyPage.Title, removeReadonlyPage));
        _navEntries.Add(new ToolNavEntry("⚙", settingsPage.Title, settingsPage));

        ApplySavedPageOrder();
        NavList.ItemsSource = _navEntries;
        NavList.SelectedIndex = 0;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_handle);
        _hwndSource?.AddHook(WndProc);

        RegisterShellHotkeys();
        InitializeTrayIcon();
    }

    #region 全局热键

    private void RegisterShellHotkeys()
    {
        TryRegisterFunction(HotkeyIds.Translator, _settings.Translator);
        TryRegisterFunction(HotkeyIds.Resize, _settings.Resize);
        TryRegisterFunction(HotkeyIds.TopMost, _settings.TopMost);
        RefreshHotkeyHint();
    }

    private bool TryRegisterFunction(int hotkeyId, HotkeyConfig config)
    {
        WindowServices.UnregisterHotKey(_handle, hotkeyId);

        var ok = config.TryResolve(out var modifiers, out var virtualKey, out _)
            && WindowServices.RegisterHotKey(_handle, hotkeyId, modifiers, virtualKey);
        _hotkeyState[hotkeyId] = ok;
        return ok;
    }

    private void RefreshHotkeyHint()
    {
        var hint = $"全局热键：{_settings.Translator.BuildDisplayText()} 划词翻译 ｜ " +
                   $"{_settings.Resize.BuildDisplayText()} 调整窗口大小 ｜ " +
                   $"{_settings.TopMost.BuildDisplayText()} 窗口置顶";

        var failures = _hotkeyState.Where(kv => !kv.Value)
            .Select(kv => _settings.ForId(kv.Key)?.BuildDisplayText() ?? $"id {kv.Key}")
            .ToList();
        if (failures.Count > 0)
        {
            hint += $"　（注册失败，可能被其他程序占用：{string.Join("、", failures)}）";
        }

        HotkeyHintTextBlock.Text = hint;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowServices.WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HotkeyIds.Translator:
                    _translatorPage.HandleGlobalHotkey();
                    handled = true;
                    break;
                case HotkeyIds.Resize:
                    _adjustUISizePage.HandleGlobalHotkey();
                    handled = true;
                    break;
                case HotkeyIds.TopMost:
                    _alwaysOnTopPage.HandleGlobalHotkey();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    #endregion

    #region 导航拖拽排序

    private void NavList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _navDragEntry = null;
        if (NavList.ContainerFromElement(e.OriginalSource as DependencyObject) is ListBoxItem container
            && container.Content is ToolNavEntry entry)
        {
            _navDragEntry = entry;
            _navDragStartPoint = e.GetPosition(NavList);
        }
    }

    private void NavList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _navDragEntry is null)
        {
            return;
        }

        var pos = e.GetPosition(NavList);
        if (Math.Abs(pos.X - _navDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _navDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var entry = _navDragEntry;
        _navDragEntry = null;
        DragDrop.DoDragDrop(NavList, new DataObject(entry), DragDropEffects.Move);
    }

    private void NavList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ToolNavEntry)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void NavList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ToolNavEntry)) is ToolNavEntry entry)
        {
            MoveNavEntry(entry, GetNavInsertIndex(e.GetPosition(NavList)));
        }
    }

    /// <summary>根据鼠标纵向位置计算插入位置：目标项上半部分插到其前，下半部分插到其后。</summary>
    private int GetNavInsertIndex(Point pos)
    {
        for (var i = 0; i < NavList.Items.Count; i++)
        {
            if (NavList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
            {
                continue;
            }

            var topLeft = container.TranslatePoint(new Point(0, 0), NavList);
            if (pos.Y < topLeft.Y + container.ActualHeight / 2)
            {
                return i;
            }
        }

        // 拖到列表末尾空白处 → 插到最后
        return NavList.Items.Count;
    }

    private void MoveNavEntry(ToolNavEntry entry, int insertIndex)
    {
        var oldIndex = _navEntries.IndexOf(entry);
        if (oldIndex < 0)
        {
            return;
        }

        // 先移除后插入时，目标位置在其后方的项索引整体前移一位
        if (oldIndex < insertIndex)
        {
            insertIndex--;
        }

        if (insertIndex == oldIndex)
        {
            return;
        }

        _navEntries.RemoveAt(oldIndex);
        insertIndex = Math.Max(0, Math.Min(insertIndex, _navEntries.Count));
        _navEntries.Insert(insertIndex, entry);
        NavList.SelectedItem = entry;

        PersistPageOrder();
    }

    /// <summary>启动时按设置中保存的顺序重排导航；未记录的新页面按默认顺序排在最后。</summary>
    private void ApplySavedPageOrder()
    {
        if (_settings.PageOrder.Count == 0)
        {
            return;
        }

        var ordered = new List<ToolNavEntry>();
        foreach (var title in _settings.PageOrder)
        {
            var entry = _navEntries.FirstOrDefault(t => t.Title == title);
            if (entry is not null && !ordered.Contains(entry))
            {
                ordered.Add(entry);
            }
        }

        foreach (var entry in _navEntries)
        {
            if (!ordered.Contains(entry))
            {
                ordered.Add(entry);
            }
        }

        if (ordered.SequenceEqual(_navEntries))
        {
            return;
        }

        _navEntries.Clear();
        foreach (var entry in ordered)
        {
            _navEntries.Add(entry);
        }
    }

    private void PersistPageOrder()
    {
        _settings.PageOrder = _navEntries.Select(t => t.Title).ToList();
        ShellSettingsStore.Save(_settings);
    }

    #endregion

    #region 托盘

    private void InitializeTrayIcon()
    {
        _appIcon = CreateApplicationIcon();
        Icon = CreateWindowIconSource(_appIcon);

        _trayMenu = new WinForms.ContextMenuStrip();
        _trayMenu.Items.Add("打开 ToolBox", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
        _trayMenu.Items.Add("退出", null, (_, _) => Close());

        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "ToolBox 工具箱",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        ActivateShell();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized)
        {
            Hide();
            if (!_minimizeBalloonShown)
            {
                _minimizeBalloonShown = true;
                _notifyIcon?.ShowBalloonTip(1000, "ToolBox", "程序已最小化到托盘，双击托盘图标恢复。", WinForms.ToolTipIcon.Info);
            }
        }
    }

    #endregion

    #region IShellHost

    public void ActivateShell()
    {
        // 从托盘隐藏或最小化状态恢复
        Show();
        WindowState = WindowState.Normal;

        if (WindowServices.SetForegroundWindow(_handle) && WindowServices.GetForegroundWindow() == _handle)
        {
            Activate();
            Focus();
            return;
        }

        // 热键触发时用户在其他程序中输入过，Windows 前台锁会拒绝普通进程置前；
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
            WindowServices.BringWindowToTop(_handle);
            WindowServices.SetForegroundWindow(_handle);
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

    void IShellHost.SelectPage(IToolPage page)
    {
        var entry = _navEntries.FirstOrDefault(t => ReferenceEquals(t.Page, page));
        if (entry is not null && !ReferenceEquals(NavList.SelectedItem, entry))
        {
            NavList.SelectedItem = entry;
        }
    }

    bool IShellHost.ApplyFunctionHotkey(int hotkeyId, HotkeyConfig config)
    {
        if (_handle == IntPtr.Zero)
        {
            return false;
        }

        var ok = TryRegisterFunction(hotkeyId, config);
        if (ok)
        {
            _settings.SetForId(hotkeyId, config);
            ShellSettingsStore.Save(_settings);
        }
        RefreshHotkeyHint();
        return ok;
    }

    public void ShowTrayBalloon(string title, string message, bool warning)
    {
        _notifyIcon?.ShowBalloonTip(warning ? 2000 : 1500, title, message,
            warning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
    }

    public string GetHotkeyDisplayText(int hotkeyId)
    {
        return _settings.ForId(hotkeyId)?.BuildDisplayText() ?? "未配置";
    }

    #endregion

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ToolNavEntry entry)
        {
            PageHost.Content = entry.Page;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        foreach (var page in _pages)
        {
            page.OnHostClosing();
        }

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        WindowServices.UnregisterHotKey(_handle, HotkeyIds.Translator);
        WindowServices.UnregisterHotKey(_handle, HotkeyIds.Resize);
        WindowServices.UnregisterHotKey(_handle, HotkeyIds.TopMost);

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _trayMenu?.Dispose();
        _appIcon?.Dispose();
    }

    #region 图标绘制

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
        graphics.FillPath(backgroundBrush, backgroundPath);

        // 白色“工”字点题工具箱
        using var font = new Drawing.Font("Segoe UI", 15f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        var format = new Drawing.StringFormat
        {
            Alignment = Drawing.StringAlignment.Center,
            LineAlignment = Drawing.StringAlignment.Center
        };
        graphics.DrawString("工", font, Drawing.Brushes.White, new Drawing.RectangleF(0, 0, 32, 32), format);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Drawing.Icon.FromHandle(iconHandle);
            return (Drawing.Icon)tempIcon.Clone();
        }
        finally
        {
            WindowServices.DestroyIcon(iconHandle);
        }
    }

    #endregion
}

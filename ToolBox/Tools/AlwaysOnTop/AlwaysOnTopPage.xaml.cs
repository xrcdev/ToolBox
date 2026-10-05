using System;
using System.Windows;
using System.Windows.Controls;
using ToolBox.Shared;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.AlwaysOnTop;

/// <summary>
/// 移植自 AlwaysOnTop.WPF.MainWindow：枚举可见窗口并切换置顶状态。
/// 托盘与全局热键注册已上移到外壳 MainWindow，本页只接收热键触发；
/// 快捷键提示在每次显示时从外壳读取当前配置（可在“系统设置”页修改）。
/// </summary>
public partial class AlwaysOnTopPage : UserControl, IToolPage, IGlobalHotkeyPage
{
    /// <summary>外壳宿主，由 MainWindow 在构造页面后注入。</summary>
    internal IShellHost? Host { get; set; }

    public AlwaysOnTopPage()
    {
        InitializeComponent();
        Loaded += AlwaysOnTopPage_Loaded;
    }

    public string Title => "窗口置顶";

    public void OnHostClosing()
    {
    }

    private void AlwaysOnTopPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
        HotkeyHintTextBlock.Text = $"全局热键：{Host?.GetHotkeyDisplayText(HotkeyIds.TopMost) ?? "未配置"}（切换当前活动窗口置顶）";
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
    }

    private void RefreshWindows()
    {
        var windows = WindowServices.GetVisibleWindows();
        WindowsGrid.ItemsSource = windows;
    }

    public void HandleGlobalHotkey()
    {
        ToggleActiveWindowTopMost();
    }

    private void ToggleActiveWindowTopMost()
    {
        var hWnd = WindowServices.GetForegroundWindow();
        if (hWnd != IntPtr.Zero)
        {
            bool isTop = WindowServices.IsTopMost(hWnd);
            WindowServices.SetTopMost(hWnd, !isTop);

            // Refresh the list to reflect changes
            RefreshWindows();
        }
    }

    private void TopMostCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is WindowInfo windowInfo)
        {
            bool isTop = checkBox.IsChecked == true;
            WindowServices.SetTopMost(windowInfo.Handle, isTop);
        }
    }
}

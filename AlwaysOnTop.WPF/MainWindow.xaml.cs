using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace AlwaysOnTop.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;
    private WinForms.NotifyIcon _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();
        InitializeNotifyIcon();
        Loaded += MainWindow_Loaded;
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon();
        _notifyIcon.Text = "AlwaysOnTop";
        // Create a custom icon
        _notifyIcon.Icon = CreateTrayIcon();
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (s, args) => ShowWindow();

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, args) => ShowWindow());
        contextMenu.Items.Add("Exit", null, (s, args) => Close());
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = new Drawing.Bitmap(16, 16);
        using var g = Drawing.Graphics.FromImage(bitmap);

        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw background circle
        using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0, 120, 215));
        g.FillEllipse(brush, 1, 1, 14, 14);

        // Draw arrow pointing up
        using var pen = new Drawing.Pen(Drawing.Color.White, 2);
        g.DrawLine(pen, 8, 4, 4, 8);   // Left part of arrow head
        g.DrawLine(pen, 8, 4, 12, 8);  // Right part of arrow head
        g.DrawLine(pen, 8, 4, 8, 12);  // Arrow shaft

        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
        base.OnStateChanged(e);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(HwndHook);
        RegisterHotKey(helper.Handle);
    }

    private void RegisterHotKey(IntPtr handle)
    {
        // Ctrl + Shift + 3
        // '3' is 0x33
        const uint VK_3 = 0x33;
        uint modifiers = WindowServices.MOD_CONTROL | WindowServices.MOD_SHIFT;

            if (!WindowServices.RegisterHotKey(handle, HOTKEY_ID, modifiers, VK_3))
            {
                System.Windows.MessageBox.Show("Failed to register hotkey Ctrl+Shift+3. It might be in use.");
            }
        }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowServices.WM_HOTKEY)
        {
            if (wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleActiveWindowTopMost();
                handled = true;
            }
        }
        return IntPtr.Zero;
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
        if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is WindowInfo windowInfo)
        {
            bool isTop = checkBox.IsChecked == true;
            WindowServices.SetTopMost(windowInfo.Handle, isTop);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _notifyIcon?.Dispose();
        var helper = new WindowInteropHelper(this);
        WindowServices.UnregisterHotKey(helper.Handle, HOTKEY_ID);
        base.OnClosed(e);
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace AlwaysOnTop.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
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
            MessageBox.Show("Failed to register hotkey Ctrl+Shift+3. It might be in use.");
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
        if (sender is CheckBox checkBox && checkBox.DataContext is WindowInfo windowInfo)
        {
            bool isTop = checkBox.IsChecked == true;
            WindowServices.SetTopMost(windowInfo.Handle, isTop);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        WindowServices.UnregisterHotKey(helper.Handle, HOTKEY_ID);
        base.OnClosed(e);
    }
}
using System.Windows.Controls;
using ToolBox.Shared;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox;

/// <summary>工具页面在外壳中的公共契约。</summary>
internal interface IToolPage
{
    /// <summary>导航条上显示的标题。</summary>
    string Title { get; }

    /// <summary>宿主窗口关闭前调用：保存设置、取消后台任务。</summary>
    void OnHostClosing();
}

/// <summary>工具页面访问外壳宿主（主窗口）的能力。</summary>
internal interface IShellHost
{
    /// <summary>显示并前置外壳窗口（从托盘恢复或热键触发时）。</summary>
    void ActivateShell();

    /// <summary>切换到指定页面。</summary>
    void SelectPage(IToolPage page);

    /// <summary>通过外壳托盘图标弹出气泡通知。</summary>
    void ShowTrayBalloon(string title, string message, bool warning);

    /// <summary>获取指定功能当前生效的快捷键显示文本（如 "Ctrl+Shift+3"），供页面提示动态刷新。</summary>
    string GetHotkeyDisplayText(int hotkeyId);

    /// <summary>
    /// 应用功能热键配置：注销旧键、注册新键；成功后写入外壳设置并持久化、刷新底部热键提示。
    /// 注册失败（如被其他程序占用）返回 false，外壳设置不变。
    /// </summary>
    bool ApplyFunctionHotkey(int hotkeyId, HotkeyConfig config);
}

/// <summary>实现此接口的页面可接收全局热键触发。</summary>
internal interface IGlobalHotkeyPage
{
    void HandleGlobalHotkey();
}

/// <summary>导航条目。</summary>
internal sealed class ToolNavEntry
{
    public ToolNavEntry(string icon, string title, UserControl page)
    {
        Icon = icon;
        Title = title;
        Page = page;
    }

    public string Icon { get; }

    public string Title { get; }

    public UserControl Page { get; }

    /// <summary>供 UI 自动化/无障碍读取条目名称。</summary>
    public override string ToString() => Title;
}

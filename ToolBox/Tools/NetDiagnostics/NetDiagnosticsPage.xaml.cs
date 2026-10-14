using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

/// <summary>网络诊断与调试工具页：Ping / 路由追踪 / DNS / 端口测试 / 本机网络 / HTTP 调试。</summary>
public partial class NetDiagnosticsPage : UserControl, IToolPage
{
    public NetDiagnosticsPage()
    {
        InitializeComponent();
    }

    public string Title => "网络诊断";

    public void OnHostClosing()
    {
        PingPart.CancelRun();
        TracePart.CancelRun();
        DnsPart.CancelRun();
        PortPart.CancelRun();
        HttpPart.CancelRun();
        LocalPart.StopAutoRefresh();
    }
}

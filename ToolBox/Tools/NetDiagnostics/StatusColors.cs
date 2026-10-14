using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ToolBox.Tools.NetDiagnostics;

/// <summary>页面内状态文字的警告色。主题字典未定义语义色，仅此处硬编码。</summary>
internal static class StatusColors
{
    public static readonly Brush Warning = Create("#FFC42B1C");

    private static Brush Create(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}

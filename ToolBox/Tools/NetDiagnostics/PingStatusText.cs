using System.Net.NetworkInformation;

namespace ToolBox.Tools.NetDiagnostics;

/// <summary>ICMP 探测状态的中文文案，Ping 与路由追踪共用。</summary>
internal static class PingStatusText
{
    public static string Describe(IPStatus status) => status switch
    {
        IPStatus.Success => "成功",
        IPStatus.TimedOut => "超时",
        IPStatus.TtlExpired => "TTL 过期",
        IPStatus.TtlReassemblyTimeExceeded => "重组超时",
        IPStatus.DestinationHostUnreachable => "目标主机不可达",
        IPStatus.DestinationNetworkUnreachable => "目标网络不可达",
        IPStatus.DestinationPortUnreachable => "目标端口不可达",
        IPStatus.DestinationProhibited => "目标被禁止",
        IPStatus.PacketTooBig => "包过大",
        IPStatus.ParameterProblem => "参数错误",
        IPStatus.SourceQuench => "源抑制",
        _ => status.ToString()
    };
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ToolBox.Tools.NetDiagnostics;

/// <summary>
/// 通过 iphlpapi 的 GetExtendedTcpTable / GetExtendedUdpTable 枚举本机 TCP/UDP 连接
/// （含 IPv6）及归属进程 PID，比解析 netstat -ano 输出干净且无残留进程。
/// </summary>
internal static class ConnectionTableHelper
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int TcpTableOwnerPid = 5;
    private const int UdpTableOwnerPid = 1;

    [DllImport("iphlpapi.dll")]
    private static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool order, int addressFamily, int tableClass, int reserved);

    [DllImport("iphlpapi.dll")]
    private static extern int GetExtendedUdpTable(IntPtr table, ref int size, bool order, int addressFamily, int tableClass, int reserved);

    public sealed class ConnectionEntry
    {
        public string Protocol { get; init; } = "";
        public string Local { get; init; } = "";
        public string Remote { get; init; } = "";
        public string State { get; init; } = "";
        public uint Pid { get; init; }
        public string ProcessName { get; set; } = "";
    }

    public static List<ConnectionEntry> GetConnections()
    {
        var rows = new List<(string Proto, string Local, string Remote, string State, uint Pid)>();
        ReadTcpTable(rows, ipv4: true);
        ReadTcpTable(rows, ipv4: false);
        ReadUdpTable(rows, ipv4: true);
        ReadUdpTable(rows, ipv4: false);

        var names = new Dictionary<uint, string>();
        var list = new List<ConnectionEntry>(rows.Count);
        foreach (var row in rows)
        {
            if (!names.TryGetValue(row.Pid, out var processName))
            {
                if (row.Pid != 0)
                {
                    try
                    {
                        using var process = Process.GetProcessById((int)row.Pid);
                        processName = process.ProcessName;
                    }
                    catch (Exception)
                    {
                        // 进程已退出或权限不足，只显示 PID
                        processName = "";
                    }
                }
                else
                {
                    processName = "系统";
                }
                names[row.Pid] = processName;
            }

            list.Add(new ConnectionEntry
            {
                Protocol = row.Proto,
                Local = row.Local,
                Remote = row.Remote,
                State = row.State,
                Pid = row.Pid,
                ProcessName = processName
            });
        }
        return list;
    }

    private static void ReadTcpTable(List<(string Proto, string Local, string Remote, string State, uint Pid)> rows, bool ipv4)
    {
        var addressFamily = ipv4 ? AfInet : AfInet6;
        var size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, addressFamily, TcpTableOwnerPid, 0);
        if (size <= 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, addressFamily, TcpTableOwnerPid, 0) != 0)
            {
                return;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowSize = ipv4 ? Marshal.SizeOf<TcpRow>() : Marshal.SizeOf<Tcp6Row>();
            for (var i = 0; i < count; i++)
            {
                var rowPtr = IntPtr.Add(buffer, sizeof(int) + i * rowSize);
                if (ipv4)
                {
                    var row = Marshal.PtrToStructure<TcpRow>(rowPtr);
                    rows.Add(("TCP",
                        EndPointText(ToAddress(row.LocalAddr), ToPort(row.LocalPort)),
                        RemoteText(ToAddress(row.RemoteAddr), ToPort(row.RemotePort)),
                        DescribeTcpState(row.State),
                        row.OwningPid));
                }
                else
                {
                    var row = Marshal.PtrToStructure<Tcp6Row>(rowPtr);
                    rows.Add(("TCP6",
                        EndPointText(new IPAddress(row.LocalAddr, row.LocalScopeId), ToPort(row.LocalPort)),
                        RemoteText(new IPAddress(row.RemoteAddr, row.RemoteScopeId), ToPort(row.RemotePort)),
                        DescribeTcpState(row.State),
                        row.OwningPid));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ReadUdpTable(List<(string Proto, string Local, string Remote, string State, uint Pid)> rows, bool ipv4)
    {
        var addressFamily = ipv4 ? AfInet : AfInet6;
        var size = 0;
        GetExtendedUdpTable(IntPtr.Zero, ref size, false, addressFamily, UdpTableOwnerPid, 0);
        if (size <= 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buffer, ref size, false, addressFamily, UdpTableOwnerPid, 0) != 0)
            {
                return;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowSize = ipv4 ? Marshal.SizeOf<UdpRow>() : Marshal.SizeOf<Udp6Row>();
            for (var i = 0; i < count; i++)
            {
                var rowPtr = IntPtr.Add(buffer, sizeof(int) + i * rowSize);
                if (ipv4)
                {
                    var row = Marshal.PtrToStructure<UdpRow>(rowPtr);
                    rows.Add(("UDP",
                        EndPointText(ToAddress(row.LocalAddr), ToPort(row.LocalPort)),
                        "—", "—", row.OwningPid));
                }
                else
                {
                    var row = Marshal.PtrToStructure<Udp6Row>(rowPtr);
                    rows.Add(("UDP6",
                        EndPointText(new IPAddress(row.LocalAddr, row.LocalScopeId), ToPort(row.LocalPort)),
                        "—", "—", row.OwningPid));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // 表中地址/端口均为网络字节序：DWORD 转字节数组后反转即 IPv4 地址字节；端口取低 16 位高低字节交换
    private static IPAddress ToAddress(uint value) => new(BitConverter.GetBytes(value).Reverse().ToArray());

    private static ushort ToPort(uint value) => (ushort)(((value & 0xFF) << 8) | ((value >> 8) & 0xFF));

    private static string EndPointText(IPAddress address, ushort port) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]:{port}" : $"{address}:{port}";

    private static string RemoteText(IPAddress address, ushort port) => port == 0 ? "—" : EndPointText(address, port);

    private static string DescribeTcpState(uint state) => state switch
    {
        1 => "CLOSED",
        2 => "LISTENING",
        3 => "SYN_SENT",
        4 => "SYN_RCVD",
        5 => "ESTABLISHED",
        6 => "FIN_WAIT_1",
        7 => "FIN_WAIT_2",
        8 => "CLOSE_WAIT",
        9 => "CLOSING",
        10 => "LAST_ACK",
        11 => "TIME_WAIT",
        12 => "DELETE_TCB",
        _ => $"UNKNOWN({state})"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRow
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Tcp6Row
    {
        public uint State;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UdpRow
    {
        public uint LocalAddr;
        public uint LocalPort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Udp6Row
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        public uint OwningPid;
    }
}

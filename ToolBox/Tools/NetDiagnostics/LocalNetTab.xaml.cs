using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

public partial class LocalNetTab : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ObservableCollection<AdapterRow> _adapters = new();
    private readonly ObservableCollection<ConnectionTableHelper.ConnectionEntry> _connections = new();
    private List<ConnectionTableHelper.ConnectionEntry> _allConnections = new();
    private bool _refreshing;
    private bool _loaded;

    public LocalNetTab()
    {
        InitializeComponent();
        AdapterGrid.ItemsSource = _adapters;
        ConnGrid.ItemsSource = _connections;
        CbProto.SelectedIndex = 0;
        CbState.SelectedIndex = 0;
        _timer.Tick += (_, _) => RefreshConnections();
        // 页面在外壳启动时即被构造，首次数据加载推迟到页面可见时
        Loaded += OnFirstLoaded;
    }

    private void OnFirstLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        RefreshAdapters();
        RefreshConnections();
    }

    /// <summary>宿主窗口关闭时停止自动刷新。</summary>
    public void StopAutoRefresh() => _timer.Stop();

    private void ChkAuto_Checked(object sender, RoutedEventArgs e) => _timer.Start();

    private void ChkAuto_Unchecked(object sender, RoutedEventArgs e) => _timer.Stop();

    private void BtnRefreshAdapters_Click(object sender, RoutedEventArgs e) => RefreshAdapters();

    private void BtnRefreshConnections_Click(object sender, RoutedEventArgs e) => RefreshConnections();

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyConnections();

    private void RefreshAdapters()
    {
        _adapters.Clear();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                     .OrderBy(n => n.OperationalStatus == OperationalStatus.Up ? 0 : 1)
                     .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            var props = nic.GetIPProperties();
            string sent, received;
            try
            {
                var stats = nic.GetIPStatistics();
                sent = FormatBytes(stats.BytesSent);
                received = FormatBytes(stats.BytesReceived);
            }
            catch
            {
                // 个别虚拟网卡不支持统计查询
                sent = received = "-";
            }

            _adapters.Add(new AdapterRow
            {
                Name = nic.Name,
                Type = nic.NetworkInterfaceType.ToString(),
                Status = nic.OperationalStatus.ToString(),
                Mac = string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                Ipv4 = string.Join("; ", props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())),
                Ipv6 = string.Join("; ", props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(a => a.Address.ToString())),
                Gateway = string.Join("; ", props.GatewayAddresses.Select(g => g.Address.ToString())),
                Dns = string.Join("; ", props.DnsAddresses.Select(d => d.ToString())),
                Sent = sent,
                Received = received
            });
        }
        TxtAdapterCount.Text = $"共 {_adapters.Count} 个网卡";
    }

    private async void RefreshConnections()
    {
        if (_refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            var entries = await Task.Run(ConnectionTableHelper.GetConnections);
            _allConnections = entries;
            await Dispatcher.InvokeAsync(ApplyConnections);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplyConnections()
    {
        var proto = (CbProto.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var state = (CbState.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filter = TxtFilter.Text.Trim();

        _connections.Clear();
        foreach (var entry in _allConnections.Where(en => Matches(en, proto, state, filter)))
        {
            _connections.Add(entry);
        }
        TxtConnCount.Text = $"共 {_connections.Count} / {_allConnections.Count} 条";
    }

    private static bool Matches(ConnectionTableHelper.ConnectionEntry entry, string? proto, string? state, string filter)
    {
        if (proto == "TCP" && !entry.Protocol.StartsWith("TCP"))
        {
            return false;
        }
        if (proto == "UDP" && !entry.Protocol.StartsWith("UDP"))
        {
            return false;
        }
        if (state == "监听" && entry.State != "LISTENING")
        {
            return false;
        }
        if (state == "已建立" && entry.State != "ESTABLISHED")
        {
            return false;
        }
        if (filter.Length == 0)
        {
            return true;
        }
        return entry.Local.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Remote.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Pid.ToString().Contains(filter, StringComparison.Ordinal);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F1} KB",
        _ => $"{bytes} B"
    };
}

public sealed class AdapterRow
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Status { get; init; } = "";
    public string Mac { get; init; } = "";
    public string Ipv4 { get; init; } = "";
    public string Ipv6 { get; init; } = "";
    public string Gateway { get; init; } = "";
    public string Dns { get; init; } = "";
    public string Sent { get; init; } = "";
    public string Received { get; init; } = "";
}

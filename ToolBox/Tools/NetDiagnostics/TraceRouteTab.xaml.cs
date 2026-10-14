using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

public partial class TraceRouteTab : UserControl
{
    private readonly ObservableCollection<TraceRow> _rows = new();
    private readonly Brush _normalStatusBrush;
    private CancellationTokenSource? _cts;

    public TraceRouteTab()
    {
        InitializeComponent();
        _normalStatusBrush = TxtStatus.Foreground;
        ResultGrid.ItemsSource = _rows;
    }

    /// <summary>宿主窗口关闭时取消进行中的追踪。</summary>
    public void CancelRun() => _cts?.Cancel();

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var target = TxtTarget.Text.Trim();
        if (target.Length == 0)
        {
            SetStatus("请先输入目标主机（域名或 IP）。", warning: true);
            return;
        }
        if (!int.TryParse(TxtMaxHops.Text.Trim(), out var maxHops) || maxHops is < 1 or > 64)
        {
            SetStatus("最大跃点数需为 1-64 的整数。", warning: true);
            return;
        }
        if (!int.TryParse(TxtProbes.Text.Trim(), out var probes) || probes is < 1 or > 10)
        {
            SetStatus("探测次数需为 1-10 的整数。", warning: true);
            return;
        }
        if (!int.TryParse(TxtTimeout.Text.Trim(), out var timeout) || timeout <= 0)
        {
            SetStatus("超时需为正整数（毫秒）。", warning: true);
            return;
        }

        _rows.Clear();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        SetStatus("路由追踪中…");

        try
        {
            await RunTraceAsync(target, maxHops, probes, timeout, ct);
        }
        finally
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async Task RunTraceAsync(string target, int maxHops, int probes, int timeout, CancellationToken ct)
    {
        var buffer = new byte[32];
        using var ping = new Ping();

        for (var ttl = 1; ttl <= maxHops && !ct.IsCancellationRequested; ttl++)
        {
            var row = new TraceRow { Hop = ttl };
            var reached = false;

            for (var p = 0; p < probes && !ct.IsCancellationRequested; p++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(timeout), buffer, new PingOptions(ttl, false), ct);
                    if (reply.Status is IPStatus.Success or IPStatus.TtlExpired)
                    {
                        reached = reply.Status == IPStatus.Success;
                        row.Probes.Add(reply.RoundtripTime);
                        row.Ip ??= reply.Address.ToString();
                    }
                    else
                    {
                        row.Probes.Add(null);
                        if (reply.Status != IPStatus.TimedOut)
                        {
                            row.Note = PingStatusText.Describe(reply.Status);
                        }
                    }
                }
                catch (PingException ex)
                {
                    row.Note = ex.InnerException?.Message ?? ex.Message;
                    row.Probes.Add(null);
                }
                catch (OperationCanceledException)
                {
                    SetStatus("已取消。");
                    return;
                }
            }

            // 反解路由器主机名，失败（很常见）则直接显示 IP
            if (row.Ip is { } ip)
            {
                try
                {
                    var entry = await Dns.GetHostEntryAsync(ip, ct);
                    row.Host = entry.HostName;
                }
                catch
                {
                    row.Host = ip;
                }
            }

            await Dispatcher.InvokeAsync(() => _rows.Add(row));

            if (reached)
            {
                SetStatus($"已到达目标，共 {ttl} 跳。");
                return;
            }
        }

        if (ct.IsCancellationRequested)
        {
            SetStatus("已取消。");
        }
        else
        {
            SetStatus($"已达最大跃点数 {maxHops} 仍未到达目标。", warning: true);
        }
    }

    private void SetStatus(string text, bool warning = false)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = warning ? StatusColors.Warning : _normalStatusBrush;
    }
}

public sealed class TraceRow
{
    public int Hop { get; init; }
    public string? Ip { get; set; }
    public string Host { get; set; } = "";
    public List<long?> Probes { get; } = new();
    public string? Note { get; set; }

    public string ProbeDisplay => string.Join("  /  ", Probes.Select(p => p is null ? "*" : $"{p} ms"));
    public string AvgDisplay
    {
        get
        {
            var valid = Probes.Where(p => p.HasValue).ToList();
            return valid.Count == 0 ? "-" : $"{valid.Average(p => p!.Value):F0}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

public partial class PortTestTab : UserControl
{
    private readonly ObservableCollection<PortResultRow> _rows = new();
    private readonly Brush _normalStatusBrush;
    private CancellationTokenSource? _cts;

    public PortTestTab()
    {
        InitializeComponent();
        _normalStatusBrush = TxtStatus.Foreground;
        ResultGrid.ItemsSource = _rows;
    }

    /// <summary>宿主窗口关闭时取消进行中的测试。</summary>
    public void CancelRun() => _cts?.Cancel();

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var host = TxtHost.Text.Trim();
        if (host.Length == 0)
        {
            SetStatus("请先输入目标主机（域名或 IP）。", warning: true);
            return;
        }
        if (!TryParsePorts(TxtPorts.Text, out var ports, out var error))
        {
            SetStatus(error, warning: true);
            return;
        }
        if (!int.TryParse(TxtTimeout.Text.Trim(), out var timeout) || timeout <= 0)
        {
            SetStatus("超时需为正整数（毫秒）。", warning: true);
            return;
        }
        if (!int.TryParse(TxtConcurrency.Text.Trim(), out var concurrency) || concurrency is < 1 or > 128)
        {
            SetStatus("并发需为 1-128 的整数。", warning: true);
            return;
        }

        _rows.Clear();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (Exception ex)
        {
            SetStatus("域名解析失败：" + ex.Message, warning: true);
            ResetButtons();
            return;
        }
        if (addresses.Length == 0)
        {
            SetStatus("域名无解析结果。", warning: true);
            ResetButtons();
            return;
        }

        SetStatus($"目标 {host}（{addresses.Length} 个地址 × {ports.Count} 个端口）测试中…");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var targets = addresses.SelectMany(address => ports.Select(port => (Address: address, Port: port)));
            await Parallel.ForEachAsync(targets,
                new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct },
                async (target, token) =>
                {
                    var row = await TestAsync(target.Address, target.Port, timeout, token);
                    await Dispatcher.InvokeAsync(() => _rows.Add(row));
                });
            stopwatch.Stop();

            // 完成后按地址 + 端口排序，便于阅读
            var sorted = _rows.OrderBy(r => r.Address, AddressOrderer).ThenBy(r => r.Port).ToList();
            _rows.Clear();
            foreach (var row in sorted)
            {
                _rows.Add(row);
            }

            SetStatus($"完成：共 {sorted.Count} 项，开放 {sorted.Count(r => r.Result == "开放")}，" +
                      $"关闭 {sorted.Count(r => r.Result == "关闭")}，超时 {sorted.Count(r => r.Result == "超时")}，" +
                      $"耗时 {stopwatch.ElapsedMilliseconds / 1000.0:F1} s。");
        }
        catch (OperationCanceledException)
        {
            SetStatus($"已取消（已完成 {_rows.Count} 项）。");
        }
        finally
        {
            ResetButtons();
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void ResetButtons()
    {
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        _cts?.Dispose();
        _cts = null;
    }

    private static async Task<PortResultRow> TestAsync(IPAddress address, int port, int timeout, CancellationToken outer)
    {
        var stopwatch = Stopwatch.StartNew();
        string result;
        using var tcp = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await tcp.ConnectAsync(address, port, timeoutCts.Token);
            result = "开放";
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            result = "超时";
        }
        catch (SocketException ex)
        {
            result = ex.SocketErrorCode == SocketError.ConnectionRefused ? "关闭" : "错误：" + ex.SocketErrorCode;
        }
        catch (Exception ex)
        {
            result = "错误：" + ex.Message;
        }
        stopwatch.Stop();
        return new PortResultRow
        {
            Address = address.ToString(),
            Port = port,
            Result = result,
            Elapsed = stopwatch.ElapsedMilliseconds
        };
    }

    private static bool TryParsePorts(string text, out List<int> ports, out string error)
    {
        ports = new List<int>();
        error = "";
        var tokens = text.Split(new[] { ';', '；', ',', '，', ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var range = token.Split('-');
            if (range.Length == 1)
            {
                if (!int.TryParse(range[0], out var port) || port is < 1 or > 65535)
                {
                    error = $"端口格式不正确：{token}";
                    return false;
                }
                ports.Add(port);
            }
            else if (range.Length == 2)
            {
                if (!int.TryParse(range[0], out var lo) || !int.TryParse(range[1], out var hi)
                    || lo < 1 || hi > 65535 || lo > hi)
                {
                    error = $"端口范围不正确：{token}";
                    return false;
                }
                if (hi - lo + 1 > 5000)
                {
                    error = $"单个范围不能超过 5000 个端口：{token}";
                    return false;
                }
                for (var port = lo; port <= hi; port++)
                {
                    ports.Add(port);
                }
            }
            else
            {
                error = $"端口格式不正确：{token}";
                return false;
            }
        }

        ports = ports.Distinct().ToList();
        if (ports.Count == 0)
        {
            error = "请输入要测试的端口。";
            return false;
        }
        return true;
    }

    // IPv4 优先于 IPv6，纯字符串排序即可满足展示需求
    private static readonly IComparer<string> AddressOrderer = Comparer<string>.Create((a, b) =>
    {
        var aV4 = a.Contains('.');
        var bV4 = b.Contains('.');
        return aV4 != bV4 ? (aV4 ? -1 : 1) : string.CompareOrdinal(a, b);
    });

    private void SetStatus(string text, bool warning = false)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = warning ? StatusColors.Warning : _normalStatusBrush;
    }
}

public sealed class PortResultRow
{
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string Result { get; init; } = "";
    public long Elapsed { get; init; }
}

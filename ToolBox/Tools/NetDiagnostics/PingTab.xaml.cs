using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

public partial class PingTab : UserControl
{
    // 连续模式下限制明细行数，避免长时间运行撑爆内存/渲染
    private const int MaxDetailRows = 5000;

    private readonly ObservableCollection<PingSummaryRow> _summaries = new();
    private readonly ObservableCollection<PingDetailRow> _details = new();
    private readonly Brush _normalStatusBrush;
    private CancellationTokenSource? _cts;

    public PingTab()
    {
        InitializeComponent();
        _normalStatusBrush = TxtStatus.Foreground;
        SummaryGrid.ItemsSource = _summaries;
        ResultGrid.ItemsSource = _details;
    }

    /// <summary>宿主窗口关闭时取消进行中的探测。</summary>
    public void CancelRun() => _cts?.Cancel();

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var targets = TxtTargets.Text
            .Split(new[] { ';', '；', ',', '，', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();
        if (targets.Count == 0)
        {
            SetStatus("请先输入目标主机（域名或 IP）。", warning: true);
            return;
        }

        if (!int.TryParse(TxtCount.Text.Trim(), out var count) || count < 0
            || !int.TryParse(TxtTimeout.Text.Trim(), out var timeout) || timeout <= 0
            || !int.TryParse(TxtInterval.Text.Trim(), out var interval) || interval < 0)
        {
            SetStatus("次数 / 超时 / 间隔需为非负整数，且超时需大于 0。", warning: true);
            return;
        }

        _details.Clear();
        _summaries.Clear();
        foreach (var target in targets)
        {
            _summaries.Add(new PingSummaryRow { Target = target });
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        SetStatus(count == 0
            ? $"连续 Ping {targets.Count} 个目标，点击“停止”结束…"
            : $"正在 Ping {targets.Count} 个目标…");

        try
        {
            await Task.WhenAll(targets.Select(t => RunTargetAsync(t, count, timeout, interval, ct)).ToArray());
            SetStatus(ct.IsCancellationRequested ? "已停止。" : "Ping 完成。");
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

    private async Task RunTargetAsync(string target, int count, int timeout, int interval, CancellationToken ct)
    {
        var summary = _summaries.First(s => s.Target == target);
        var buffer = new byte[32];
        var options = new PingOptions { DontFragment = false };
        var seq = 0;
        using var ping = new Ping();

        while (!ct.IsCancellationRequested && (count == 0 || seq < count))
        {
            seq++;
            PingReply? reply = null;
            string? error = null;
            var sw = Stopwatch.StartNew();
            try
            {
                reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(timeout), buffer, options, ct);
            }
            catch (PingException ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            sw.Stop();

            var row = new PingDetailRow
            {
                Target = target,
                Seq = seq,
                Time = DateTime.Now.ToString("HH:mm:ss")
            };
            if (reply is not null)
            {
                row.Ip = reply.Status is IPStatus.Success or IPStatus.TtlExpired && reply.Address is not null
                    ? reply.Address.ToString()
                    : "";
                row.Status = PingStatusText.Describe(reply.Status);
                row.Rtt = reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            }
            else
            {
                row.Status = error ?? "未知错误";
            }

            await Dispatcher.InvokeAsync(() =>
            {
                _details.Add(row);
                TrimDetails();
                summary.Record(row.Ip, row.Rtt);
            });

            if (count != 0 && seq >= count)
            {
                break;
            }

            var wait = (int)Math.Max(200, interval - sw.ElapsedMilliseconds);
            try
            {
                await Task.Delay(wait, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void TrimDetails()
    {
        while (_details.Count > MaxDetailRows)
        {
            _details.RemoveAt(0);
        }
    }

    private void SetStatus(string text, bool warning = false)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = warning ? StatusColors.Warning : _normalStatusBrush;
    }
}

public sealed class PingDetailRow
{
    public string Target { get; init; } = "";
    public string Ip { get; set; } = "";
    public int Seq { get; init; }
    public string Status { get; set; } = "";
    public long? Rtt { get; set; }
    public string RttDisplay => Rtt is { } value ? value.ToString() : "-";
    public string Time { get; set; } = "";
}

public sealed class PingSummaryRow : INotifyPropertyChanged
{
    private string? _ip;
    private int _sent;
    private int _received;
    private long? _min;
    private long? _max;
    private long _sum;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Target { get; init; } = "";

    public string? Ip
    {
        get => _ip;
        private set => Set(ref _ip, value);
    }

    public int Sent
    {
        get => _sent;
        private set => Set(ref _sent, value);
    }

    public int Received
    {
        get => _received;
        private set => Set(ref _received, value);
    }

    public string LossDisplay => Sent == 0 ? "-" : $"{(Sent - Received) * 100.0 / Sent:F1}%";
    public string MinDisplay => _min?.ToString() ?? "-";
    public string AvgDisplay => Received == 0 ? "-" : $"{_sum / (double)Received:F0}";
    public string MaxDisplay => _max?.ToString() ?? "-";

    /// <summary>在 UI 线程上调用：登记一次探测结果并刷新汇总。</summary>
    public void Record(string? ip, long? rtt)
    {
        if (ip is { Length: > 0 })
        {
            Ip = ip;
        }
        Sent++;
        if (rtt is { } value)
        {
            Received++;
            _sum += value;
            if (_min is null || value < _min)
            {
                _min = value;
            }
            if (_max is null || value > _max)
            {
                _max = value;
            }
        }
        Raise(nameof(LossDisplay));
        Raise(nameof(MinDisplay));
        Raise(nameof(AvgDisplay));
        Raise(nameof(MaxDisplay));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        Raise(name!);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

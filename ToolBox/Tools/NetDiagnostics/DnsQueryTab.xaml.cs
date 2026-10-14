using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using DnsClient;

namespace ToolBox.Tools.NetDiagnostics;

public partial class DnsQueryTab : UserControl
{
    private readonly ObservableCollection<DnsRecordRow> _rows = new();
    private readonly Brush _normalStatusBrush;
    private CancellationTokenSource? _cts;

    public DnsQueryTab()
    {
        InitializeComponent();
        _normalStatusBrush = TxtStatus.Foreground;
        ResultGrid.ItemsSource = _rows;
        CbType.SelectedIndex = 0;
    }

    /// <summary>宿主窗口关闭时取消进行中的查询。</summary>
    public void CancelRun() => _cts?.Cancel();

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (name.Length == 0)
        {
            SetStatus("请先输入要查询的域名或 IP。", warning: true);
            return;
        }

        var serverText = TxtServer.Text.Trim();
        LookupClientOptions options;
        if (serverText.Length == 0)
        {
            options = new LookupClientOptions();
        }
        else
        {
            var servers = serverText
                .Split(new[] { ';', '；', ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(text => IPAddress.TryParse(text, out var address) ? address : null)
                .ToList();
            if (servers.Any(address => address is null))
            {
                SetStatus("DNS 服务器地址格式不正确。", warning: true);
                return;
            }
            options = new LookupClientOptions(servers.Select(address => address!).ToArray());
        }
        options.UseCache = false;
        options.UseTcpFallback = true;
        options.Retries = 1;

        var typeText = (CbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "A";
        var queryType = Enum.Parse<QueryType>(typeText);

        _rows.Clear();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        BtnQuery.IsEnabled = false;
        BtnCancel.IsEnabled = true;
        SetStatus("查询中…");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = new LookupClient(options);
            var response = await client.QueryAsync(name, queryType, QueryClass.IN, ct);
            stopwatch.Stop();

            if (response.HasError)
            {
                SetStatus($"查询失败：{response.ErrorMessage}", warning: true);
            }
            else
            {
                foreach (var record in response.Answers)
                {
                    _rows.Add(new DnsRecordRow
                    {
                        Name = record.DomainName.ToString(),
                        Type = record.RecordType.ToString(),
                        Ttl = record.TimeToLive,
                        Value = record.ToString()
                    });
                }
                var server = serverText.Length == 0 ? "系统默认" : serverText;
                SetStatus(_rows.Count == 0
                    ? $"无记录（{server}，耗时 {stopwatch.ElapsedMilliseconds} ms）"
                    : $"共 {_rows.Count} 条记录（{server}，耗时 {stopwatch.ElapsedMilliseconds} ms）");
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消。");
        }
        catch (Exception ex)
        {
            SetStatus("查询失败：" + (ex.InnerException?.Message ?? ex.Message), warning: true);
        }
        finally
        {
            BtnQuery.IsEnabled = true;
            BtnCancel.IsEnabled = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void SetStatus(string text, bool warning = false)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = warning ? StatusColors.Warning : _normalStatusBrush;
    }
}

public sealed class DnsRecordRow
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Ttl { get; init; }
    public string Value { get; init; } = "";
}

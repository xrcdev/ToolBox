using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.NetDiagnostics;

public partial class HttpDebugTab : UserControl
{
    private const long MaxBodyBytes = 10 * 1024 * 1024;

    // HTTP 规范中归属实体而非请求本身的头，需写入 request.Content.Headers
    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Length", "Content-Disposition", "Content-Encoding",
        "Content-Language", "Content-Location", "Content-Range", "Expires", "Last-Modified"
    };

    private readonly ObservableCollection<HeaderRow> _responseHeaders = new();
    private readonly Brush _normalStatusBrush;
    private CancellationTokenSource? _cts;

    public HttpDebugTab()
    {
        InitializeComponent();
        _normalStatusBrush = TxtStatus.Foreground;
        HeaderGrid.ItemsSource = _responseHeaders;
        CbMethod.SelectedIndex = 0;
    }

    /// <summary>宿主窗口关闭时取消进行中的请求。</summary>
    public void CancelRun() => _cts?.Cancel();

    private async void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        var methodText = CbMethod.Text.Trim().ToUpperInvariant();
        HttpMethod method;
        try
        {
            method = new HttpMethod(methodText);
        }
        catch (ArgumentException)
        {
            ShowError("HTTP 方法无效。");
            return;
        }

        if (!Uri.TryCreate(TxtUrl.Text.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowError("URL 需为以 http:// 或 https:// 开头的完整地址。");
            return;
        }
        if (!int.TryParse(TxtTimeoutSec.Text.Trim(), out var timeoutSec) || timeoutSec <= 0)
        {
            ShowError("超时需为正整数（秒）。");
            return;
        }

        var headers = ParseHeaders(TxtHeaders.Text, out var badLines);
        if (badLines.Count > 0)
        {
            ShowError("以下请求头行格式不正确（应为 Name: Value）：\n" + string.Join("\n", badLines));
            return;
        }
        var bodyText = TxtRequestBody.Text;

        _responseHeaders.Clear();
        TxtPhases.Text = "";
        TxtBody.Text = "";
        SetStatus("请求中…");

        _cts = new CancellationTokenSource();
        var userCt = _cts.Token;
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(userCt);
        requestCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
        BtnSend.IsEnabled = false;
        BtnCancel.IsEnabled = true;

        try
        {
            await SendAsync(uri, method, headers, bodyText, requestCts.Token);
        }
        catch (OperationCanceledException) when (userCt.IsCancellationRequested)
        {
            SetStatus("已取消。");
        }
        catch (OperationCanceledException)
        {
            SetStatus($"请求超时（{timeoutSec} 秒）。");
        }
        catch (Exception ex)
        {
            ShowError("请求失败：" + Describe(ex));
        }
        finally
        {
            BtnSend.IsEnabled = true;
            BtnCancel.IsEnabled = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async Task SendAsync(Uri uri, HttpMethod method, List<(string Name, string Value)> headers,
        string bodyText, CancellationToken ct)
    {
        var phases = new List<(string Name, long Ms)>();
        string? phaseError = null;

        // 分阶段耗时用独立探测连接测量，仅供参考（正式请求由 HttpClient 另行建连）
        try
        {
            var probeWatch = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            phases.Add(("DNS 解析", probeWatch.ElapsedMilliseconds));

            var address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is not null)
            {
                using var probe = new TcpClient();
                probeWatch.Restart();
                await probe.ConnectAsync(address, uri.Port, ct);
                phases.Add(("TCP 连接", probeWatch.ElapsedMilliseconds));

                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    var sslOptions = new SslClientAuthenticationOptions { TargetHost = uri.Host };
                    if (ChkIgnoreCert.IsChecked == true)
                    {
                        sslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                    }
                    using var sslStream = new SslStream(probe.GetStream());
                    probeWatch.Restart();
                    await sslStream.AuthenticateAsClientAsync(sslOptions, ct);
                    phases.Add(("TLS 握手", probeWatch.ElapsedMilliseconds));
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            phaseError = Describe(ex);
        }

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = ChkRedirect.IsChecked == true,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        };
        if (ChkIgnoreCert.IsChecked == true)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }
        using var client = new HttpClient(handler, disposeHandler: true);

        using var request = new HttpRequestMessage(method, uri);
        // 允许 TLS 上协商到 HTTP/2，响应里展示实际版本
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

        var hasBody = bodyText.Length > 0 && method != HttpMethod.Get && method != HttpMethod.Head;
        if (hasBody)
        {
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyText));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        foreach (var (name, value) in headers)
        {
            if (!ApplyHeader(request, name, value))
            {
                throw new FormatException($"无法添加请求头：{name}" +
                    (hasBody ? "" : "（该头属于请求体，但当前请求没有请求体）"));
            }
        }

        var watch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var ttfb = watch.ElapsedMilliseconds;

        var (bytes, truncated) = await ReadBodyAsync(response, ct);
        watch.Stop();

        var sizeText = truncated ? $"{bytes.Length}+ 字节（超出预览上限已截断）" : $"{bytes.Length} 字节";
        SetStatus($"{(int)response.StatusCode} {response.ReasonPhrase}    HTTP/{response.Version}    " +
                  $"首字节 {ttfb} ms    总耗时 {watch.ElapsedMilliseconds} ms    大小 {sizeText}");

        var phaseLines = phases.Select(p => $"{p.Name}：{p.Ms} ms").ToList();
        if (phaseError is not null)
        {
            phaseLines.Add("阶段探测失败：" + phaseError);
        }
        phaseLines.Add("（阶段耗时来自独立探测连接，仅供参考）");
        TxtPhases.Text = string.Join("\n", phaseLines);

        foreach (var header in response.Headers)
        {
            _responseHeaders.Add(new HeaderRow { Name = header.Key, Value = string.Join(", ", header.Value) });
        }
        foreach (var header in response.Content.Headers)
        {
            _responseHeaders.Add(new HeaderRow { Name = header.Key, Value = string.Join(", ", header.Value) });
        }

        TxtBody.Text = BuildBodyText(response, bytes, truncated);
    }

    /// <summary>按 HTTP 语义把头写入请求头或内容头；返回 false 表示无法承载（如无请求体时的 Content-Type）。</summary>
    private static bool ApplyHeader(HttpRequestMessage request, string name, string value)
    {
        if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Host = value;
            return true;
        }
        if (!ContentHeaderNames.Contains(name))
        {
            return request.Headers.TryAddWithoutValidation(name, value);
        }
        if (request.Content is null)
        {
            return false;
        }
        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            // 覆盖默认的 application/json
            request.Content.Headers.Remove("Content-Type");
        }
        return request.Content.Headers.TryAddWithoutValidation(name, value);
    }

    private static async Task<(byte[] Bytes, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read <= 0)
            {
                break;
            }
            var remaining = MaxBodyBytes - buffer.Length;
            if (read > remaining)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, (int)remaining), ct);
                return (buffer.ToArray(), true);
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            if (buffer.Length >= MaxBodyBytes)
            {
                return (buffer.ToArray(), true);
            }
        }
        return (buffer.ToArray(), false);
    }

    private static string BuildBodyText(HttpResponseMessage response, byte[] bytes, bool truncated)
    {
        if (IsBinary(response.Content.Headers.ContentType?.MediaType, bytes))
        {
            return $"（二进制内容 {bytes.Length} 字节，未预览）";
        }
        var encoding = TryGetEncoding(response.Content.Headers.ContentType?.CharSet) ?? Encoding.UTF8;
        var text = encoding.GetString(bytes);
        return truncated ? text + "\n…（超出预览上限已截断）" : text;
    }

    private static bool IsBinary(string? mediaType, byte[] bytes)
    {
        if (mediaType is not null)
        {
            if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (mediaType.Contains("json") || mediaType.Contains("xml") || mediaType.Contains("javascript")
                || mediaType.Contains("yaml") || mediaType.Contains("csv")
                || mediaType.Contains("x-www-form-urlencoded"))
            {
                return false;
            }
        }

        // 类型未知时按内容判断：前 1KB 出现 NUL 或大量不可打印字符视为二进制
        var limit = Math.Min(bytes.Length, 1024);
        if (limit == 0)
        {
            return false;
        }
        var suspicious = 0;
        for (var i = 0; i < limit; i++)
        {
            var b = bytes[i];
            if (b == 0)
            {
                return true;
            }
            if (b < 9 || (b > 13 && b < 32))
            {
                suspicious++;
            }
        }
        return suspicious * 100.0 / limit > 10;
    }

    private static Encoding? TryGetEncoding(string? charset)
    {
        if (string.IsNullOrEmpty(charset))
        {
            return null;
        }
        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return null;
        }
    }

    private static List<(string Name, string Value)> ParseHeaders(string text, out List<string> badLines)
    {
        var headers = new List<(string Name, string Value)>();
        badLines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                continue;
            }
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                badLines.Add(line);
                continue;
            }
            headers.Add((line[..separatorIndex].Trim(), line[(separatorIndex + 1)..].Trim()));
        }
        return headers;
    }

    private static string Describe(Exception ex) => ex.InnerException?.Message ?? ex.Message;

    private void SetStatus(string text)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = _normalStatusBrush;
    }

    private void ShowError(string message)
    {
        TxtStatus.Text = "⚠ " + message;
        TxtStatus.Foreground = StatusColors.Warning;
    }
}

public sealed class HeaderRow
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}

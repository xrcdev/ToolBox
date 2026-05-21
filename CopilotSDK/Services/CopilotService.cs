using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopilotSDKDemo.Models;
using Microsoft.Extensions.Logging;

namespace CopilotSDKDemo.Services;

/// <summary>
/// GitHub Copilot SDK 核心服务类
/// 提供与 GitHub Copilot API 交互的核心功能
/// </summary>
public class CopilotService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CopilotConfig _config;
    private readonly ILogger<CopilotService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// 初始化 CopilotService 实例
    /// </summary>
    /// <param name="config">Copilot 配置</param>
    /// <param name="logger">日志记录器</param>
    public CopilotService(CopilotConfig config, ILogger<CopilotService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ApiEndpoint),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        // 设置认证头
        if (!string.IsNullOrWhiteSpace(config.GitHubToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.GitHubToken);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CopilotSDK-Demo");
        }

        _logger.LogInformation("CopilotService 初始化完成");
    }

    /// <summary>
    /// 发送 HTTP 请求到 Copilot API
    /// </summary>
    private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("发送请求到 {Endpoint}: {Request}",
                    endpoint, JsonSerializer.Serialize(request, _jsonOptions));
            }

            var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                request,
                _jsonOptions,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API 请求失败: {StatusCode}, 响应: {Body}",
                    response.StatusCode, responseBody);

                throw new HttpRequestException(
                    $"API 请求失败: {response.StatusCode}, 响应: {responseBody}");
            }

            var result = JsonSerializer.Deserialize<TResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            return result ?? throw new InvalidOperationException("反序列化响应失败");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "请求超时");
            throw new TimeoutException("API 请求超时", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API 请求异常");
            throw;
        }
    }

    /// <summary>
    /// 模拟代码补全 API 调用（用于演示）
    /// 实际使用时应替换为真实的 GitHub Copilot API 调用
    /// </summary>
    public async Task<CompletionResponse> GetCompletionAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求代码补全: {Prompt}", request.Prompt[..Math.Min(50, request.Prompt.Length)]);

        // 这里应该是实际的 API 调用
        // var response = await SendRequestAsync<CompletionRequest, CompletionResponse>(
        //     "/completions", request, cancellationToken);

        // 模拟响应用于演示
        await Task.Delay(500, cancellationToken);

        var response = new CompletionResponse
        {
            Model = request.Model,
            Status = ResponseStatus.Success,
            Text = GenerateMockCompletion(request),
            Type = string.IsNullOrEmpty(request.Suffix) ? CompletionType.Prefix : CompletionType.Infix,
            Choices = new List<CompletionChoice>
            {
                new()
                {
                    Text = GenerateMockCompletion(request),
                    Score = 0.95,
                    FinishReason = "length"
                }
            },
            Usage = new Usage
            {
                PromptTokens = request.Prompt.Length / 4,
                CompletionTokens = 100
            }
        };

        _logger.LogInformation("补全完成，生成 {Tokens} 个 token", response.Usage?.CompletionTokens);
        return response;
    }

    /// <summary>
    /// 模拟聊天 API 调用
    /// </summary>
    public async Task<ChatResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送聊天消息: {Prompt}", request.Prompt[..Math.Min(50, request.Prompt.Length)]);

        // 这里应该是实际的 API 调用
        // var response = await SendRequestAsync<ChatRequest, ChatResponse>(
        //     "/chat/completions", request, cancellationToken);

        // 模拟响应用于演示
        await Task.Delay(800, cancellationToken);

        var response = new ChatResponse
        {
            Model = request.Model,
            Status = ResponseStatus.Success,
            Message = new ChatMessage
            {
                Role = "assistant",
                Content = GenerateMockChatResponse(request),
                Timestamp = DateTime.UtcNow
            },
            ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
            SuggestedQuestions = new List<string>
            {
                "如何优化这段代码的性能？",
                "你能提供更多示例吗？",
                "这个方法有哪些替代方案？"
            },
            Usage = new Usage
            {
                PromptTokens = request.Prompt.Length / 4 + request.Messages.Count * 20,
                CompletionTokens = 150
            }
        };

        _logger.LogInformation("聊天响应完成: {Response}", response.Message.Content[..Math.Min(50, response.Message.Content.Length)]);
        return response;
    }

    /// <summary>
    /// 代码解释
    /// </summary>
    public async Task<ExplainResponse> ExplainCodeAsync(
        ExplainRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求代码解释: {Language}, 代码长度: {Length}",
            request.Language, request.Code.Length);

        // 这里应该是实际的 API 调用
        await Task.Delay(600, cancellationToken);

        var response = new ExplainResponse
        {
            Model = request.Language,
            Status = ResponseStatus.Success,
            Explanation = GenerateMockExplanation(request),
            Summary = "这段代码实现了数据验证和转换功能",
            KeyConcepts = new List<string>
            {
                "输入验证",
                "异步编程",
                "错误处理",
                "LINQ 查询"
            },
            Suggestions = request.IncludeSuggestions ? new List<string>
            {
                "考虑使用 Polly 库进行重试处理",
                "可以将验证逻辑提取为单独的方法",
                "添加更多的单元测试覆盖边界情况"
            } : new List<string>(),
            PotentialIssues = new List<string>
            {
                "缺少 null 检查",
                "可能存在性能瓶颈（大数据集时）"
            },
            Usage = new Usage
            {
                PromptTokens = request.Code.Length / 4,
                CompletionTokens = 200
            }
        };

        _logger.LogInformation("代码解释完成");
        return response;
    }

    /// <summary>
    /// 流式代码补全
    /// </summary>
    public async IAsyncEnumerable<string> StreamCompletionAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始流式补全");

        var chunks = new[]
        {
            "public async Task",
            "<IEnumerable<User>>",
            " GetUsersAsync()\n",
            "    {\n",
            "        var client ",
            "= _httpClientFactory",
            ".CreateClient();\n",
            "        var response",
            " = await client",
            ".GetAsync(\"api/users\");\n",
            "        response.Ensure",
            "SuccessStatusCode();\n",
            "        return await response",
            ".Content.ReadFrom",
            "JsonAsync<IEnumerable<User>>();\n",
            "    }"
        };

        foreach (var chunk in chunks)
        {
            await Task.Delay(100, cancellationToken);
            yield return chunk;
        }

        _logger.LogInformation("流式补全完成");
    }

    /// <summary>
    /// 获取代码建议（多个选项）
    /// </summary>
    public async Task<List<CompletionChoice>> GetSuggestionsAsync(
        CompletionRequest request,
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("请求 {Count} 个代码建议", count);

        var suggestions = new List<CompletionChoice>();

        for (int i = 0; i < count; i++)
        {
            await Task.Delay(200, cancellationToken);

            suggestions.Add(new CompletionChoice
            {
                Text = GenerateMockSuggestion(request, i),
                Score = 0.9 - (i * 0.1),
                FinishReason = "length"
            });
        }

        return suggestions;
    }

    /// <summary>
    /// 生成模拟补全文本
    /// </summary>
    private string GenerateMockCompletion(CompletionRequest request)
    {
        var prompt = request.Prompt.ToLower();

        if (prompt.Contains("public async task"))
        {
            return "\n{\n    var client = _httpClientFactory.CreateClient();\n    var response = await client.GetAsync(\"api/data\");\n    response.EnsureSuccessStatusCode();\n    return await response.Content.ReadAsStringAsync();\n}";
        }

        if (prompt.Contains("public class"))
        {
            return "\n{\n    public int Id { get; set; }\n    public string Name { get; set; } = string.Empty;\n    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;\n}";
        }

        return "\n// Generated code by Copilot\nvar result = await ProcessAsync(data);\nreturn result;";
    }

    /// <summary>
    /// 生成模拟聊天响应
    /// </summary>
    private string GenerateMockChatResponse(ChatRequest request)
    {
        var prompt = request.Prompt.ToLower();

        if (prompt.Contains("如何") || prompt.Contains("how"))
        {
            return "我来帮你解答这个问题。首先，我们需要理解问题的核心，然后提供几种解决方案。建议您根据具体场景选择最适合的方案。";
        }

        if (prompt.Contains("代码") || prompt.Contains("code"))
        {
            return "关于这段代码，我建议进行以下优化：\n1. 使用异步方法提高性能\n2. 添加适当的错误处理\n3. 考虑使用缓存减少重复计算\n\n需要我提供具体的示例代码吗？";
        }

        if (prompt.Contains("bug") || prompt.Contains("错误"))
        {
            return "让我帮你分析这个问题。通常这类错误是由于：\n1. 数据类型不匹配\n2. 空引用未处理\n3. 异步方法调用不当\n\n建议检查相关代码并添加调试日志来定位问题。";
        }

        return "感谢你的问题。基于你提供的信息，我可以提供以下建议和解决方案。如果需要更详细的帮助，请提供更多上下文信息。";
    }

    /// <summary>
    /// 生成模拟代码解释
    /// </summary>
    private string GenerateMockExplanation(ExplainRequest request)
    {
        return $"""
        这段 {request.Language} 代码的主要功能如下：

        1. **输入处理**: 接收并验证输入参数
        2. **核心逻辑**: 执行主要的业务逻辑操作
        3. **结果返回**: 返回处理后的结果

        代码特点：
        - 使用了现代 {request.Language} 特性
        - 遵循最佳实践和编码规范
        - 包含基本的错误处理

        改进建议：
        - 可以添加更多的注释说明
        - 考虑提取复杂逻辑为独立方法
        - 添加单元测试确保代码质量
        """;
    }

    /// <summary>
    /// 生成模拟代码建议
    /// </summary>
    private string GenerateMockSuggestion(CompletionRequest request, int index)
    {
        return index switch
        {
            0 => "\n{\n    await using var stream = File.OpenRead(path);\n    return await JsonSerializer.DeserializeAsync<Data>(stream);\n}",
            1 => "\n{\n    var json = await File.ReadAllTextAsync(path);\n    return JsonSerializer.Deserialize<Data>(json);\n}",
            2 => "\n{\n    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };\n    return await JsonSerializer.DeserializeAsync<Data>(File.OpenRead(path), options);\n}",
            _ => "\n// Alternative implementation"
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _httpClient?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

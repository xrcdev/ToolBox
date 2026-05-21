namespace CopilotSDKDemo.Models;

/// <summary>
/// Copilot API 响应基类
/// </summary>
public class CopilotResponse
{
    /// <summary>
    /// 响应唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 使用的模型
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间戳
    /// </summary>
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// 响应状态
    /// </summary>
    public ResponseStatus Status { get; set; } = ResponseStatus.Success;

    /// <summary>
    /// 错误信息（如果请求失败）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 使用的 token 数量
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
/// 响应状态
/// </summary>
public enum ResponseStatus
{
    /// <summary>
    /// 请求成功
    /// </summary>
    Success,

    /// <summary>
    /// 请求失败
    /// </summary>
    Error,

    /// <summary>
    /// 请求超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 认证失败
    /// </summary>
    Unauthorized,

    /// <summary>
    /// 速率限制
    /// </summary>
    RateLimited
}

/// <summary>
/// Token 使用统计
/// </summary>
public class Usage
{
    /// <summary>
    /// 输入 token 数量
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 输出 token 数量
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 总 token 数量
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// 代码补全响应
/// </summary>
public class CompletionResponse : CopilotResponse
{
    /// <summary>
    /// 补全文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 多个补全选项
    /// </summary>
    public List<CompletionChoice> Choices { get; set; } = new();

    /// <summary>
    /// 补全类型（前缀/中缀）
    /// </summary>
    public CompletionType Type { get; set; } = CompletionType.Prefix;
}

/// <summary>
/// 补全类型
/// </summary>
public enum CompletionType
{
    /// <summary>
    /// 前缀补全（在光标后补全）
    /// </summary>
    Prefix,

    /// <summary>
    /// 中缀补全（在光标前后之间补全）
    /// </summary>
    Infix
}

/// <summary>
/// 补全选项
/// </summary>
public class CompletionChoice
{
    /// <summary>
    /// 补全文本
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// 置信度得分
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 结束原因
    /// </summary>
    public string? FinishReason { get; set; }
}

/// <summary>
/// 聊天响应
/// </summary>
public class ChatResponse : CopilotResponse
{
    /// <summary>
    /// 助手回复消息
    /// </summary>
    public required ChatMessage Message { get; set; }

    /// <summary>
    /// 对话ID
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// 建议的后续问题
    /// </summary>
    public List<string> SuggestedQuestions { get; set; } = new();

    /// <summary>
    /// 引用的代码片段
    /// </summary>
    public List<CodeReference> CodeReferences { get; set; } = new();
}

/// <summary>
/// 代码引用
/// </summary>
public class CodeReference
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 起始行号
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// 结束行号
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// 引用的代码片段
    /// </summary>
    public required string Snippet { get; set; }

    /// <summary>
    /// 引用原因
    /// </summary>
    public required string Reason { get; set; }
}

/// <summary>
/// 代码解释请求
/// </summary>
public class ExplainRequest
{
    /// <summary>
    /// 要解释的代码
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// 编程语言
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 解释详细程度
    /// </summary>
    public ExplanationDetail DetailLevel { get; set; } = ExplanationDetail.Medium;

    /// <summary>
    /// 是否包含代码建议
    /// </summary>
    public bool IncludeSuggestions { get; set; } = true;
}

/// <summary>
/// 解释详细程度
/// </summary>
public enum ExplanationDetail
{
    /// <summary>
    /// 简洁
    /// </summary>
    Brief,

    /// <summary>
    /// 中等
    /// </summary>
    Medium,

    /// <summary>
    /// 详细
    /// </summary>
    Detailed
}

/// <summary>
/// 代码解释响应
/// </summary>
public class ExplainResponse : CopilotResponse
{
    /// <summary>
    /// 代码解释
    /// </summary>
    public required string Explanation { get; set; }

    /// <summary>
    /// 代码功能概述
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 关键概念列表
    /// </summary>
    public List<string> KeyConcepts { get; set; } = new();

    /// <summary>
    /// 改进建议
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// 潜在问题
    /// </summary>
    public List<string> PotentialIssues { get; set; } = new();
}

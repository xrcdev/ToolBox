namespace CopilotSDKDemo.Models;

/// <summary>
/// Copilot API 请求基类
/// </summary>
public class CopilotRequest
{
    /// <summary>
    /// 模型标识符
    /// </summary>
    public string Model { get; set; } = "copilot";

    /// <summary>
    /// 输入提示或消息
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// 温度参数
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// 最大 tokens 数量
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Top P 参数
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// 频率惩罚
    /// </summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// 存在惩罚
    /// </summary>
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// 停止序列
    /// </summary>
    public string[]? Stop { get; set; }
}

/// <summary>
/// 代码补全请求
/// </summary>
public class CompletionRequest : CopilotRequest
{
    /// <summary>
    /// 代码补全的后缀（用于中缀补全）
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    /// 补全数量
    /// </summary>
    public int N { get; set; } = 1;

    /// <summary>
    /// 是否流式返回
    /// </summary>
    public bool Stream { get; set; } = false;

    /// <summary>
    /// 额外上下文信息（文件名、语言等）
    /// </summary>
    public CodeContext? Context { get; set; }
}

/// <summary>
/// 代码上下文信息
/// </summary>
public class CodeContext
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 编程语言
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 光标位置（行号，从0开始）
    /// </summary>
    public int CursorLine { get; set; }

    /// <summary>
    /// 光标位置（列号，从0开始）
    /// </summary>
    public int CursorColumn { get; set; }

    /// <summary>
    /// 相关代码片段
    /// </summary>
    public List<CodeSnippet> Snippets { get; set; } = new();
}

/// <summary>
/// 代码片段
/// </summary>
public class CodeSnippet
{
    /// <summary>
    /// 代码内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 编程语言
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 起始行号
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// 相似度得分
    /// </summary>
    public double? Score { get; set; }
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// 消息角色
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest : CopilotRequest
{
    /// <summary>
    /// 对话历史消息
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 对话ID（用于多轮对话）
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// 是否流式返回
    /// </summary>
    public bool Stream { get; set; } = false;
}

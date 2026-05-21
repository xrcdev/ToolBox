using System.ComponentModel;

namespace CopilotSDKDemo.Models;

/// <summary>
/// Copilot API 配置
/// </summary>
public class CopilotConfig
{
    /// <summary>
    /// GitHub Token (用于认证)
    /// </summary>
    [Description("GitHub Personal Access Token 或 Copilot Token")]
    public string GitHubToken { get; set; } = string.Empty;

    /// <summary>
    /// API 端点地址 (默认使用 GitHub Copilot API)
    /// </summary>
    [Description("Copilot API 端点地址")]
    public string ApiEndpoint { get; set; } = "https://api.githubcopilot.com";

    /// <summary>
    /// 请求超时时间(秒)
    /// </summary>
    [Description("API 请求超时时间")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    [Description("启用详细日志输出")]
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// 模型参数配置
    /// </summary>
    public ModelParameters ModelParameters { get; set; } = new();
}

/// <summary>
/// 模型参数配置
/// </summary>
public class ModelParameters
{
    /// <summary>
    /// 温度参数 (0.0 - 2.0), 控制随机性
    /// </summary>
    [Description("温度参数，越高输出越随机")]
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 最大生成的 token 数量
    /// </summary>
    [Description("最大生成的 token 数量")]
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Top P 采样参数 (0.0 - 1.0)
    /// </summary>
    [Description("核采样参数")]
    public double TopP { get; set; } = 0.9;

    /// <summary>
    /// 频率惩罚参数 (-2.0 - 2.0)
    /// </summary>
    [Description("频率惩罚，降低重复词汇出现频率")]
    public double FrequencyPenalty { get; set; } = 0;

    /// <summary>
    /// 存在惩罚参数 (-2.0 - 2.0)
    /// </summary>
    [Description("存在惩罚，鼓励讨论新话题")]
    public double PresencePenalty { get; set; } = 0;
}

# GitHub Copilot SDK - C# 使用演示

这是一个全面的 GitHub Copilot SDK C# 集成示例项目，展示了如何在实际应用中使用 Copilot 的各种 AI 辅助编程功能。

## 功能特性

### 1. 代码补全 (Completion)
- 基础代码补全
- 中缀补全 (在光标位置中间补全)
- 流式代码补全 (实时流式返回)
- 多建议补全 (获取多个候选选项)
- 上下文感知补全 (基于相关代码片段)
- 多编程语言支持

### 2. 对话/聊天 (Chat)
- 简单问答
- 多轮对话
- 自定义系统提示词
- 代码相关问题
- 对话历史管理
- 温度参数控制 (调整输出随机性)
- 建议的后续问题

### 3. 代码解释 (Explain)
- 代码功能解释
- 算法分析
- 设计模式识别
- 代码改进建议
- 潜在问题检测
- 不同详细程度 (简洁/中等/详细)
- 多编程语言支持

## 快速开始

### 环境要求

- .NET 8.0 SDK 或更高版本
- Windows/Linux/macOS

### 安装和运行

1. 克隆项目到本地
```bash
git clone <repository-url>
cd CopilotSDK
```

2. 设置 GitHub Token (可选)
```bash
# Windows
set GITHUB_TOKEN=your_token_here

# Linux/Mac
export GITHUB_TOKEN=your_token_here
```

3. 运行项目
```bash
dotnet run
```

## 项目结构

```
CopilotSDK/
├── Models/                          # 数据模型
│   ├── CopilotConfig.cs             # SDK 配置
│   ├── CopilotRequest.cs            # API 请求模型
│   └── CopilotResponse.cs           # API 响应模型
│
├── Services/                        # 服务层
│   └── CopilotService.cs            # 核心服务类
│
├── Examples/                        # 示例代码
│   ├── CompletionExamples.cs        # 代码补全示例
│   ├── ChatExamples.cs              # 对话示例
│   └── ExplainExamples.cs           # 代码解释示例
│
├── Program.cs                        # 主程序入口
└── CopilotSDKDemo.csproj            # 项目文件
```

## 使用示例

### 基础代码补全

```csharp
var service = new CopilotService(config, logger);

var request = new CompletionRequest
{
    Prompt = "public async Task<List<User>> GetUsersAsync()",
    Model = "copilot",
    Temperature = 0.2,
    MaxTokens = 500
};

var response = await service.GetCompletionAsync(request);
Console.WriteLine(response.Text);
```

### 流式代码补全

```csharp
await foreach (var chunk in service.StreamCompletionAsync(request))
{
    Console.Write(chunk);
}
```

### 多轮对话

```csharp
var request = new ChatRequest
{
    Prompt = "如何在 C# 中实现异步编程？",
    ConversationId = conversationId,
    SystemPrompt = "你是一个专业的 C# 编程助手",
    Temperature = 0.5
};

var response = await service.ChatAsync(request);
Console.WriteLine(response.Message.Content);
```

### 代码解释

```csharp
var request = new ExplainRequest
{
    Code = "public int CalculateFactorial(int n) { ... }",
    Language = "csharp",
    DetailLevel = ExplanationDetail.Detailed,
    IncludeSuggestions = true
};

var response = await service.ExplainCodeAsync(request);
Console.WriteLine(response.Explanation);
```

## 配置说明

### CopilotConfig 配置项

| 配置项 | 类型 | 说明 | 默认值 |
|--------|------|------|--------|
| GitHubToken | string | GitHub 认证 Token | 空 |
| ApiEndpoint | string | API 端点地址 | https://api.githubcopilot.com |
| TimeoutSeconds | int | 请求超时时间 | 30 |
| EnableVerboseLogging | bool | 是否启用详细日志 | false |
| Temperature | double | 温度参数 (0-2) | 0.7 |
| MaxTokens | int | 最大输出 tokens | 2048 |
| TopP | double | 核采样参数 | 0.9 |
| FrequencyPenalty | double | 频率惩罚 | 0 |
| PresencePenalty | double | 存在惩罚 | 0 |

### 参数说明

- **Temperature**: 控制输出的随机性。较低值输出更确定，较高值输出更随机
- **MaxTokens**: 限制输出的最大长度
- **TopP**: 核采样，控制考虑的词汇范围
- **FrequencyPenalty**: 降低重复词汇的频率
- **PresencePenalty**: 鼓励讨论新话题

## NuGet 包依赖

- Microsoft.Extensions.Logging
- Microsoft.Extensions.Logging.Console
- Microsoft.Extensions.DependencyInjection
- Newtonsoft.Json
- Azure.Core
- Azure.Identity

## 注意事项

1. 本项目使用模拟数据演示功能，实际使用时需要替换为真实的 GitHub Copilot API 调用
2. 需要有效的 GitHub Token 才能访问 Copilot API
3. API 调用可能产生费用，请注意使用量
4. 某些功能可能需要特定版本的 Copilot API

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request!

## 联系方式

如有问题或建议，欢迎通过 Issue 反馈。

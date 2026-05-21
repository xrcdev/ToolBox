using CopilotSDKDemo.Models;
using CopilotSDKDemo.Services;
using Microsoft.Extensions.Logging;

namespace CopilotSDKDemo.Examples;

/// <summary>
/// 对话/聊天示例
/// 演示如何使用 GitHub Copilot SDK 进行对话交互
/// </summary>
public class ChatExamples
{
    private readonly CopilotService _copilotService;
    private readonly ILogger<ChatExamples> _logger;

    public ChatExamples(CopilotService copilotService, ILogger<ChatExamples> logger)
    {
        _copilotService = copilotService ?? throw new ArgumentNullException(nameof(copilotService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 示例 1: 简单问答
    /// </summary>
    public async Task Example_SimpleQuestion()
    {
        Console.WriteLine("\n=== 示例 1: 简单问答 ===\n");

        var request = new ChatRequest
        {
            Prompt = "如何在 C# 中实现异步编程？请提供一个简单的示例。",
            Model = "copilot",
            Temperature = 0.5,
            MaxTokens = 500
        };

        try
        {
            var response = await _copilotService.ChatAsync(request);

            Console.WriteLine($"用户问题: {request.Prompt}\n");
            Console.WriteLine($"助手回复: {response.Message.Content}");
            Console.WriteLine($"\n对话 ID: {response.ConversationId}");
            Console.WriteLine($"Token 使用: {response.Usage?.TotalTokens}");
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "简单问答失败");
        }
    }

    /// <summary>
    /// 示例 2: 多轮对话
    /// </summary>
    public async Task Example_MultiTurnConversation()
    {
        Console.WriteLine("\n=== 示例 2: 多轮对话 ===\n");

        var conversationId = Guid.NewGuid().ToString();
        var systemPrompt = "你是一个专业的 C# 编程助手，擅长解答 .NET 相关的技术问题。";

        var questions = new[]
        {
            "什么是依赖注入？",
            "能给我一个具体的例子吗？",
            "使用依赖注入有什么好处？"
        };

        try
        {
            foreach (var (index, question) in questions.Select((q, i) => (i, q)))
            {
                Console.WriteLine($"\n--- 第 {index + 1} 轮对话 ---");
                Console.WriteLine($"用户: {question}");

                var request = new ChatRequest
                {
                    Prompt = question,
                    Model = "copilot",
                    ConversationId = conversationId,
                    SystemPrompt = systemPrompt,
                    Temperature = 0.5,
                    Messages = new List<ChatMessage>() // 实际应用中应包含历史消息
                };

                var response = await _copilotService.ChatAsync(request);

                Console.WriteLine($"助手: {response.Message.Content}\n");
                await Task.Delay(1000); // 模拟对话间隔
            }

            Console.WriteLine(new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "多轮对话失败");
        }
    }

    /// <summary>
    /// 示例 3: 带系统提示的对话
    /// </summary>
    public async Task Example_ChatWithSystemPrompt()
    {
        Console.WriteLine("\n=== 示例 3: 带系统提示的对话 ===\n");

        var systemPrompts = new Dictionary<string, string>
        {
            ["代码审查专家"] = "你是一位经验丰富的代码审查专家，专注于代码质量、性能和安全性。",
            ["架构师"] = "你是一位软件架构师，擅长系统设计和架构决策。",
            ["技术写作者"] = "你是一位技术文档写作者，擅长将复杂的技术概念解释清楚。"
        };

        var question = "请解释一下什么是微服务架构？";

        foreach (var (role, prompt) in systemPrompts)
        {
            try
            {
                Console.WriteLine($"\n角色: {role}");
                Console.WriteLine($"系统提示: {prompt}\n");
                Console.WriteLine($"问题: {question}");

                var request = new ChatRequest
                {
                    Prompt = question,
                    Model = "copilot",
                    SystemPrompt = prompt,
                    Temperature = 0.6,
                    MaxTokens = 400
                };

                var response = await _copilotService.ChatAsync(request);

                Console.WriteLine($"\n回复:\n{response.Message.Content}\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Role} 对话失败", role);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 4: 代码相关问题
    /// </summary>
    public async Task Example_CodeRelatedQuestions()
    {
        Console.WriteLine("\n=== 示例 4: 代码相关问题 ===\n");

        var codeQuestions = new[]
        {
            new { Question = "这段代码有什么问题？", CodeSnippet = "public void ProcessData(List<string> data)\n{\n    foreach (var item in data)\n    {\n        item.Trim();\n    }\n}" },
            new { Question = "如何优化这段 LINQ 查询？", CodeSnippet = "var result = list.Where(x => x.IsActive).Select(x => x.Name).ToList();" },
            new { Question = "这个异常处理正确吗？", CodeSnippet = "try\n{\n    File.ReadAllText(path);\n}\ncatch (Exception ex)\n{\n    Console.WriteLine(ex.Message);\n}" }
        };

        foreach (var item in codeQuestions)
        {
            try
            {
                Console.WriteLine($"问题: {item.Question}\n");
                Console.WriteLine("代码片段:");
                Console.WriteLine(item.CodeSnippet);
                Console.WriteLine();

                var request = new ChatRequest
                {
                    Prompt = $"{item.Question}\n\n```csharp\n{item.CodeSnippet}\n```",
                    Model = "copilot",
                    Temperature = 0.4,
                    MaxTokens = 600
                };

                var response = await _copilotService.ChatAsync(request);

                Console.WriteLine("回复:");
                Console.WriteLine(response.Message.Content);
                Console.WriteLine("\n" + new string('-', 60));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代码问题回答失败");
            }
        }
    }

    /// <summary>
    /// 示例 5: 获取建议的后续问题
    /// </summary>
    public async Task Example_SuggestedFollowUpQuestions()
    {
        Console.WriteLine("\n=== 示例 5: 建议的后续问题 ===\n");

        var request = new ChatRequest
        {
            Prompt = "请解释一下 .NET 中的异步编程模式和最佳实践。",
            Model = "copilot",
            Temperature = 0.5,
            MaxTokens = 400
        };

        try
        {
            var response = await _copilotService.ChatAsync(request);

            Console.WriteLine($"原始问题: {request.Prompt}\n");
            Console.WriteLine($"回复:\n{response.Message.Content}\n");

            if (response.SuggestedQuestions.Count > 0)
            {
                Console.WriteLine("建议的后续问题:");
                for (int i = 0; i < response.SuggestedQuestions.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {response.SuggestedQuestions[i]}");
                }
            }

            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后续问题建议失败");
        }
    }

    /// <summary>
    /// 示例 6: 交互式聊天
    /// </summary>
    public async Task Example_InteractiveChat()
    {
        Console.WriteLine("\n=== 示例 6: 交互式聊天 ===\n");
        Console.WriteLine("输入 'quit' 或 'exit' 退出\n");

        var conversationId = Guid.NewGuid().ToString();
        var messages = new List<ChatMessage>();

        while (true)
        {
            Console.Write("你: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // 添加用户消息
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = input,
                Timestamp = DateTime.UtcNow
            });

            try
            {
                var request = new ChatRequest
                {
                    Prompt = input,
                    Model = "copilot",
                    ConversationId = conversationId,
                    Temperature = 0.6,
                    Messages = messages.ToList(),
                    MaxTokens = 500
                };

                var response = await _copilotService.ChatAsync(request);

                Console.Write($"\n助手: {response.Message.Content}\n\n");

                // 添加助手回复
                messages.Add(response.Message);

                // 显示建议问题
                if (response.SuggestedQuestions.Count > 0)
                {
                    Console.WriteLine("建议问题:");
                    foreach (var question in response.SuggestedQuestions)
                    {
                        Console.WriteLine($"  - {question}");
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "聊天请求失败");
                Console.WriteLine("抱歉，出现错误。请重试。\n");
            }
        }

        Console.WriteLine("\n聊天结束。");
        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 7: 不同温度参数的效果
    /// </summary>
    public async Task Example_TemperatureComparison()
    {
        Console.WriteLine("\n=== 示例 7: 温度参数对比 ===\n");

        var question = "请用一句话解释什么是多态性。";
        var temperatures = new[] { 0.1, 0.5, 1.0, 1.5 };

        foreach (var temp in temperatures)
        {
            try
            {
                Console.WriteLine($"温度: {temp}");

                var request = new ChatRequest
                {
                    Prompt = question,
                    Model = "copilot",
                    Temperature = temp,
                    MaxTokens = 100
                };

                var response = await _copilotService.ChatAsync(request);
                Console.WriteLine($"回复: {response.Message.Content}\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "温度 {Temperature} 测试失败", temp);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 8: 对话历史管理
    /// </summary>
    public async Task Example_ConversationHistoryManagement()
    {
        Console.WriteLine("\n=== 示例 8: 对话历史管理 ===\n");

        var conversation = new Dictionary<string, List<ChatMessage>>();

        // 开始新对话
        var conversationId = Guid.NewGuid().ToString();
        conversation[conversationId] = new List<ChatMessage>();

        var userInputs = new[]
        {
            "你好，我想学习 C#",
            "从哪里开始比较好？",
            "有什么推荐的资源吗？"
        };

        foreach (var input in userInputs)
        {
            Console.WriteLine($"\n用户: {input}");

            // 添加用户消息到历史
            conversation[conversationId].Add(new ChatMessage
            {
                Role = "user",
                Content = input,
                Timestamp = DateTime.UtcNow
            });

            try
            {
                var request = new ChatRequest
                {
                    Prompt = input,
                    Model = "copilot",
                    ConversationId = conversationId,
                    Temperature = 0.5,
                    Messages = conversation[conversationId].ToList()
                };

                var response = await _copilotService.ChatAsync(request);

                Console.WriteLine($"助手: {response.Message.Content}");

                // 添加助手回复到历史
                conversation[conversationId].Add(response.Message);

                // 显示当前对话历史
                Console.WriteLine($"\n当前对话历史 ({conversation[conversationId].Count} 条消息)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "对话历史管理失败");
            }
        }

        Console.WriteLine("\n" + new string('-', 60));
    }

    /// <summary>
    /// 运行所有聊天示例
    /// </summary>
    public async Task RunAllExamples(bool includeInteractive = false)
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     GitHub Copilot SDK - 对话示例演示                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");

        await Example_SimpleQuestion();
        await Example_MultiTurnConversation();
        await Example_ChatWithSystemPrompt();
        await Example_CodeRelatedQuestions();
        await Example_SuggestedFollowUpQuestions();
        await Example_TemperatureComparison();
        await Example_ConversationHistoryManagement();

        if (includeInteractive)
        {
            await Example_InteractiveChat();
        }

        Console.WriteLine("\n✓ 所有对话示例执行完成!");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CopilotSDKDemo.Models;
using CopilotSDKDemo.Services;
using CopilotSDKDemo.Examples;

namespace CopilotSDKDemo;

/// <summary>
/// GitHub Copilot SDK 演示程序
/// 展示如何使用 GitHub Copilot SDK 进行各种 AI 辅助编程任务
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        DisplayWelcomeBanner();

        // 配置服务
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        try
        {
            // 获取配置
            var config = serviceProvider.GetRequiredService<CopilotConfig>();

            // 显示配置信息
            DisplayConfiguration(config);

            // 运行示例选择菜单
            await RunDemoMenu(serviceProvider);
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "程序执行出错");
            Console.WriteLine($"\n错误: {ex.Message}");
        }
        finally
        {
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        Console.WriteLine("\n程序结束，按任意键退出...");
        Console.ReadKey();
    }

    /// <summary>
    /// 显示欢迎横幅
    /// </summary>
    static void DisplayWelcomeBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║     GitHub Copilot SDK - C# 使用演示                         ║
║                                                               ║
║     功能全面的 Copilot SDK 集成示例                           ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
        ");
        Console.ResetColor();
    }

    /// <summary>
    /// 显示配置信息
    /// </summary>
    static void DisplayConfiguration(CopilotConfig config)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n【当前配置】");
        Console.WriteLine($"  API 端点: {config.ApiEndpoint}");
        Console.WriteLine($"  超时时间: {config.TimeoutSeconds} 秒");
        Console.WriteLine($"  详细日志: {(config.EnableVerboseLogging ? "启用" : "禁用")}");
        Console.WriteLine($"  温度参数: {config.ModelParameters.Temperature}");
        Console.WriteLine($"  最大 Tokens: {config.ModelParameters.MaxTokens}");
        Console.ResetColor();
    }

    /// <summary>
    /// 配置依赖注入服务
    /// </summary>
    static void ConfigureServices(IServiceCollection services)
    {
        // 配置日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 配置 Copilot 配置（实际使用时应从配置文件读取）
        var config = new CopilotConfig
        {
            GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty,
            ApiEndpoint = "https://api.githubcopilot.com",
            TimeoutSeconds = 30,
            EnableVerboseLogging = true,
            ModelParameters = new ModelParameters
            {
                Temperature = 0.7,
                MaxTokens = 2048,
                TopP = 0.9,
                FrequencyPenalty = 0,
                PresencePenalty = 0
            }
        };

        services.AddSingleton(config);
        services.AddSingleton<CopilotService>();

        // 注册示例
        services.AddTransient<CompletionExamples>();
        services.AddTransient<ChatExamples>();
        services.AddTransient<ExplainExamples>();
    }

    /// <summary>
    /// 运行演示菜单
    /// </summary>
    static async Task RunDemoMenu(ServiceProvider serviceProvider)
    {
        var completionExamples = serviceProvider.GetRequiredService<CompletionExamples>();
        var chatExamples = serviceProvider.GetRequiredService<ChatExamples>();
        var explainExamples = serviceProvider.GetRequiredService<ExplainExamples>();

        while (true)
        {
            Console.Clear();
            DisplayWelcomeBanner();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n【主菜单】请选择要运行的示例:\n");
            Console.ResetColor();

            Console.WriteLine("  1. 代码补全示例 (Completion Examples)");
            Console.WriteLine("  2. 对话/聊天示例 (Chat Examples)");
            Console.WriteLine("  3. 代码解释示例 (Explain Examples)");
            Console.WriteLine("  4. 运行所有示例 (Run All Examples)");
            Console.WriteLine("  5. 快速开始指南 (Quick Start Guide)");
            Console.WriteLine("  6. 关于此项目 (About)");
            Console.WriteLine("  0. 退出程序 (Exit)");

            Console.Write("\n请输入选项 (0-6): ");
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await RunCompletionSubMenu(completionExamples);
                    break;

                case "2":
                    await RunChatSubMenu(chatExamples);
                    break;

                case "3":
                    await RunExplainSubMenu(explainExamples);
                    break;

                case "4":
                    await RunAllExamples(completionExamples, chatExamples, explainExamples);
                    break;

                case "5":
                    DisplayQuickStartGuide();
                    break;

                case "6":
                    DisplayAbout();
                    break;

                case "0":
                    Console.WriteLine("\n感谢使用！再见！");
                    return;

                default:
                    Console.WriteLine("\n无效选项，请重试...");
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    /// <summary>
    /// 代码补全子菜单
    /// </summary>
    static async Task RunCompletionSubMenu(CompletionExamples examples)
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n【代码补全示例】\n");
            Console.ResetColor();

            Console.WriteLine("  1. 基础代码补全");
            Console.WriteLine("  2. 中缀补全");
            Console.WriteLine("  3. 流式代码补全");
            Console.WriteLine("  4. 多个补全建议");
            Console.WriteLine("  5. 带上下文的补全");
            Console.WriteLine("  6. 多语言补全");
            Console.WriteLine("  7. 运行所有补全示例");
            Console.WriteLine("  0. 返回主菜单");

            Console.Write("\n请选择 (0-7): ");
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await examples.Example_BasicCompletion();
                    PromptContinue();
                    break;

                case "2":
                    await examples.Example_InfixCompletion();
                    PromptContinue();
                    break;

                case "3":
                    await examples.Example_StreamCompletion();
                    PromptContinue();
                    break;

                case "4":
                    await examples.Example_MultipleSuggestions();
                    PromptContinue();
                    break;

                case "5":
                    await examples.Example_CompletionWithContext();
                    PromptContinue();
                    break;

                case "6":
                    await examples.Example_MultipleLanguages();
                    PromptContinue();
                    break;

                case "7":
                    await examples.RunAllExamples();
                    PromptContinue();
                    break;

                case "0":
                    return;

                default:
                    Console.WriteLine("\n无效选项...");
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    /// <summary>
    /// 对话示例子菜单
    /// </summary>
    static async Task RunChatSubMenu(ChatExamples examples)
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n【对话示例】\n");
            Console.ResetColor();

            Console.WriteLine("  1. 简单问答");
            Console.WriteLine("  2. 多轮对话");
            Console.WriteLine("  3. 带系统提示的对话");
            Console.WriteLine("  4. 代码相关问题");
            Console.WriteLine("  5. 后续问题建议");
            Console.WriteLine("  6. 温度参数对比");
            Console.WriteLine("  7. 对话历史管理");
            Console.WriteLine("  8. 交互式聊天");
            Console.WriteLine("  9. 运行所有对话示例");
            Console.WriteLine("  0. 返回主菜单");

            Console.Write("\n请选择 (0-9): ");
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await examples.Example_SimpleQuestion();
                    PromptContinue();
                    break;

                case "2":
                    await examples.Example_MultiTurnConversation();
                    PromptContinue();
                    break;

                case "3":
                    await examples.Example_ChatWithSystemPrompt();
                    PromptContinue();
                    break;

                case "4":
                    await examples.Example_CodeRelatedQuestions();
                    PromptContinue();
                    break;

                case "5":
                    await examples.Example_SuggestedFollowUpQuestions();
                    PromptContinue();
                    break;

                case "6":
                    await examples.Example_TemperatureComparison();
                    PromptContinue();
                    break;

                case "7":
                    await examples.Example_ConversationHistoryManagement();
                    PromptContinue();
                    break;

                case "8":
                    await examples.Example_InteractiveChat();
                    PromptContinue();
                    break;

                case "9":
                    await examples.RunAllExamples(includeInteractive: false);
                    PromptContinue();
                    break;

                case "0":
                    return;

                default:
                    Console.WriteLine("\n无效选项...");
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    /// <summary>
    /// 代码解释子菜单
    /// </summary>
    static async Task RunExplainSubMenu(ExplainExamples examples)
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n【代码解释示例】\n");
            Console.ResetColor();

            Console.WriteLine("  1. 简单代码解释");
            Console.WriteLine("  2. 不同详细程度的解释");
            Console.WriteLine("  3. 多语言代码解释");
            Console.WriteLine("  4. 代码改进建议");
            Console.WriteLine("  5. 复杂算法解释");
            Console.WriteLine("  6. 设计模式识别");
            Console.WriteLine("  7. 异步代码解释");
            Console.WriteLine("  8. 带文件上下文的解释");
            Console.WriteLine("  9. 运行所有解释示例");
            Console.WriteLine("  0. 返回主菜单");

            Console.Write("\n请选择 (0-9): ");
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await examples.Example_BasicCodeExplanation();
                    PromptContinue();
                    break;

                case "2":
                    await examples.Example_DifferentDetailLevels();
                    PromptContinue();
                    break;

                case "3":
                    await examples.Example_MultipleLanguageExplanation();
                    PromptContinue();
                    break;

                case "4":
                    await examples.Example_CodeImprovementSuggestions();
                    PromptContinue();
                    break;

                case "5":
                    await examples.Example_ComplexAlgorithmExplanation();
                    PromptContinue();
                    break;

                case "6":
                    await examples.Example_DesignPatternRecognition();
                    PromptContinue();
                    break;

                case "7":
                    await examples.Example_AsyncCodeExplanation();
                    PromptContinue();
                    break;

                case "8":
                    await examples.Example_CodeExplanationWithFileContext();
                    PromptContinue();
                    break;

                case "9":
                    await examples.RunAllExamples();
                    PromptContinue();
                    break;

                case "0":
                    return;

                default:
                    Console.WriteLine("\n无效选项...");
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    static async Task RunAllExamples(
        CompletionExamples completionExamples,
        ChatExamples chatExamples,
        ExplainExamples explainExamples)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n开始运行所有示例...\n");
        Console.ResetColor();

        try
        {
            await completionExamples.RunAllExamples();
            await Task.Delay(2000);

            await chatExamples.RunAllExamples(includeInteractive: false);
            await Task.Delay(2000);

            await explainExamples.RunAllExamples();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✓ 所有示例运行完成!");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ 运行出错: {ex.Message}");
            Console.ResetColor();
        }

        PromptContinue();
    }

    /// <summary>
    /// 显示快速开始指南
    /// </summary>
    static void DisplayQuickStartGuide()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                    快速开始指南                               ║
╚═══════════════════════════════════════════════════════════════╝

1. 设置 GitHub Token

   在运行程序前，需要设置 GITHUB_TOKEN 环境变量:

   Windows:
     set GITHUB_TOKEN=your_token_here

   Linux/Mac:
     export GITHUB_TOKEN=your_token_here

   或者使用 GitHub Personal Access Token


2. 基本使用

   // 创建配置
   var config = new CopilotConfig
   {
       GitHubToken = ""your_token"",
       ApiEndpoint = ""https://api.githubcopilot.com""
   };

   // 创建服务
   var service = new CopilotService(config, logger);

   // 代码补全
   var completion = await service.GetCompletionAsync(new CompletionRequest
   {
       Prompt = ""public async Task""
   });

   // 聊天
   var chat = await service.ChatAsync(new ChatRequest
   {
       Prompt = ""如何在 C# 中实现异步编程？""
   });

   // 代码解释
   var explanation = await service.ExplainCodeAsync(new ExplainRequest
   {
       Code = ""your_code_here"",
       Language = ""csharp""
   });


3. 主要功能

   • 代码补全: GetCompletionAsync()
   • 流式补全: StreamCompletionAsync()
   • 多建议: GetSuggestionsAsync()
   • 对话: ChatAsync()
   • 代码解释: ExplainCodeAsync()


4. 配置选项

   • Temperature: 控制输出随机性 (0.0 - 2.0)
   • MaxTokens: 最大输出长度
   • TopP: 核采样参数
   • FrequencyPenalty: 频率惩罚
   • PresencePenalty: 存在惩罚


更多信息请查看项目文档和示例代码。
        ");
        Console.ResetColor();
        PromptContinue();
    }

    /// <summary>
    /// 显示关于信息
    /// </summary>
    static void DisplayAbout()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                    关于此项目                                 ║
╚═══════════════════════════════════════════════════════════════╝

GitHub Copilot SDK - C# 使用演示

这是一个全面的 GitHub Copilot SDK C# 集成示例项目，展示了如何
在实际应用中使用 Copilot 的各种 AI 辅助编程功能。

主要功能:

  1. 代码补全
     - 基础补全
     - 中缀补全
     - 流式补全
     - 多建议补全
     - 上下文感知补全
     - 多语言支持

  2. 对话/聊天
     - 简单问答
     - 多轮对话
     - 系统提示
     - 代码问答
     - 对话历史管理
     - 温度参数控制

  3. 代码解释
     - 代码分析
     - 算法解释
     - 模式识别
     - 改进建议
     - 多语言支持
     - 不同详细程度

技术特点:

  • 依赖注入 (Microsoft.Extensions.DependencyInjection)
  • 结构化日志 (Microsoft.Extensions.Logging)
  • 异步编程模式
  • 清晰的代码组织结构
  • 完整的示例和文档

版本: 1.0.0
作者: Copilot SDK Demo Team
许可: MIT License

如有问题或建议，欢迎反馈!
        ");
        Console.ResetColor();
        PromptContinue();
    }

    /// <summary>
    /// 提示用户继续
    /// </summary>
    static void PromptContinue()
    {
        Console.WriteLine("\n按任意键继续...");
        Console.ReadKey();
    }
}

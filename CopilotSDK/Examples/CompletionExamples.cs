using System.Text;
using CopilotSDKDemo.Models;
using CopilotSDKDemo.Services;
using Microsoft.Extensions.Logging;

namespace CopilotSDKDemo.Examples;

/// <summary>
/// 代码补全示例
/// 演示如何使用 GitHub Copilot SDK 进行代码补全
/// </summary>
public class CompletionExamples
{
    private readonly CopilotService _copilotService;
    private readonly ILogger<CompletionExamples> _logger;

    public CompletionExamples(CopilotService copilotService, ILogger<CompletionExamples> logger)
    {
        _copilotService = copilotService ?? throw new ArgumentNullException(nameof(copilotService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 示例 1: 基础代码补全
    /// </summary>
    public async Task Example_BasicCompletion()
    {
        Console.WriteLine("\n=== 示例 1: 基础代码补全 ===\n");

        var request = new CompletionRequest
        {
            Prompt = "public async Task<List<User>> GetUsersAsync()",
            Model = "copilot",
            Temperature = 0.2,
            MaxTokens = 500,
            Context = new CodeContext
            {
                Language = "csharp",
                FilePath = "Services/UserService.cs",
                CursorLine = 10,
                CursorColumn = 50
            }
        };

        try
        {
            var response = await _copilotService.GetCompletionAsync(request);

            Console.WriteLine($"模型: {response.Model}");
            Console.WriteLine($"补全类型: {response.Type}");
            Console.WriteLine($"Token 使用: {response.Usage?.TotalTokens}");
            Console.WriteLine("\n补全代码:");
            Console.WriteLine(request.Prompt);
            Console.WriteLine(response.Text);
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代码补全失败");
        }
    }

    /// <summary>
    /// 示例 2: 中缀补全（在光标位置中间补全）
    /// </summary>
    public async Task Example_InfixCompletion()
    {
        Console.WriteLine("\n=== 示例 2: 中缀补全 ===\n");

        var request = new CompletionRequest
        {
            Prompt = "public async Task<IActionResult> UpdateUser(",
            Suffix = ")\n{\n    // TODO: 实现更新逻辑\n    return Ok();\n}",
            Model = "copilot",
            Temperature = 0.3
        };

        try
        {
            var response = await _copilotService.GetCompletionAsync(request);

            Console.WriteLine("前缀:");
            Console.WriteLine(request.Prompt[..Math.Min(50, request.Prompt.Length)]);
            Console.WriteLine("\n补全内容:");
            Console.WriteLine(response.Text);
            Console.WriteLine("\n后缀:");
            Console.WriteLine(request.Suffix);
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "中缀补全失败");
        }
    }

    /// <summary>
    /// 示例 3: 流式代码补全
    /// </summary>
    public async Task Example_StreamCompletion()
    {
        Console.WriteLine("\n=== 示例 3: 流式代码补全 ===\n");

        var request = new CompletionRequest
        {
            Prompt = "public class ConfigurationService\n{\n    private readonly IConfiguration _config;\n\n    public ConfigurationService(IConfiguration config)\n    {\n        _config = config;\n    }\n\n    public string GetValue(",
            Model = "copilot",
            Temperature = 0.2
        };

        try
        {
            Console.WriteLine("开始接收流式补全...\n");
            Console.WriteLine("提示: " + request.Prompt);

            var fullCompletion = new StringBuilder();

            await foreach (var chunk in _copilotService.StreamCompletionAsync(request))
            {
                Console.Write(chunk);
                fullCompletion.Append(chunk);
                // 模拟打字机效果
                await Task.Delay(50);
            }

            Console.WriteLine("\n\n流式补全完成!");
            Console.WriteLine("总长度: " + fullCompletion.Length + " 字符");
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式补全失败");
        }
    }

    /// <summary>
    /// 示例 4: 获取多个补全建议
    /// </summary>
    public async Task Example_MultipleSuggestions()
    {
        Console.WriteLine("\n=== 示例 4: 多个补全建议 ===\n");

        var request = new CompletionRequest
        {
            Prompt = "var users = await ",
            Model = "copilot",
            Temperature = 0.7,
            Context = new CodeContext
            {
                Language = "csharp",
                Snippets = new List<CodeSnippet>
                {
                    new()
                    {
                        Content = "public class User { public int Id { get; set; } public string Name { get; set; } }",
                        Language = "csharp"
                    }
                }
            }
        };

        try
        {
            var suggestions = await _copilotService.GetSuggestionsAsync(request, count: 3);

            Console.WriteLine($"收到 {suggestions.Count} 个建议:\n");

            for (int i = 0; i < suggestions.Count; i++)
            {
                var suggestion = suggestions[i];
                Console.WriteLine($"建议 {i + 1} (置信度: {suggestion.Score:P2}):");
                Console.WriteLine(request.Prompt + suggestion.Text);
                Console.WriteLine();
            }

            Console.WriteLine(new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取多个建议失败");
        }
    }

    /// <summary>
    /// 示例 5: 带上下文的代码补全
    /// </summary>
    public async Task Example_CompletionWithContext()
    {
        Console.WriteLine("\n=== 示例 5: 带上下文的代码补全 ===\n");

        // 创建相关代码片段
        var relatedCode = new CodeContext
        {
            FilePath = "Models/User.cs",
            Language = "csharp",
            CursorLine = 15,
            CursorColumn = 20,
            Snippets = new List<CodeSnippet>
            {
                new()
                {
                    FilePath = "Models/User.cs",
                    Language = "csharp",
                    StartLine = 1,
                    Content = """
                    public class User
                    {
                        public int Id { get; set; }
                        public string Email { get; set; }
                        public string? PasswordHash { get; set; }
                        public DateTime CreatedAt { get; set; }
                    }
                    """,
                    Score = 0.95
                },
                new()
                {
                    FilePath = "Data/AppDbContext.cs",
                    Language = "csharp",
                    StartLine = 10,
                    Content = "public DbSet<User> Users { get; set; }",
                    Score = 0.88
                }
            }
        };

        var request = new CompletionRequest
        {
            Prompt = """
            public async Task<bool> ValidateUserAsync(string email, string password)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    return false;
                }

                // 验证密码
            """,
            Model = "copilot",
            Temperature = 0.2,
            Context = relatedCode
        };

        try
        {
            var response = await _copilotService.GetCompletionAsync(request);

            Console.WriteLine("使用上下文信息:");
            foreach (var snippet in relatedCode.Snippets)
            {
                Console.WriteLine($"  - {snippet.FilePath} (相似度: {snippet.Score:P2})");
            }

            Console.WriteLine("\n补全结果:");
            Console.WriteLine(response.Text);
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "带上下文的补全失败");
        }
    }

    /// <summary>
    /// 示例 6: 不同编程语言的补全
    /// </summary>
    public async Task Example_MultipleLanguages()
    {
        Console.WriteLine("\n=== 示例 6: 多语言代码补全 ===\n");

        var languages = new Dictionary<string, string>
        {
            ["javascript"] = "async function fetchUsers() {",
            ["python"] = "async def fetch_users():",
            ["typescript"] = "interface User { id: number; name: string; }\n\nconst getAllUsers = async (): Promise",
            ["java"] = "public List<User> getAllUsers() {"
        };

        foreach (var (language, prompt) in languages)
        {
            try
            {
                Console.WriteLine($"语言: {language.ToUpper()}");
                Console.WriteLine($"提示: {prompt}");

                var request = new CompletionRequest
                {
                    Prompt = prompt,
                    Model = "copilot",
                    Temperature = 0.3,
                    Context = new CodeContext { Language = language }
                };

                var response = await _copilotService.GetCompletionAsync(request);

                Console.WriteLine($"补全: {response.Text[..Math.Min(100, response.Text.Length)]}...\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Language} 补全失败", language);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 运行所有代码补全示例
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     GitHub Copilot SDK - 代码补全示例演示             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");

        await Example_BasicCompletion();
        await Example_InfixCompletion();
        await Example_StreamCompletion();
        await Example_MultipleSuggestions();
        await Example_CompletionWithContext();
        await Example_MultipleLanguages();

        Console.WriteLine("\n✓ 所有代码补全示例执行完成!");
    }
}

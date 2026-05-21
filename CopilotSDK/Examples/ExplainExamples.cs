using CopilotSDKDemo.Models;
using CopilotSDKDemo.Services;
using Microsoft.Extensions.Logging;

namespace CopilotSDKDemo.Examples;

/// <summary>
/// 代码解释示例
/// 演示如何使用 GitHub Copilot SDK 进行代码解释和分析
/// </summary>
public class ExplainExamples
{
    private readonly CopilotService _copilotService;
    private readonly ILogger<ExplainExamples> _logger;

    public ExplainExamples(CopilotService copilotService, ILogger<ExplainExamples> logger)
    {
        _copilotService = copilotService ?? throw new ArgumentNullException(nameof(copilotService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 示例 1: 简单代码解释
    /// </summary>
    public async Task Example_BasicCodeExplanation()
    {
        Console.WriteLine("\n=== 示例 1: 简单代码解释 ===\n");

        var code = """
            public int CalculateFactorial(int n)
            {
                if (n <= 1)
                    return 1;

                int result = 1;
                for (int i = 2; i <= n; i++)
                {
                    result *= i;
                }
                return result;
            }
            """;

        var request = new ExplainRequest
        {
            Code = code,
            Language = "csharp",
            DetailLevel = ExplanationDetail.Medium,
            IncludeSuggestions = true
        };

        try
        {
            var response = await _copilotService.ExplainCodeAsync(request);

            Console.WriteLine("代码片段:");
            Console.WriteLine(code);
            Console.WriteLine($"\n解释:\n{response.Explanation}");
            Console.WriteLine($"\n摘要: {response.Summary}");
            Console.WriteLine($"\n关键概念: {string.Join(", ", response.KeyConcepts)}");
            Console.WriteLine($"\nToken 使用: {response.Usage?.TotalTokens}");
            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代码解释失败");
        }
    }

    /// <summary>
    /// 示例 2: 不同详细程度的解释
    /// </summary>
    public async Task Example_DifferentDetailLevels()
    {
        Console.WriteLine("\n=== 示例 2: 不同详细程度的解释 ===\n");

        var code = """
            public async Task<IEnumerable<User>> GetActiveUsersAsync()
            {
                var cacheKey = "active_users";

                var cached = await _cache.GetAsync<List<User>>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var users = await _context.Users
                    .Where(u => u.IsActive && !u.IsDeleted)
                    .ToListAsync();

                await _cache.SetAsync(cacheKey, users, TimeSpan.FromMinutes(5));

                return users;
            }
            """;

        var detailLevels = new[]
        {
            ExplanationDetail.Brief,
            ExplanationDetail.Medium,
            ExplanationDetail.Detailed
        };

        foreach (var level in detailLevels)
        {
            try
            {
                Console.WriteLine($"详细程度: {level}\n");

                var request = new ExplainRequest
                {
                    Code = code,
                    Language = "csharp",
                    DetailLevel = level,
                    IncludeSuggestions = true
                };

                var response = await _copilotService.ExplainCodeAsync(request);

                Console.WriteLine(response.Explanation);
                Console.WriteLine(new string('-', 40) + "\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Level} 解释失败", level);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 3: 多语言代码解释
    /// </summary>
    public async Task Example_MultipleLanguageExplanation()
    {
        Console.WriteLine("\n=== 示例 3: 多语言代码解释 ===\n");

        var codeExamples = new Dictionary<string, string>
        {
            ["javascript"] = """
                // JavaScript 示例
                const debounce = (func, wait) => {
                    let timeout;
                    return function executedFunction(...args) {
                        const later = () => {
                            clearTimeout(timeout);
                            func(...args);
                        };
                        clearTimeout(timeout);
                        timeout = setTimeout(later, wait);
                    };
                };
                """,
            ["python"] = """
                # Python 示例
                from functools import wraps
                import time

                def timing_decorator(func):
                    @wraps(func)
                    def wrapper(*args, **kwargs):
                        start = time.time()
                        result = func(*args, **kwargs)
                        end = time.time()
                        print(f"{func.__name__} took {end - start:.4f} seconds")
                        return result
                    return wrapper
                """,
            ["typescript"] = """
                // TypeScript 示例
                interface Repository<T> {
                    findById(id: string): Promise<T | null>;
                    findAll(): Promise<T[]>;
                    create(entity: Omit<T, 'id'>): Promise<T>;
                    update(id: string, entity: Partial<T>): Promise<T>;
                    delete(id: string): Promise<boolean>;
                }
                """
        };

        foreach (var (language, code) in codeExamples)
        {
            try
            {
                Console.WriteLine($"语言: {language.ToUpper()}\n");
                Console.WriteLine("代码:");
                Console.WriteLine(code);
                Console.WriteLine();

                var request = new ExplainRequest
                {
                    Code = code,
                    Language = language,
                    DetailLevel = ExplanationDetail.Medium,
                    IncludeSuggestions = false
                };

                var response = await _copilotService.ExplainCodeAsync(request);

                Console.WriteLine("解释:");
                Console.WriteLine(response.Explanation);
                Console.WriteLine(new string('-', 40) + "\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Language} 代码解释失败", language);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 4: 获取代码改进建议
    /// </summary>
    public async Task Example_CodeImprovementSuggestions()
    {
        Console.WriteLine("\n=== 示例 4: 代码改进建议 ===\n");

        var code = """
            public List<string> LoadFile(string path)
            {
                var lines = new List<string>();
                var file = File.OpenText(path);
                string line;

                while ((line = file.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                file.Close();
                return lines;
            }
            """;

        var request = new ExplainRequest
        {
            Code = code,
            Language = "csharp",
            DetailLevel = ExplanationDetail.Detailed,
            IncludeSuggestions = true
        };

        try
        {
            var response = await _copilotService.ExplainCodeAsync(request);

            Console.WriteLine("原始代码:");
            Console.WriteLine(code);
            Console.WriteLine($"\n解释:\n{response.Explanation}");

            if (response.Suggestions.Count > 0)
            {
                Console.WriteLine("\n改进建议:");
                for (int i = 0; i < response.Suggestions.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {response.Suggestions[i]}");
                }
            }

            if (response.PotentialIssues.Count > 0)
            {
                Console.WriteLine("\n潜在问题:");
                for (int i = 0; i < response.PotentialIssues.Count; i++)
                {
                    Console.WriteLine($"  • {response.PotentialIssues[i]}");
                }
            }

            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代码改进建议失败");
        }
    }

    /// <summary>
    /// 示例 5: 复杂算法解释
    /// </summary>
    public async Task Example_ComplexAlgorithmExplanation()
    {
        Console.WriteLine("\n=== 示例 5: 复杂算法解释 ===\n");

        var code = """
            public class LruCache<K, V>
            {
                private readonly int _capacity;
                private readonly Dictionary<K, LinkedListNode<CacheItem>> _cache;
                private readonly LinkedList<CacheItem> _lruList;

                public LruCache(int capacity)
                {
                    _capacity = capacity;
                    _cache = new Dictionary<K, LinkedListNode<CacheItem>>();
                    _lruList = new LinkedList<CacheItem>();
                }

                public V Get(K key)
                {
                    if (!_cache.TryGetValue(key, out var node))
                    {
                        return default(V);
                    }

                    _lruList.Remove(node);
                    _lruList.AddFirst(node);

                    return node.Value.Value;
                }

                public void Put(K key, V value)
                {
                    if (_cache.TryGetValue(key, out var node))
                    {
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                        node.Value.Value = value;
                        return;
                    }

                    if (_cache.Count >= _capacity)
                    {
                        var last = _lruList.Last;
                        _cache.Remove(last.Value.Key);
                        _lruList.RemoveLast();
                    }

                    var item = new CacheItem { Key = key, Value = value };
                    var newNode = _lruList.AddFirst(item);
                    _cache[key] = newNode;
                }

                private class CacheItem
                {
                    public K Key { get; set; }
                    public V Value { get; set; }
                }
            }
            """;

        var request = new ExplainRequest
        {
            Code = code,
            Language = "csharp",
            DetailLevel = ExplanationDetail.Detailed,
            IncludeSuggestions = true
        };

        try
        {
            var response = await _copilotService.ExplainCodeAsync(request);

            Console.WriteLine("LRU 缓存实现:\n");
            Console.WriteLine(response.Explanation);

            Console.WriteLine("\n关键概念:");
            foreach (var concept in response.KeyConcepts)
            {
                Console.WriteLine($"  • {concept}");
            }

            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复杂算法解释失败");
        }
    }

    /// <summary>
    /// 示例 6: 代码模式识别
    /// </summary>
    public async Task Example_DesignPatternRecognition()
    {
        Console.WriteLine("\n=== 示例 6: 设计模式识别 ===\n");

        var patterns = new Dictionary<string, string>
        {
            ["单例模式"] = """
                public class Singleton
                {
                    private static Singleton _instance;
                    private static readonly object _lock = new object();

                    private Singleton() { }

                    public static Singleton Instance
                    {
                        get
                        {
                            if (_instance == null)
                            {
                                lock (_lock)
                                {
                                    if (_instance == null)
                                    {
                                        _instance = new Singleton();
                                    }
                                }
                            }
                            return _instance;
                        }
                    }
                }
                """,
            ["观察者模式"] = """
                public interface IObserver<T>
                {
                    void Update(T data);
                }

                public class Subject<T>
                {
                    private readonly List<IObserver<T>> _observers = new List<IObserver<T>>();

                    public void Subscribe(IObserver<T> observer)
                    {
                        _observers.Add(observer);
                    }

                    public void Notify(T data)
                    {
                        foreach (var observer in _observers)
                        {
                            observer.Update(data);
                        }
                    }
                }
                """,
            ["工厂模式"] = """
                public interface IProduct
                {
                    string Operation();
                }

                public class ConcreteProductA : IProduct
                {
                    public string Operation() => "Result of ProductA";
                }

                public class Factory
                {
                    public IProduct CreateProduct(string type)
                    {
                        return type switch
                        {
                            "A" => new ConcreteProductA(),
                            _ => throw new ArgumentException("Invalid type")
                        };
                    }
                }
                """
        };

        foreach (var (patternName, code) in patterns)
        {
            try
            {
                Console.WriteLine($"\n识别设计模式: {patternName}\n");

                var request = new ExplainRequest
                {
                    Code = code,
                    Language = "csharp",
                    DetailLevel = ExplanationDetail.Medium,
                    IncludeSuggestions = false
                };

                var response = await _copilotService.ExplainCodeAsync(request);

                Console.WriteLine(response.Explanation);

                if (response.Summary != null)
                {
                    Console.WriteLine($"\n模式概述: {response.Summary}");
                }

                Console.WriteLine(new string('-', 40));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Pattern} 识别失败", patternName);
            }
        }

        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// 示例 7: 异步代码解释
    /// </summary>
    public async Task Example_AsyncCodeExplanation()
    {
        Console.WriteLine("\n=== 示例 7: 异步代码解释 ===\n");

        var code = """
            public async Task ProcessBatchAsync(IEnumerable<int> ids)
            {
                var tasks = ids.Select(async id =>
                {
                    try
                    {
                        var data = await _apiService.GetDataAsync(id);
                        await _repository.SaveAsync(data);
                        return (id, true, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process {Id}", id);
                        return (id, false, ex.Message);
                    }
                });

                var results = await Task.WhenAll(tasks);

                var successful = results.Count(r => r.Item2);
                var failed = results.Count(r => !r.Item2);

                _logger.LogInformation("Processed {Total}: {Success} success, {Failed} failed",
                    results.Length, successful, failed);
            }
            """;

        var request = new ExplainRequest
        {
            Code = code,
            Language = "csharp",
            DetailLevel = ExplanationDetail.Detailed,
            IncludeSuggestions = true
        };

        try
        {
            var response = await _copilotService.ExplainCodeAsync(request);

            Console.WriteLine("异步批处理代码:\n");
            Console.WriteLine(response.Explanation);

            if (response.KeyConcepts.Count > 0)
            {
                Console.WriteLine("\n关键异步概念:");
                foreach (var concept in response.KeyConcepts)
                {
                    Console.WriteLine($"  • {concept}");
                }
            }

            Console.WriteLine(new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "异步代码解释失败");
        }
    }

    /// <summary>
    /// 示例 8: 带文件路径的代码解释
    /// </summary>
    public async Task Example_CodeExplanationWithFileContext()
    {
        Console.WriteLine("\n=== 示例 8: 带文件上下文的代码解释 ===\n");

        var code = """
            [HttpGet("{id}")]
            public async Task<ActionResult<User>> GetUser(int id)
            {
                var user = await _userService.GetUserByIdAsync(id);

                if (user == null)
                {
                    return NotFound();
                }

                return Ok(user);
            }
            """;

        var request = new ExplainRequest
        {
            Code = code,
            Language = "csharp",
            FilePath = "Controllers/UsersController.cs",
            DetailLevel = ExplanationDetail.Medium,
            IncludeSuggestions = true
        };

        try
        {
            var response = await _copilotService.ExplainCodeAsync(request);

            Console.WriteLine($"文件: {request.FilePath}\n");
            Console.WriteLine("代码:");
            Console.WriteLine(code);
            Console.WriteLine($"\n解释:\n{response.Explanation}");

            if (response.Suggestions.Count > 0)
            {
                Console.WriteLine("\n建议:");
                foreach (var suggestion in response.Suggestions)
                {
                    Console.WriteLine($"  • {suggestion}");
                }
            }

            Console.WriteLine("\n" + new string('-', 60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "带上下文的代码解释失败");
        }
    }

    /// <summary>
    /// 运行所有代码解释示例
    /// </summary>
    public async Task RunAllExamples()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     GitHub Copilot SDK - 代码解释示例演示             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");

        await Example_BasicCodeExplanation();
        await Example_DifferentDetailLevels();
        await Example_MultipleLanguageExplanation();
        await Example_CodeImprovementSuggestions();
        await Example_ComplexAlgorithmExplanation();
        await Example_DesignPatternRecognition();
        await Example_AsyncCodeExplanation();
        await Example_CodeExplanationWithFileContext();

        Console.WriteLine("\n✓ 所有代码解释示例执行完成!");
    }
}

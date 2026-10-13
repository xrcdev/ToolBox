using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sdcb.GgufWeights.Hy_MT2_1_8B_2Bit;
using Sdcb.HyMT2Sharp.Backends;
using Sdcb.HyMT2Sharp.Model;

namespace ToolBox.Tools.MyTranslator;

/// <summary>
/// 离线翻译引擎：基于 HyMT2Sharp（腾讯混元翻译 Hy-MT2-1.8B，2Bit 量化，纯 C# 推理）。
/// 模型权重随 NuGet 包发布，运行期完全离线；首次使用时加载并常驻进程，
/// 推理引擎变化时重建。HunyuanDenseModel 非线程安全，所有推理经信号量串行化。
/// </summary>
internal static class OfflineTranslator
{
    // 单次生成的 token 上限，翻译结果通常远短于此，命中即截断
    private const int MaxGeneratedTokens = 1024;
    // HY-MT2 官方提示词模板（与 HyMT2Sharp README 示例一致）
    private const string PromptPrefix = "Translate the following segment into Chinese, without additional explanation：";
    // 流式输出节流间隔：避免每个 token 都跨线程刷新 UI
    private const int PartialFlushIntervalMs = 64;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static HunyuanDenseModel? _model;
    // 当前已加载模型对应的引擎标识（auto/cpu/vulkan），变化时触发重建
    private static string _modelBackend = string.Empty;

    /// <summary>
    /// 同步离线翻译一段文本。<paramref name="updatePartial"/> 在后台线程被调用，
    /// 参数为“到目前为止的完整译文”，调用方需自行调度到 UI 线程。
    /// 自动模式下优先使用 GPU，加载或推理失败时自动回退纯 CPU 并整体重试一次。
    /// </summary>
    public static async Task<string> TranslateAsync(
        string sourceText,
        string backend,
        Action<string> reportStatus,
        Action<string> updatePartial,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var model = await EnsureModelAsync(backend, reportStatus);
                return await GenerateAsync(model, sourceText, reportStatus, updatePartial, cancellationToken);
            }
            catch (Exception ex) when (backend == "auto"
                && _modelBackend != "cpu"
                && !cancellationToken.IsCancellationRequested)
            {
                // GPU 的显存分配/驱动问题可能在加载或推理任一阶段暴露；丢弃 GPU 模型
                // 回退纯 CPU 重试。回退后 _modelBackend 记为 cpu，本进程内自动模式直接复用。
                reportStatus($"GPU 不可用（{ex.Message}），自动回退纯 CPU 重试...");
                _model?.Dispose();
                _model = null;
                var cpuModel = await EnsureModelAsync("cpu", reportStatus);
                updatePartial(string.Empty);
                return await GenerateAsync(cpuModel, sourceText, reportStatus, updatePartial, cancellationToken);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>外壳关闭时释放模型；若翻译仍在进行则跳过（进程即将退出，交给系统回收）。</summary>
    public static void DisposeModel()
    {
        if (!Gate.Wait(0))
        {
            return;
        }

        try
        {
            _model?.Dispose();
            _model = null;
            _modelBackend = string.Empty;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<HunyuanDenseModel> EnsureModelAsync(string backend, Action<string> reportStatus)
    {
        // 自动模式回退 CPU 后 _modelBackend 记为 cpu：本进程内再次请求自动模式
        // 直接复用 CPU 模型，避免每次翻译都重试一遍注定失败的 GPU
        if (_model is not null
            && (_modelBackend == backend || (backend == "auto" && _modelBackend == "cpu")))
        {
            return _model;
        }

        if (_model is not null)
        {
            reportStatus($"正在切换离线引擎为 {DescribeBackend(backend)}，重新加载模型...");
            _model.Dispose();
            _model = null;
        }

        reportStatus($"正在加载离线翻译模型（{DescribeBackend(backend)}，首次约需数秒）...");
        var stopwatch = Stopwatch.StartNew();
        // 内联 switch 让返回类型与 backend 参数一致：auto 交由库自动选择
        // （Metal → Vulkan → 无 GPU 时纯 CPU）；cpu 用 null 表示纯 CPU
        var model = await Task.Run(() => new HunyuanDenseModel(
            Weights.Model(),
            backend: backend switch
            {
                "vulkan" => BackendFactory.Create("vulkan"),
                "cpu" => null,
                _ => BackendFactory.Create()
            }));
        stopwatch.Stop();
        _model = model;
        _modelBackend = backend;
        reportStatus($"离线模型加载完成（{DescribeBackend(backend)}，耗时 {stopwatch.Elapsed.TotalSeconds:0.0} 秒），开始翻译...");
        return model;
    }

    private static async Task<string> GenerateAsync(
        HunyuanDenseModel model,
        string sourceText,
        Action<string> reportStatus,
        Action<string> updatePartial,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var prompt = model.Tokenizer.Encode(
                ChatTemplate.RenderHunyuanDense([new ChatMessage("user", PromptPrefix + sourceText)]));
            var logits = model.Forward(model.AlignPrompt(prompt).Suffix);

            var generated = new List<int>();
            var result = new StringBuilder();
            var visible = string.Empty;
            var lastFlush = Stopwatch.StartNew();
            for (var i = 0; i < MaxGeneratedTokens; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var token = Sampler.ArgMax(logits);
                if (model.Tokenizer.IsStop(token))
                {
                    break;
                }

                generated.Add(token);
                // 末尾 U+FFFD 表示 UTF-8 序列被 token 边界截断，等下一个 token 拼全再输出
                var next = model.Tokenizer.DecodeVisible(generated).TrimEnd('\uFFFD');
                if (next.Length > visible.Length && next.StartsWith(visible, StringComparison.Ordinal))
                {
                    result.Append(next[visible.Length..]);
                    visible = next;

                    if (lastFlush.ElapsedMilliseconds >= PartialFlushIntervalMs)
                    {
                        lastFlush.Restart();
                        reportStatus($"离线翻译中：已生成 {generated.Count} tokens...");
                        updatePartial(result.ToString());
                    }
                }

                logits = model.Forward([token]);
            }

            updatePartial(result.ToString());
            return result.ToString();
        }, cancellationToken);
    }

    /// <summary>auto 交由库自动选择（Metal → Vulkan → 无 GPU 时纯 CPU）；cpu 用 null 表示纯 CPU。</summary>
    private static string DescribeBackend(string backend)
    {
        return backend switch
        {
            "cpu" => "CPU",
            "vulkan" => "Vulkan GPU",
            _ => "自动（GPU 优先）"
        };
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using Leno.ApiGateway.Bff.Dag;
using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff.Dag.Tests;

/// <summary>
/// DAG 编排引擎单元测试：覆盖并行调度、依赖链、级联超时、节点失败、整体超时等场景。
/// <para>
/// 测试策略：
/// <list type="bullet">
///   <item>使用内联委托构造节点，避免依赖真实 HTTP 调用</item>
///   <item>通过 <see cref="ConcurrentDictionary{TKey, TValue}"/> 记录执行顺序与时间戳，验证并行度</item>
///   <item>使用短超时（200-500ms）加速超时场景测试</item>
///   <item>整体超时与节点超时分离测试，验证级联取消行为</item>
/// </list>
/// </para>
/// </summary>
public class DagOrchestratorTests
{
    private static readonly TimeSpan DefaultNodeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultOverallTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] LinearChainExpectedOrder = { "A", "B", "C" };

    private static DagOrchestrator CreateOrchestrator(TimeSpan? overallTimeout = null)
    {
        return new DagOrchestrator(
            new CascadeTimeoutPolicy(),
            NullLogger<DagOrchestrator>.Instance,
            overallTimeout ?? DefaultOverallTimeout);
    }

    private static AggregateNode CreateNode(
        string name,
        Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> executor,
        TimeSpan? timeout = null)
    {
        return new AggregateNode(name, executor, timeout ?? DefaultNodeTimeout);
    }

    private static AggregateNode CreateInstantNode(string name, object? result = null)
    {
        var value = result ?? name;
        return CreateNode(name, (_, _) => Task.FromResult<object?>(value));
    }

    /// <summary>
    /// 构造一个会延迟指定时间的节点，返回节点名作为结果。
    /// </summary>
    private static AggregateNode CreateDelayedNode(
        string name,
        TimeSpan delay,
        ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)>? timestamps = null)
    {
        return CreateNode(name, async (_, ct) =>
        {
            var start = DateTimeOffset.UtcNow;
            timestamps?.TryAdd(name, (start, default));
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            finally
            {
                if (timestamps is not null)
                {
                    timestamps[name] = (start, DateTimeOffset.UtcNow);
                }
            }
            return name;
        });
    }

    // ===== 基础场景 =====

    [Fact]
    public async Task Execute_EmptyGraph_ReturnsEmptySuccessResult()
    {
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder().Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeTrue();
        result.Partial.Should().BeFalse();
        result.Results.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_SingleNode_ReturnsNodeResult()
    {
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateInstantNode("A", "value-a"))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Results.Should().HaveCount(1);
        result.Results["A"].Should().Be("value-a");
    }

    [Fact]
    public async Task Execute_NullGraph_ThrowsArgumentNullException()
    {
        var orchestrator = CreateOrchestrator();

        var act = async () => await orchestrator.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ===== 并行调度场景 =====

    [Fact]
    public async Task Execute_ThreeIndependentNodes_AllRunInParallel()
    {
        // 三个无依赖节点，每个延迟 200ms。
        // 若串行则总耗时 ≥ 600ms，并行应接近 200-300ms。
        var timestamps = new ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)>();
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateDelayedNode("A", TimeSpan.FromMilliseconds(200), timestamps))
            .AddNode(CreateDelayedNode("B", TimeSpan.FromMilliseconds(200), timestamps))
            .AddNode(CreateDelayedNode("C", TimeSpan.FromMilliseconds(200), timestamps))
            .Build();

        var stopwatch = Stopwatch.StartNew();
        var result = await orchestrator.ExecuteAsync(graph);
        stopwatch.Stop();

        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(3);

        // 并行执行：总耗时应远小于 600ms（串行耗时）
        // 容忍度 150ms，避免 CI 环境抖动
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(600),
            "三个并行节点应同时执行，总耗时接近单个节点耗时而非三者之和");

        // 验证三节点执行时间区间有重叠
        var a = timestamps["A"];
        var b = timestamps["B"];
        var c = timestamps["C"];
        Overlaps(a, b).Should().BeTrue("A 与 B 应并行执行");
        Overlaps(a, c).Should().BeTrue("A 与 C 应并行执行");
        Overlaps(b, c).Should().BeTrue("B 与 C 应并行执行");
    }

    [Fact]
    public async Task Execute_DiamondDependency_BranchesRunInParallel()
    {
        // 菱形依赖：A → {B, C} → D
        // 第一波：A 单独执行
        // 第二波：B 与 C 并行执行
        // 第三波：D 单独执行
        var timestamps = new ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)>();
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateDelayedNode("A", TimeSpan.FromMilliseconds(150), timestamps))
            .AddNode(CreateDelayedNode("B", TimeSpan.FromMilliseconds(200), timestamps))
            .AddNode(CreateDelayedNode("C", TimeSpan.FromMilliseconds(200), timestamps))
            .AddNode(CreateDelayedNode("D", TimeSpan.FromMilliseconds(150), timestamps))
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .DependsOn("D", "B", "C")
            .Build();

        var stopwatch = Stopwatch.StartNew();
        var result = await orchestrator.ExecuteAsync(graph);
        stopwatch.Stop();

        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(4);

        // 总耗时 ≈ 150(A) + 200(B,C并行) + 150(D) = 500ms
        // 若 B,C 串行则 ≈ 700ms，容忍 200ms 抖动
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(700),
            "B 与 C 应并行执行，总耗时不应达到串行水平");

        // B 和 C 的执行时间区间应重叠
        var b = timestamps["B"];
        var c = timestamps["C"];
        Overlaps(b, c).Should().BeTrue("B 与 C 在同一波内应并行执行");

        // A 必须在 B 和 C 之前完成
        var a = timestamps["A"];
        a.End.Should().BeBefore(b.Start, "A 应在 B 之前完成");
        a.End.Should().BeBefore(c.Start, "A 应在 C 之前完成");

        // D 必须在 B 和 C 之后开始
        var d = timestamps["D"];
        d.Start.Should().BeAfter(b.End, "D 应在 B 完成后开始");
        d.Start.Should().BeAfter(c.End, "D 应在 C 完成后开始");
    }

    [Fact]
    public async Task Execute_LinearChain_ExecutesInOrder()
    {
        // 线性链：A → B → C，每个节点延迟 100ms
        var executionOrder = new ConcurrentQueue<string>();
        var orchestrator = CreateOrchestrator();

        AggregateNode RecordNode(string name, params string[] deps)
        {
            var node = CreateNode(name, async (_, ct) =>
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                executionOrder.Enqueue(name);
                return name;
            });
            foreach (var d in deps)
            {
                node.Dependencies.Add(d);
            }
            return node;
        }

        var graph = new AggregateBuilder()
            .AddNode(RecordNode("A"))
            .AddNode(RecordNode("B"))
            .AddNode(RecordNode("C"))
            .DependsOn("B", "A")
            .DependsOn("C", "B")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(3);
        executionOrder.Should().Equal(LinearChainExpectedOrder,
            "线性依赖链应严格按 A → B → C 顺序执行");
    }

    [Fact]
    public async Task Execute_NodeCanReadUpstreamResults()
    {
        // A 产生值 → B 读取 A 的值并拼接
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A", (_, _) => Task.FromResult<object?>("hello")))
            .AddNode(CreateNode("B", async (ctx, ct) =>
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
                var upstream = (string)ctx["A"]!;
                return $"{upstream}-world";
            }))
            .DependsOn("B", "A")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeTrue();
        result.Results["A"].Should().Be("hello");
        result.Results["B"].Should().Be("hello-world");
    }

    // ===== 级联超时场景 =====

    [Fact]
    public async Task Execute_NodeTimeout_CascadesToDownstreamNodes()
    {
        // A 超时 → B（依赖 A）被级联取消
        // A: timeout 200ms, executor sleeps 1000ms → 超时
        // B: depends on A, timeout 5s → 被 503 级联取消
        var bExecuted = false;
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1000), ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 节点超时后被取消，重新抛出以让编排器捕获
                        throw;
                    }
                    return "A-result";
                },
                TimeSpan.FromMilliseconds(200)))
            .AddNode(CreateNode("B",
                async (_, ct) =>
                {
                    bExecuted = true;
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    return "B-result";
                }))
            .DependsOn("B", "A")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeFalse("所有节点均失败（A 超时，B 级联取消）");
        result.Results.Should().BeEmpty();

        var aError = result.Errors.FirstOrDefault(e => e.Source == "A");
        aError.Should().NotBeNull();
        aError!.StatusCode.Should().Be(504, "A 超时应标记为 504");
        aError.Message.Should().Contain("节点超时");

        var bError = result.Errors.FirstOrDefault(e => e.Source == "B");
        bError.Should().NotBeNull();
        bError!.StatusCode.Should().Be(503, "B 应被级联取消标记为 503");
        bError.Message.Should().Contain("级联取消");

        bExecuted.Should().BeFalse("B 应被跳过，不应执行其 Executor");
    }

    [Fact]
    public async Task Execute_NodeTimeout_DoesNotAffectIndependentNodes()
    {
        // A 超时，B 独立于 A 应正常完成
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), ct).ConfigureAwait(false);
                    return "A";
                },
                TimeSpan.FromMilliseconds(200)))
            .AddNode(CreateInstantNode("B", "B-result"))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeTrue("部分成功（B）部分失败（A）");

        result.Results.Should().ContainKey("B");
        result.Results["B"].Should().Be("B-result");

        var aError = result.Errors.FirstOrDefault(e => e.Source == "A");
        aError.Should().NotBeNull();
        aError!.StatusCode.Should().Be(504);
    }

    [Fact]
    public async Task Execute_NodeTimeout_CascadePolicyRecordsTimeout()
    {
        // 验证 CascadeTimeoutPolicy.OnNodeTimeout 被调用
        var cascadePolicy = new CascadeTimeoutPolicy();
        var orchestrator = new DagOrchestrator(
            cascadePolicy,
            NullLogger<DagOrchestrator>.Instance,
            DefaultOverallTimeout);

        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), ct).ConfigureAwait(false);
                    return "A";
                },
                TimeSpan.FromMilliseconds(200)))
            .Build();

        await orchestrator.ExecuteAsync(graph);

        cascadePolicy.TotalTimeouts.Should().Be(1, "A 超时应被记录");
        cascadePolicy.IsCancelled("A").Should().BeTrue("A 应在级联策略中标记为已取消");
        cascadePolicy.CancelledNodes.Should().Contain("A");
    }

    // ===== 级联失败场景（节点抛异常） =====

    [Fact]
    public async Task Execute_NodeThrowsDagNodeException_PreservesStatusCode()
    {
        // A 抛出 DagNodeException(404) → 错误应保留 404 状态码
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                (_, _) => throw new DagNodeException("A", 404, "Not Found")))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        var error = result.Errors[0];
        error.Source.Should().Be("A");
        error.StatusCode.Should().Be(404, "DagNodeException 的 StatusCode 应被保留");
        error.Message.Should().Be("Not Found");
    }

    [Fact]
    public async Task Execute_NodeThrowsException_MarksAs500()
    {
        // A 抛出普通异常 → 错误应标记为 500
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                (_, _) => throw new InvalidOperationException("boom")))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        var error = result.Errors[0];
        error.Source.Should().Be("A");
        error.StatusCode.Should().Be(500, "普通异常应标记为 500");
        error.Message.Should().Be("boom");
    }

    [Fact]
    public async Task Execute_NodeFailure_CascadesToDownstreamNodes()
    {
        // A 失败（抛异常）→ B（依赖 A）被级联取消
        var bExecuted = false;
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                (_, _) => throw new DagNodeException("A", 500, "A failed")))
            .AddNode(CreateNode("B",
                async (_, ct) =>
                {
                    bExecuted = true;
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    return "B";
                }))
            .DependsOn("B", "A")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeFalse("所有节点均失败");

        var aError = result.Errors.FirstOrDefault(e => e.Source == "A");
        aError.Should().NotBeNull();
        aError!.StatusCode.Should().Be(500);

        var bError = result.Errors.FirstOrDefault(e => e.Source == "B");
        bError.Should().NotBeNull();
        bError!.StatusCode.Should().Be(503, "B 应被级联取消");
        bError.Message.Should().Contain("级联取消");

        bExecuted.Should().BeFalse("B 应被跳过");
    }

    [Fact]
    public async Task Execute_PartialFailure_IndependentNodeSucceeds()
    {
        // A 失败，B 独立于 A 成功 → Partial=true
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                (_, _) => throw new DagNodeException("A", 503, "A failed")))
            .AddNode(CreateInstantNode("B", "B-result"))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeTrue("部分成功（B）部分失败（A）");
        result.Results.Should().ContainKey("B");
        result.Results["B"].Should().Be("B-result");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Source.Should().Be("A");
    }

    // ===== 整体超时场景 =====

    [Fact]
    public async Task Execute_OverallTimeout_MarksUnfinishedNodesAs504()
    {
        // 整体超时 300ms，A 延迟 2000ms → A 被整体超时标记为 504
        var orchestrator = CreateOrchestrator(overallTimeout: TimeSpan.FromMilliseconds(300));
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000), ct).ConfigureAwait(false);
                    return "A";
                },
                TimeSpan.FromSeconds(10)))  // 节点超时设为 10s，确保整体超时先触发
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeFalse("无成功结果");
        result.Results.Should().BeEmpty();

        var aError = result.Errors.FirstOrDefault(e => e.Source == "A");
        aError.Should().NotBeNull();
        aError!.StatusCode.Should().Be(504, "整体超时应标记为 504");
        aError.Message.Should().Contain("整体超时");
    }

    [Fact]
    public async Task Execute_OverallTimeout_CompletedNodesPreservedInResults()
    {
        // 整体超时 400ms：A 延迟 100ms（先完成），B 延迟 2000ms（超时）
        var orchestrator = CreateOrchestrator(overallTimeout: TimeSpan.FromMilliseconds(400));
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
                    return "A-done";
                }))
            .AddNode(CreateNode("B",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000), ct).ConfigureAwait(false);
                    return "B-done";
                }))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeTrue("A 成功但 B 因整体超时失败");
        result.Results.Should().ContainKey("A");
        result.Results["A"].Should().Be("A-done");

        var bError = result.Errors.FirstOrDefault(e => e.Source == "B");
        bError.Should().NotBeNull();
        bError!.StatusCode.Should().Be(504);
    }

    // ===== 调用方取消场景 =====

    [Fact]
    public async Task Execute_CallerCancellation_MarksUnfinishedNodesAs499()
    {
        // 调用方主动取消：A 延迟 2000ms，外部在 100ms 后取消
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000), ct).ConfigureAwait(false);
                    return "A";
                },
                TimeSpan.FromSeconds(10)))  // 节点超时设为 10s，确保调用方取消先触发
            .Build();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await orchestrator.ExecuteAsync(graph, cts.Token);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeFalse();

        var aError = result.Errors.FirstOrDefault(e => e.Source == "A");
        aError.Should().NotBeNull();
        aError!.StatusCode.Should().Be(499, "调用方取消应标记为 499");
        aError.Message.Should().Contain("调用方取消");
    }

    // ===== 多波复杂场景 =====

    [Fact]
    public async Task Execute_ComplexDependencyChain_AllSucceed()
    {
        // 复杂依赖链：
        //   A → {B, C} → D → {E, F}
        // 波次：1[A] 2[B,C] 3[D] 4[E,F]
        var executionOrder = new ConcurrentQueue<string>();
        var timestamps = new ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)>();

        AggregateNode TrackNode(string name, int delayMs, params string[] deps)
        {
            var node = CreateNode(name, async (_, ct) =>
            {
                var start = DateTimeOffset.UtcNow;
                timestamps.TryAdd(name, (start, default));
                try
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    executionOrder.Enqueue(name);
                    return name;
                }
                finally
                {
                    timestamps[name] = (start, DateTimeOffset.UtcNow);
                }
            });
            foreach (var d in deps)
            {
                node.Dependencies.Add(d);
            }
            return node;
        }

        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(TrackNode("A", 50))
            .AddNode(TrackNode("B", 100, "A"))
            .AddNode(TrackNode("C", 100, "A"))
            .AddNode(TrackNode("D", 50, "B", "C"))
            .AddNode(TrackNode("E", 50, "D"))
            .AddNode(TrackNode("F", 50, "D"))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Results.Should().HaveCount(6);

        // 验证拓扑顺序：A 在 B/C 之前，B/C 在 D 之前，D 在 E/F 之前
        var order = executionOrder.ToArray();
        var aIdx = Array.IndexOf(order, "A");
        var bIdx = Array.IndexOf(order, "B");
        var cIdx = Array.IndexOf(order, "C");
        var dIdx = Array.IndexOf(order, "D");
        var eIdx = Array.IndexOf(order, "E");
        var fIdx = Array.IndexOf(order, "F");

        aIdx.Should().BeLessThan(bIdx, "A 应在 B 之前执行");
        aIdx.Should().BeLessThan(cIdx, "A 应在 C 之前执行");
        bIdx.Should().BeLessThan(dIdx, "B 应在 D 之前执行");
        cIdx.Should().BeLessThan(dIdx, "C 应在 D 之前执行");
        dIdx.Should().BeLessThan(eIdx, "D 应在 E 之前执行");
        dIdx.Should().BeLessThan(fIdx, "D 应在 F 之前执行");

        // B 和 C 应并行执行
        Overlaps(timestamps["B"], timestamps["C"]).Should().BeTrue("B 和 C 应并行执行");

        // E 和 F 应并行执行
        Overlaps(timestamps["E"], timestamps["F"]).Should().BeTrue("E 和 F 应并行执行");
    }

    [Fact]
    public async Task Execute_MidChainFailure_CascadesOnlyDownstream()
    {
        // 复杂依赖链中的中间节点失败：
        //   A → B（失败）→ C
        //   D 独立于 A/B/C
        // 预期：A 成功，B 失败（500），C 级联取消（503），D 成功
        var cExecuted = false;
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateInstantNode("A", "A-result"))
            .AddNode(CreateNode("B",
                async (_, ct) =>
                {
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    throw new DagNodeException("B", 500, "B failed");
                }))
            .AddNode(CreateNode("C",
                async (_, ct) =>
                {
                    cExecuted = true;
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    return "C";
                }))
            .AddNode(CreateInstantNode("D", "D-result"))
            .DependsOn("B", "A")
            .DependsOn("C", "B")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeTrue("A 和 D 成功，B 和 C 失败");
        result.Results.Should().HaveCount(2);
        result.Results.Should().ContainKey("A");
        result.Results.Should().ContainKey("D");
        result.Errors.Should().HaveCount(2);

        var bError = result.Errors.First(e => e.Source == "B");
        bError.StatusCode.Should().Be(500);

        var cError = result.Errors.First(e => e.Source == "C");
        cError.StatusCode.Should().Be(503, "C 应被级联取消");
        cError.Message.Should().Contain("级联取消");

        cExecuted.Should().BeFalse("C 应被跳过");
    }

    [Fact]
    public async Task Execute_AllNodesFail_PartialFalse()
    {
        // 所有节点均失败 → Partial=false
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A", (_, _) => throw new InvalidOperationException("A failed")))
            .AddNode(CreateNode("B", (_, _) => throw new InvalidOperationException("B failed")))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeFalse("无成功结果，Partial=false");
        result.Results.Should().BeEmpty();
        result.Errors.Should().HaveCount(2);
    }

    // ===== AggregateResult 辅助方法 =====

    [Fact]
    public async Task Execute_GetResult_ReturnsTypedValue()
    {
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateNode("A", (_, _) => Task.FromResult<object?>(42)))
            .AddNode(CreateNode("B", (_, _) => Task.FromResult<object?>("hello")))
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.GetResult<int>("A").Should().Be(42);
        result.GetResult<string>("B").Should().Be("hello");
        result.GetResult<int>("B").Should().Be(0, "类型不匹配应返回 default");
        result.GetResult<string>("NonExistent").Should().BeNull("不存在的节点应返回 default");
    }

    [Fact]
    public async Task Execute_MultipleWaves_CascadeFromTimeoutInMiddle()
    {
        // 中间节点超时，下游级联取消，但独立分支正常执行
        //   A → B（超时）→ C
        //   D → E
        // 预期：A 成功，B 超时 504，C 级联 503，D/E 成功
        var cExecuted = false;
        var orchestrator = CreateOrchestrator();
        var graph = new AggregateBuilder()
            .AddNode(CreateInstantNode("A", "A-result"))
            .AddNode(CreateNode("B",
                async (_, ct) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), ct).ConfigureAwait(false);
                    return "B";
                },
                TimeSpan.FromMilliseconds(200)))
            .AddNode(CreateNode("C",
                async (_, ct) =>
                {
                    cExecuted = true;
                    await Task.Delay(10, ct).ConfigureAwait(false);
                    return "C";
                }))
            .AddNode(CreateInstantNode("D", "D-result"))
            .AddNode(CreateInstantNode("E", "E-result"))
            .DependsOn("B", "A")
            .DependsOn("C", "B")
            .DependsOn("E", "D")
            .Build();

        var result = await orchestrator.ExecuteAsync(graph);

        result.Success.Should().BeFalse();
        result.Partial.Should().BeTrue("部分成功（A, D, E）部分失败（B, C）");

        result.Results.Should().HaveCount(3);
        result.Results.Should().ContainKey("A");
        result.Results.Should().ContainKey("D");
        result.Results.Should().ContainKey("E");

        result.Errors.Should().HaveCount(2);
        var bError = result.Errors.First(e => e.Source == "B");
        bError.StatusCode.Should().Be(504, "B 应超时");

        var cError = result.Errors.First(e => e.Source == "C");
        cError.StatusCode.Should().Be(503, "C 应级联取消");

        cExecuted.Should().BeFalse("C 应被跳过");
    }

    /// <summary>
    /// 判断两个时间区间是否重叠。
    /// </summary>
    private static bool Overlaps((DateTimeOffset Start, DateTimeOffset End) x, (DateTimeOffset Start, DateTimeOffset End) y)
    {
        return x.Start < y.End && y.Start < x.End;
    }
}

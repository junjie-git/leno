using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 策略链调度器（阶段四 4.2：可插拔策略链）。
/// <para>
/// 接收 DI 容器注入的所有 <see cref="IAclChannel"/> 实现，按 <see cref="IAclChannel.Priority"/> 升序排列，
/// 遍历策略链：跳过熔断 Open 的通道；调用 <see cref="IAclChannel.HealthCheckAsync"/> 探测可用性；
/// 任一通道返回 <see cref="AclResponse"/> 即短路返回；抛 <see cref="AclChannelException"/> 时降级到下一通道；
/// 所有通道耗尽抛 <c>ChannelName=all</c> 的 <see cref="AclChannelException"/>。
/// </para>
/// <para>
/// 与 <see cref="AntiCorruptionDispatcher{TService}"/> 共存：
/// <list type="bullet">
/// <item>非泛型版本（本类）：基于 <see cref="IAclChannel"/> 策略链，新协议零侵入接入</item>
/// <item>泛型版本 <see cref="AntiCorruptionDispatcher{TService}"/>：M4 双轨方案保留向后兼容</item>
/// </list>
/// </para>
/// <para>
/// 双轨期：feature flag <c>AntiCorruption:UseStrategyChain</c> 按 BC 切流，
/// 新代码使用本类（基于 <see cref="IAclChannel"/>），旧代码继续使用泛型版本，
/// 双轨期 4 周后下线泛型版本。
/// </para>
/// </summary>
public sealed class AntiCorruptionDispatcher
{
    private readonly IAclChannel[] _channels; // 按 Priority 升序排列
    private readonly CircuitBreakerState[] _breakerStates;
    private readonly ILogger<AntiCorruptionDispatcher> _logger;
    private readonly AclChannelRegistry? _registry;

    /// <summary>构造策略链调度器（自带熔断器数组，每通道一个独立熔断器）。</summary>
    /// <param name="channels">DI 容器注入的所有通道（按 Priority 升序遍历）。</param>
    /// <param name="logger">日志记录器。</param>
    public AntiCorruptionDispatcher(
        IEnumerable<IAclChannel> channels,
        ILogger<AntiCorruptionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(logger);

        _channels = channels.OrderBy(c => c.Priority).ThenBy(c => c.Name).ToArray();
        if (_channels.Length == 0)
        {
            throw new InvalidOperationException(
                "AntiCorruptionDispatcher 至少需要一个 IAclChannel 实现，请检查 DI 注册");
        }
        _logger = logger;

        // 为每个通道创建独立熔断器（默认阈值 3/2/30s，与既有 CircuitBreakerState 默认值一致）
        _breakerStates = _channels
            .Select(c => new CircuitBreakerState(
                $"{c.Name}_{c.Priority}",
                failureThreshold: 3,
                successThreshold: 2,
                openDuration: TimeSpan.FromSeconds(30)))
            .ToArray();
    }

    /// <summary>构造策略链调度器（使用 <see cref="AclChannelRegistry"/> 的熔断器，复用注册表查询能力）。</summary>
    /// <param name="registry">ACL 通道注册表。</param>
    /// <param name="logger">日志记录器。</param>
    public AntiCorruptionDispatcher(
        AclChannelRegistry registry,
        ILogger<AntiCorruptionDispatcher> logger)
        : this(registry?.Channels ?? throw new ArgumentNullException(nameof(registry)), logger)
    {
        _registry = registry;
    }

    /// <summary>已注册的通道列表（按 Priority 升序）。</summary>
    public IReadOnlyList<IAclChannel> Channels => _channels;

    /// <summary>
    /// 按优先级遍历通道，返回首个成功的响应；所有通道耗尽抛 <see cref="AclChannelException"/>。
    /// </summary>
    /// <param name="request">ACL 请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>首个成功通道的响应。</returns>
    /// <exception cref="AclChannelException">所有通道均失败时抛出，<see cref="AclChannelException.ChannelName"/> = "all"。</exception>
    public async Task<AclResponse> DispatchAsync(AclRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Exception? lastException = null;

        for (int i = 0; i < _channels.Length; i++)
        {
            var channel = _channels[i];
            var breaker = _breakerStates[i];

            // 跳过熔断 Open 状态的通道
            if (breaker.GetState() == CircuitState.Open)
            {
                _logger.LogDebug(
                    "ACL channel {Channel} circuit open, skipping for {Service}/{Operation}",
                    channel.Name, request.TargetService, request.OperationName);
                AntiCorruptionMetrics.RecordFallback(
                    request.TargetService,
                    $"{channel.Name}_circuit_open");
                AntiCorruptionMetrics.RecordChannelDispatch(
                    channel.Name, request.TargetService, request.OperationName, "circuit_open");
                continue;
            }

            // 健康检查：失败则记录熔断并尝试下一通道
            bool healthy;
            try
            {
                healthy = await channel.HealthCheckAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "ACL channel {Channel} health check failed for {Service}/{Operation}",
                    channel.Name, request.TargetService, request.OperationName);
                healthy = false;
            }

            if (!healthy)
            {
                breaker.RecordFailure();
                AntiCorruptionMetrics.RecordFallback(
                    request.TargetService,
                    $"{channel.Name}_health_check_failed");
                AntiCorruptionMetrics.RecordChannelFailure(
                    channel.Name, request.TargetService, request.OperationName);
                AntiCorruptionMetrics.RecordChannelDispatch(
                    channel.Name, request.TargetService, request.OperationName, "health_check_failed");
                _registry?.RecordChannelFailure(channel.Name);
                continue;
            }

            // 发送请求：成功返回，AclChannelException 降级到下一通道
            try
            {
                var response = await channel.SendAsync(request, ct).ConfigureAwait(false);

                // 业务层失败（Success=false）：不降级，直接返回调用方
                // 业务错误（如商品不存在、库存不足）是确定的语义结果，重试其他通道结果一致
                breaker.RecordSuccess();
                _registry?.RecordChannelSuccess(channel.Name);
                AntiCorruptionMetrics.RecordChannelDispatch(
                    channel.Name,
                    request.TargetService,
                    request.OperationName,
                    response.Success ? "success" : "business_failure");
                return response;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (AclChannelException ex)
            {
                lastException = ex;
                breaker.RecordFailure();
                _registry?.RecordChannelFailure(channel.Name);
                AntiCorruptionMetrics.RecordFallback(
                    request.TargetService,
                    $"{channel.Name}_send_failed");
                AntiCorruptionMetrics.RecordChannelFailure(
                    channel.Name, request.TargetService, request.OperationName);
                AntiCorruptionMetrics.RecordChannelDispatch(
                    channel.Name, request.TargetService, request.OperationName, "infra_failure");
                _logger.LogWarning(ex,
                    "ACL channel {Channel} failed for {Service}/{Operation}, trying next channel",
                    channel.Name, request.TargetService, request.OperationName);
                // 继续尝试下一通道
            }
        }

        // 所有通道耗尽：抛 AclChannelException(ChannelName="all")
        var exhaustedMessage = lastException is not null
            ? $"All ACL channels exhausted for operation {request.TargetService}/{request.OperationName}. Last error: {lastException.Message}"
            : $"All ACL channels exhausted for operation {request.TargetService}/{request.OperationName}";

        _logger.LogError("ACL all channels exhausted for {Service}/{Operation}",
            request.TargetService, request.OperationName);
        throw new AclChannelException("all", exhaustedMessage, lastException);
    }
}

/// <summary>
/// 双轨调度器（M4 双轨方案）。
/// 接收同一接口 <typeparamref name="TService"/> 的 HttpClient 实现（必填）与 gRPC 实现（可选），
/// 每次 <see cref="ExecuteAsync{TResult}"/> 根据 <c>UseGrpc</c> 开关与熔断状态选择实现。
/// 设计要点：
/// 1. 通过 <see cref="IOptionsMonitor{AntiCorruptionOptions}"/> 每次请求读取最新配置，支持 ConsulConfigWatcher 热更新
/// 2. 熔断器为 Keyed Singleton（每个防腐层一个实例），跨请求累积失败计数
/// 3. 仅 gRPC 不可用异常（Unavailable/DeadlineExceeded/Internal/ResourceExhausted）触发降级，业务异常直接抛
/// 4. 熔断 Open 期间所有 gRPC 调用直接降级到 HttpClient，不调 gRPC
/// </summary>
public sealed class AntiCorruptionDispatcher<TService> : IDisposable
    where TService : class
{
    private readonly TService _httpImplementation;
    private readonly TService? _grpcImplementation;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _optionsMonitor;
    private readonly ILogger<AntiCorruptionDispatcher<TService>> _logger;
    private readonly CircuitBreakerState _circuitBreaker;
    private readonly string _serviceName;

    public AntiCorruptionDispatcher(
        TService httpImplementation,
        TService? grpcImplementation,
        IOptionsMonitor<AntiCorruptionOptions> optionsMonitor,
        ILogger<AntiCorruptionDispatcher<TService>> logger,
        string serviceName,
        CircuitBreakerState circuitBreaker)
    {
        ArgumentNullException.ThrowIfNull(httpImplementation);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _httpImplementation = httpImplementation;
        _grpcImplementation = grpcImplementation;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _serviceName = serviceName;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // 每次请求读取最新配置（支持 ConsulConfigWatcher 热更新）
        var currentOptions = _optionsMonitor.CurrentValue;

        if (!currentOptions.UseGrpc || _grpcImplementation is null)
        {
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        var state = _circuitBreaker.GetState();
        if (state == CircuitState.Open)
        {
            _logger.LogWarning("AntiCorruption {Service} gRPC circuit open, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, "circuit_open");
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        try
        {
            var result = await operation(_grpcImplementation).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (AntiCorruptionException ex) when (IsGrpcUnavailable(ex))
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "AntiCorruption {Service} gRPC unavailable, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, ExtractReason(ex));

            // 熔断因本次失败触发 → 本次直接抛（下次走 HTTP）
            if (_circuitBreaker.GetState() == CircuitState.Open)
            {
                throw;
            }

            // 熔断未触发 → 本次降级到 HttpClient
            return await operation(_httpImplementation).ConfigureAwait(false);
        }
    }

    /// <summary>判断 AntiCorruptionException 是否由 gRPC 不可用引起（用于决定是否降级）。</summary>
    private static bool IsGrpcUnavailable(AntiCorruptionException ex)
    {
        if (ex.InnerException is not RpcException rpc) return false;
        return rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.ResourceExhausted;
    }

    private static string ExtractReason(AntiCorruptionException ex)
        => ex.InnerException is RpcException rpc ? $"grpc_{rpc.StatusCode}" : "grpc_unknown";

    /// <summary>
    /// 释放 Dispatcher 资源。
    /// <para>
    /// 注意：<see cref="_circuitBreaker"/> 是通过 DI 容器解析的 KeyedSingleton，
    /// 生命周期由 DI 容器统一管理。本 Dispatcher 注册为 Scoped，不应在 Dispose 时
    /// 销毁共享的 KeyedSingleton，否则同进程中其他 Scope 的 Dispatcher 熔断器指标状态丢失。
    /// </para>
    /// </summary>
    public void Dispose()
    {
        // 不 Dispose _circuitBreaker — 它是 KeyedSingleton，由 DI 容器管理生命周期。
        // 仅清理 Dispatcher 自身拥有的非共享资源（当前无）。
        GC.SuppressFinalize(this);
    }
}

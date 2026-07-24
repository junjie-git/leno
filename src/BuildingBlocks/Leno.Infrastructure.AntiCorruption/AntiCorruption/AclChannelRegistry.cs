using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 通道注册表（阶段四 4.2：可插拔策略链）。
/// <para>
/// 接收 DI 容器注入的所有 <see cref="IAclChannel"/> 实现，
/// 按 <see cref="IAclChannel.Priority"/> 升序排列（数值越小优先级越高），
/// 为 <see cref="AntiCorruptionDispatcher"/> 提供有序通道列表查询。
/// </para>
/// <para>
/// 主要职责：
/// <list type="bullet">
/// <item>启动时收集所有 IAclChannel 并按优先级排序</item>
/// <item>检测通道名冲突（同优先级 / 同名称）</item>
/// <item>提供 <see cref="GetAvailableChannels"/> 查询：可叠加熔断状态过滤</item>
/// <item>维护通道熔断器映射：每个 channel 关联一个独立 <see cref="CircuitBreakerState"/></item>
/// </list>
/// </para>
/// </summary>
public sealed class AclChannelRegistry
{
    private readonly IReadOnlyList<IAclChannel> _sortedChannels;
    private readonly IReadOnlyDictionary<string, CircuitBreakerState> _breakerStates;
    private readonly ILogger<AclChannelRegistry>? _logger;

    /// <summary>
    /// 构造 ACL 通道注册表。
    /// </summary>
    /// <param name="channels">DI 容器注入的所有通道。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="optionsMonitor">防腐层配置监听器；为 null 时熔断器使用默认阈值 3/2/30s。</param>
    public AclChannelRegistry(
        IEnumerable<IAclChannel> channels,
        ILogger<AclChannelRegistry>? logger = null,
        Microsoft.Extensions.Options.IOptionsMonitor<AntiCorruptionOptions>? optionsMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(channels);
        _logger = logger;

        var channelList = channels.ToList();
        if (channelList.Count == 0)
        {
            throw new InvalidOperationException(
                "AclChannelRegistry 至少需要一个 IAclChannel 实现，请检查 DI 注册");
        }

        // 按 Priority 升序排列（数值越小优先级越高）
        _sortedChannels = channelList.OrderBy(c => c.Priority).ThenBy(c => c.Name).ToList().AsReadOnly();

        // 校验通道名唯一性（不允许重名）
        var duplicateNames = _sortedChannels
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"IAclChannel 通道名冲突：{string.Join(", ", duplicateNames)}。请为每个通道分配唯一 Name。");
        }

        // 为每个通道创建独立的熔断器（Keyed Singleton 由本注册表托管）
        var breakers = new Dictionary<string, CircuitBreakerState>(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in _sortedChannels)
        {
            // 使用 channel.Name 作为 serviceName 标识，避免与其他通道的熔断状态串扰
            // 通道熔断器默认阈值：FailureThreshold=3, SuccessThreshold=2, OpenDurationSeconds=30
            // 若配置了 IOptionsMonitor 则支持热更新（与既有 CircuitBreakerState 一致）
            var breaker = optionsMonitor is not null
                ? new CircuitBreakerState($"{channel.Name}_{channel.Priority}", optionsMonitor)
                : new CircuitBreakerState($"{channel.Name}_{channel.Priority}", 3, 2, TimeSpan.FromSeconds(30));
            breakers[channel.Name] = breaker;
        }
        _breakerStates = breakers;
    }

    /// <summary>所有已注册通道（按 Priority 升序，只读）。</summary>
    public IReadOnlyList<IAclChannel> Channels => _sortedChannels;

    /// <summary>已注册通道数量。</summary>
    public int Count => _sortedChannels.Count;

    /// <summary>获取指定通道名关联的熔断器状态机。</summary>
    /// <param name="channelName">通道名（如 "grpc", "http"）。</param>
    /// <returns>熔断器状态机；未注册时抛 KeyNotFoundException。</returns>
    public CircuitBreakerState GetCircuitBreaker(string channelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        return _breakerStates.TryGetValue(channelName, out var breaker)
            ? breaker
            : throw new KeyNotFoundException($"未注册通道 '{channelName}' 的熔断器");
    }

    /// <summary>尝试获取指定通道名关联的熔断器状态机。</summary>
    public bool TryGetCircuitBreaker(string channelName, out CircuitBreakerState? breaker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        return _breakerStates.TryGetValue(channelName, out breaker);
    }

    /// <summary>
    /// 返回当前可用的通道列表（按 Priority 升序，过滤已熔断 Open 的通道）。
    /// 调度器可选择只遍历此列表，跳过熔断中通道。
    /// </summary>
    /// <remarks>
    /// 本方法仅返回按优先级排序的通道列表；不主动执行健康检查（避免阻塞调用线程）。
    /// 调度器在遍历通道时按需调用 <see cref="IAclChannel.HealthCheckAsync"/> 决定是否跳过。
    /// </remarks>
    public IReadOnlyList<IAclChannel> GetAvailableChannels()
    {
        return _sortedChannels.Where(c =>
            {
                if (!_breakerStates.TryGetValue(c.Name, out var breaker))
                    return true;
                var state = breaker.GetState();
                return state != CircuitState.Open;
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>按通道名查找通道；未找到返回 null。</summary>
    public IAclChannel? FindByName(string channelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        return _sortedChannels.FirstOrDefault(c =>
            string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>记录某通道调用成功，更新其熔断器状态。</summary>
    public void RecordChannelSuccess(string channelName)
    {
        if (TryGetCircuitBreaker(channelName, out var breaker))
            breaker.RecordSuccess();
    }

    /// <summary>记录某通道调用失败，更新其熔断器状态。</summary>
    public void RecordChannelFailure(string channelName)
    {
        if (TryGetCircuitBreaker(channelName, out var breaker))
            breaker.RecordFailure();
    }
}

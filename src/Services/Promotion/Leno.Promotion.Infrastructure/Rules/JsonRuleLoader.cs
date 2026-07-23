using System.Collections.Concurrent;
using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 规则定义加载器实现。
/// 启动时通过 <see cref="IHostedService"/> 接口加载所有启用规则定义到内存缓存；
/// 规则定义变更后由应用层调用 <see cref="ReloadAsync"/> 触发即时刷新；
/// 同时由后台定时器每 60 秒兜底刷新一次，避免事件丢失导致缓存陈旧。
/// 缓存线程安全，使用 <see cref="ConcurrentDictionary"/> 与原子快照替换。
/// </summary>
public sealed class JsonRuleLoader : IJsonRuleLoader, IHostedService, IDisposable
{
    private readonly IPromotionRuleDefinitionRepository _repository;
    private readonly ILogger<JsonRuleLoader> _logger;
    private readonly ConcurrentDictionary<string, JsonRuleDefinition> _cache = new();
    private long _cachedVersion;
    private DateTime? _loadedAt;
    private Timer? _refreshTimer;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private int _loading; // 0 = idle, 1 = loading，避免并发刷新重叠

    public JsonRuleLoader(
        IPromotionRuleDefinitionRepository repository,
        ILogger<JsonRuleLoader> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public long CachedVersion => Interlocked.Read(ref _cachedVersion);

    /// <inheritdoc />
    public DateTime? LoadedAt => _loadedAt;

    /// <inheritdoc />
    public JsonRuleDefinition? GetDefinition(string ruleType)
    {
        if (string.IsNullOrWhiteSpace(ruleType))
        {
            return null;
        }

        return _cache.TryGetValue(ruleType, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken ct = default)
    {
        await LoadCoreAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await LoadCoreAsync(ct);
    }

    /// <summary>
    /// 处理规则定义变更领域事件（由 <c>PromotionRuleDefinitionAppService</c> 在 SaveEntitiesAsync 后调用）。
    /// 与 <see cref="ReloadAsync(CancellationToken)"/> 等价，签名对齐事件处理约定便于后续接入 MediatR。
    /// </summary>
    public async Task HandleChangedEventAsync(
        PromotionRuleDefinitionChangedEvent @event,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _logger.LogInformation(
            "收到规则定义变更事件：RuleType={RuleType} Version={Version}，触发缓存刷新",
            @event.RuleType, @event.Version);
        await LoadCoreAsync(ct);
    }

    /// <inheritdoc />
    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JsonRuleLoader 启动，开始加载规则定义缓存");
        try
        {
            await LoadCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // 启动加载失败不阻塞服务启动，规则实现会回退到默认行为
            _logger.LogError(ex, "JsonRuleLoader 启动加载失败，规则将使用默认行为，等待定时刷新重试");
        }

        // 兜底定时刷新：避免应用层事件丢失导致缓存陈旧
        _refreshTimer = new Timer(
            async _ => await SafeRefreshAsync(),
            null,
            RefreshInterval,
            RefreshInterval);
    }

    /// <inheritdoc />
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JsonRuleLoader 停止");
        _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    /// <summary>
    /// 核心加载逻辑：从 DB 读取启用规则定义，反序列化为 <see cref="JsonRuleDefinition"/> 后原子替换缓存。
    /// 使用 <see cref="Interlocked.CompareExchange"/> 防止并发刷新重叠。
    /// </summary>
    private async Task LoadCoreAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
        {
            // 已有刷新进行中，跳过本次（避免 DB 抖动）
            _logger.LogDebug("JsonRuleLoader 加载进行中，跳过本次刷新");
            return;
        }

        try
        {
            var definitions = await _repository.GetEnabledAsync(ct);
            var snapshot = new ConcurrentDictionary<string, JsonRuleDefinition>();

            foreach (var def in definitions)
            {
                var parsed = JsonRuleDefinition.FromJson(def.DefinitionJson);
                if (parsed is null)
                {
                    _logger.LogWarning(
                        "规则定义 {RuleDefinitionId}（RuleType={RuleType}）的 DefinitionJson 反序列化失败，跳过",
                        def.Id, def.RuleType);
                    continue;
                }

                // 同步聚合根的元数据到 JsonRuleDefinition，保证规则实现读取时与 DB 一致
                parsed.RuleType = def.RuleType;
                parsed.Priority = def.Priority;
                parsed.Stacking = def.Stacking;

                snapshot[def.RuleType] = parsed;
            }

            // 原子替换：清空旧缓存再填充新快照（避免半刷新状态）
            _cache.Clear();
            foreach (var kv in snapshot)
            {
                _cache[kv.Key] = kv.Value;
            }

            Interlocked.Increment(ref _cachedVersion);
            _loadedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "JsonRuleLoader 加载完成：{Count} 条规则定义，版本 {Version}，时间 {LoadedAt}",
                _cache.Count, CachedVersion, _loadedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonRuleLoader 加载失败：缓存未更新，保持旧版本 {Version}", CachedVersion);
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    /// <summary>定时器回调包装，吞掉异常避免 Timer 后台取消。</summary>
    private async Task SafeRefreshAsync()
    {
        try
        {
            await LoadCoreAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JsonRuleLoader 定时刷新失败");
        }
    }
}

using System.Text.Json;
using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 发件箱后台发布器，轮询发件箱表，将待发布消息发布到事件总线。
/// 采用两阶段标记防重复发布：
/// 1) 事务内置 <see cref="OutboxMessageStatus.Publishing"/> 中间态并提交；
/// 2) 发布到 MQ；
/// 3) 置 <see cref="OutboxMessageStatus.Processed"/> 并提交。
/// 若发布失败，回退为 <see cref="OutboxMessageStatus.Pending"/> 等待下次重试；
/// 若发布成功但 Processed 标记失败，由 <see cref="RecoverStalePublishingAsync"/> 在下次轮询扫描超时后回退 Pending，
/// 依赖下游消费者幂等性保证不重复执行业务。
/// <para>
/// T22 增强：
/// <list type="bullet">
/// <item>并行处理：使用 <see cref="Parallel.ForEachAsync"/> 并行发布批次内消息（默认 DOP=4），每条消息独立事务保持两阶段语义</item>
/// <item>积压告警：每次轮询后统计 pending 数量，超阈值（默认 100）记录结构化告警日志</item>
/// <item>类型解析：通过 <see cref="IOutboxEventTypeResolver"/> 按 FullName 解析事件类型，兼容 BC 版本升级（程序集版本变更/命名空间迁移）</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TDbContext">承载发件箱表的 DbContext 类型。</typeparam>
public class OutboxPublisher<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxPublisher<TDbContext>> _logger;
    private readonly IOutboxEventTypeResolver _typeResolver;

    private const int BatchSize = 50;
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    /// <summary>Publishing 状态超时阈值，超过此时间认为发布中断（应用重启/标记失败），回退 Pending。</summary>
    private static readonly TimeSpan PublishingStaleTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>并行发布默认并行度。可通过 <c>Outbox:MaxDegreeOfParallelism</c> 配置覆盖。</summary>
    private const int DefaultMaxDegreeOfParallelism = 4;

    /// <summary>pending 积压告警阈值。可通过 <c>Outbox:PendingAlertThreshold</c> 配置覆盖。</summary>
    private const int DefaultPendingAlertThreshold = 100;

    /// <summary>
    /// 并行发布并行度，测试可通过此属性覆盖默认值。
    /// 生产环境使用 <see cref="DefaultMaxDegreeOfParallelism"/>。
    /// </summary>
    internal int MaxDegreeOfParallelism { get; set; } = DefaultMaxDegreeOfParallelism;

    /// <summary>
    /// pending 积压告警阈值，测试可通过此属性覆盖默认值。
    /// 生产环境使用 <see cref="DefaultPendingAlertThreshold"/>。
    /// </summary>
    internal int PendingAlertThreshold { get; set; } = DefaultPendingAlertThreshold;

    public OutboxPublisher(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILogger<OutboxPublisher<TDbContext>> logger,
        IOutboxEventTypeResolver? typeResolver = null)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
        _typeResolver = typeResolver ?? DefaultOutboxEventTypeResolver.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 每次轮询首先扫描重启或上次中断遗留的 Publishing 超时消息
                await RecoverStalePublishingAsync(stoppingToken);
                await ProcessBatchAsync(stoppingToken);
                // 每次轮询后统计 pending 积压并告警
                await AlertIfPendingBacklogAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发件箱轮询异常");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 扫描处于 <see cref="OutboxMessageStatus.Publishing"/> 状态超过 <see cref="PublishingStaleTimeout"/> 的消息，
    /// 将其重置为 <see cref="OutboxMessageStatus.Pending"/> 以便下次轮询重试。
    /// 依赖下游消费者幂等性保证不重复执行业务。
    /// </summary>
    internal async Task RecoverStalePublishingAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var staleThreshold = DateTime.UtcNow - PublishingStaleTimeout;
        var staleMessages = await context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Publishing
                        && m.PublishingStartedAt != null
                        && m.PublishingStartedAt < staleThreshold)
            .OrderBy(m => m.PublishingStartedAt)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (staleMessages.Count == 0)
        {
            return;
        }

        _logger.LogWarning("扫描到 {Count} 条 Publishing 超时消息，回退至 Pending 等待重试", staleMessages.Count);

        foreach (var message in staleMessages)
        {
            message.ResetStalePublishing();
        }

        await context.SaveChangesAsync(stoppingToken);
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        // 阶段 0：在主作用域内拉取本批次待发布消息 ID（DbContext 非线程安全，并行处理需每条消息独立作用域）
        List<Guid> pendingIds;
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
            pendingIds = await context.Set<OutboxMessage>()
                .Where(m => m.Status == OutboxMessageStatus.Pending)
                .OrderBy(m => m.OccurredAt)
                .Take(BatchSize)
                .Select(m => m.Id)
                .ToListAsync(stoppingToken);
        }

        if (pendingIds.Count == 0)
        {
            return;
        }

        // 阶段 1+2+3：并行处理每条消息，每条消息独立作用域 + 独立 DbContext + 独立事务，保持两阶段标记语义
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(pendingIds, parallelOptions, async (messageId, ct) =>
        {
            try
            {
                await PublishSingleByIdAsync(messageId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 单条消息处理异常不应中断整个并行批次
                _logger.LogError(ex, "发件箱消息处理异常 Id={MessageId}", messageId);
            }
        });
    }

    /// <summary>
    /// 测试入口：处理一批 Pending 消息。生产代码由 <see cref="ExecuteAsync"/> 调用。
    /// </summary>
    internal Task ProcessBatchForTestAsync(CancellationToken ct) => ProcessBatchAsync(ct);

    /// <summary>
    /// 按消息 ID 重新加载并发布单条消息（独立作用域 + 独立 DbContext）。
    /// 用于并行处理场景，保证每条消息的事务隔离。
    /// </summary>
    private async Task PublishSingleByIdAsync(Guid messageId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var message = await context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == messageId, stoppingToken);

        if (message is null)
        {
            // 消息可能在并行调度期间已被其它轮询处理（理论上不会，因为同一批次 ID 唯一）
            _logger.LogDebug("发件箱消息未找到 Id={MessageId}，可能已被处理", messageId);
            return;
        }

        // 仅处理仍为 Pending 的消息，跳过状态已被并行任务改变的（如 RecoverStalePublishing 重置）
        if (message.Status != OutboxMessageStatus.Pending)
        {
            _logger.LogDebug("发件箱消息状态非 Pending，跳过 Id={MessageId} Status={Status}", messageId, message.Status);
            return;
        }

        await PublishSingleAsync(context, message, stoppingToken);
    }

    /// <summary>
    /// 单条消息的两阶段发布：
    /// 1) 事务内置 Publishing 提交；
    /// 2) 发布 MQ；
    /// 3) 置 Processed 提交（失败则由 RecoverStalePublishingAsync 兜底）。
    /// 发布失败时回退 Pending 等待下次重试，重试次数超阈值进入 DeadLetter。
    /// </summary>
    private async Task PublishSingleAsync(TDbContext context, OutboxMessage message, CancellationToken stoppingToken)
    {
        Type? eventType;
        IIntegrationEvent? integrationEvent;

        try
        {
            // T22.3：使用 IOutboxEventTypeResolver 按 FullName 解析，兼容 BC 版本升级
            eventType = _typeResolver.Resolve(message.Type);
            if (eventType is null)
            {
                _logger.LogError("无法解析发件箱事件类型 Type={Type}", message.Type);
                message.MarkAsFailed("事件类型无法解析", MaxRetryCount);
                await context.SaveChangesAsync(stoppingToken);
                return;
            }

            integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions) as IIntegrationEvent;
            if (integrationEvent is null)
            {
                message.MarkAsFailed("事件反序列化为 null", MaxRetryCount);
                await context.SaveChangesAsync(stoppingToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发件箱消息预处理失败 Id={MessageId}", message.Id);
            message.MarkAsFailed(ex.Message, MaxRetryCount);
            await context.SaveChangesAsync(stoppingToken);
            return;
        }

        // 阶段 1：事务内置 Publishing 中间态并提交
        message.MarkAsPublishing();
        try
        {
            await context.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发件箱消息置 Publishing 失败 Id={MessageId}", message.Id);
            // 提交失败说明状态未持久化，重置回 Pending 等待下次重试
            message.ResetStalePublishing();
            throw;
        }

        // 阶段 2：发布到 MQ（M4.2 起 Outbox 在消息头携带 schema-version，供消费方按版本路由 handler）
        try
        {
            var headers = new Dictionary<string, string?>
            {
                ["schema-version"] = message.SchemaVersion.ToString()
            };
            await _eventBus.PublishAsync(integrationEvent, headers, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发件箱消息发布失败 Id={MessageId}", message.Id);
            // 发布失败：回退 Pending，递增重试计数
            message.MarkAsFailed(ex.Message, MaxRetryCount);
            try
            {
                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception commitEx)
            {
                _logger.LogError(commitEx, "发件箱发布失败回退提交失败 Id={MessageId}", message.Id);
            }
            return;
        }

        // 阶段 3：置 Processed 并提交
        message.MarkAsProcessed();
        try
        {
            await context.SaveChangesAsync(stoppingToken);
            // M5.3：记录成功发布计数（按 BC 维度，使用 DbContext 类型名作为标签）
            OutboxMetrics.RecordPublished(typeof(TDbContext).Name);
            _logger.LogInformation("发件箱消息已发布 Id={MessageId} Type={Type}", message.Id, eventType.Name);
        }
        catch (Exception ex)
        {
            // 发布成功但标记 Processed 失败：依赖下游幂等性，由 RecoverStalePublishingAsync 兜底
            _logger.LogWarning(ex,
                "发件箱消息发布成功但 Processed 标记失败 Id={MessageId}，将由 Publishing 超时扫描回退 Pending，依赖下游幂等兜底",
                message.Id);
            // 注意：不在此抛出，避免上层循环中断；消息保留 Publishing 状态等待 RecoverStalePublishingAsync 处理
            // ChangeTracker 中残留的修改状态由下一次 SaveChangesAsync 重置，此处不做手动清理以避免遮蔽真实异常
        }
    }

    /// <summary>
    /// 统计当前 pending 消息数量，超阈值记录结构化告警日志。
    /// 阈值默认 100，可由业务上下文覆盖（后续通过 <c>Outbox:PendingAlertThreshold</c> 配置）。
    /// 同时更新 Prometheus gauge <c>outbox_pending_count</c>（M5.3）。
    /// </summary>
    internal async Task AlertIfPendingBacklogAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var pendingCount = await context.Set<OutboxMessage>()
            .CountAsync(m => m.Status == OutboxMessageStatus.Pending, stoppingToken);

        // M5.3：暴露 outbox_pending_count 指标供 Prometheus 抓取
        OutboxMetrics.SetPendingCount(pendingCount);

        if (pendingCount > PendingAlertThreshold)
        {
            _logger.LogWarning(
                "发件箱积压告警：pending 消息数 {PendingCount} 超过阈值 {Threshold}，请检查下游消费速度或发布器健康状态",
                pendingCount, PendingAlertThreshold);
        }
    }
}

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
/// </summary>
/// <typeparam name="TDbContext">承载发件箱表的 DbContext 类型。</typeparam>
public class OutboxPublisher<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxPublisher<TDbContext>> _logger;

    private const int BatchSize = 50;
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    /// <summary>Publishing 状态超时阈值，超过此时间认为发布中断（应用重启/标记失败），回退 Pending。</summary>
    private static readonly TimeSpan PublishingStaleTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OutboxPublisher(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILogger<OutboxPublisher<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
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
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var pendingMessages = await context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            await PublishSingleAsync(context, message, stoppingToken);
        }
    }

    /// <summary>
    /// 测试入口：处理一批 Pending 消息。生产代码由 <see cref="ExecuteAsync"/> 调用。
    /// </summary>
    internal Task ProcessBatchForTestAsync(CancellationToken ct) => ProcessBatchAsync(ct);

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
            eventType = Type.GetType(message.Type);
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

        // 阶段 2：发布到 MQ
        try
        {
            await _eventBus.PublishAsync(integrationEvent, stoppingToken);
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
}

using System.Text.Json;
using Leno.Infrastructure.Outbox;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 发件箱后台发布器，轮询发件箱表，将待发布消息发布到事件总线，
/// 成功标记已处理，失败按重试次数递增，超阈值进入死信状态。
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

        await context.SaveChangesAsync(stoppingToken);
    }

    private async Task PublishSingleAsync(TDbContext context, OutboxMessage message, CancellationToken stoppingToken)
    {
        try
        {
            var eventType = Type.GetType(message.Type);
            if (eventType is null)
            {
                _logger.LogError("无法解析发件箱事件类型 Type={Type}", message.Type);
                message.MarkAsFailed("事件类型无法解析", MaxRetryCount);
                return;
            }

            var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions) as IIntegrationEvent;
            if (integrationEvent is null)
            {
                message.MarkAsFailed("事件反序列化为 null", MaxRetryCount);
                return;
            }

            await _eventBus.PublishAsync(integrationEvent, stoppingToken);
            message.MarkAsProcessed();

            _logger.LogInformation("发件箱消息已发布 Id={MessageId} Type={Type}", message.Id, eventType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发件箱消息发布失败 Id={MessageId}", message.Id);
            message.MarkAsFailed(ex.Message, MaxRetryCount);
        }
    }
}

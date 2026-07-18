using Leno.Infrastructure.EventBus;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Promotion.Infrastructure.Consumers;

/// <summary>
/// 秒杀订单创建失败事件消费者，回退 Redis 库存与 DB 基线。
/// 通过 EventId 幂等去重（Redis 24h）。
/// 消费订单域发布的 SeckillOrderCreationFailedIntegrationEvent 集成事件。
/// </summary>
public sealed class SeckillOrderCreationFailedEventConsumer : IntegrationEventConsumerBase<SeckillOrderCreationFailedIntegrationEvent>
{
    private readonly ISeckillActivityRepository _activityRepository;
    private readonly ISeckillStockService _stockService;
    private readonly ISeckillPreOccupationRecordRepository _preOccupationRecordRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeckillOrderCreationFailedEventConsumer(
        ISeckillActivityRepository activityRepository,
        ISeckillStockService stockService,
        ISeckillPreOccupationRecordRepository preOccupationRecordRepository,
        IUnitOfWork unitOfWork,
        ILogger<SeckillOrderCreationFailedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(activityRepository);
        ArgumentNullException.ThrowIfNull(stockService);
        ArgumentNullException.ThrowIfNull(preOccupationRecordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _activityRepository = activityRepository;
        _stockService = stockService;
        _preOccupationRecordRepository = preOccupationRecordRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(SeckillOrderCreationFailedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 标记预占记录为已回退
        var record = await _preOccupationRecordRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (record is not null && !record.IsRolledBack)
        {
            record.MarkRolledBack();
        }

        // 回退 Redis 库存
        await _stockService.RestoreAsync(integrationEvent.ActivityId, integrationEvent.SkuId, integrationEvent.Quantity, ct);

        // 回退 DB 基线库存
        var activity = await _activityRepository.GetByIdAsync(integrationEvent.ActivityId, ct);
        if (activity is not null)
        {
            activity.RestoreStock(integrationEvent.Quantity);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation(
            "秒杀订单创建失败回退完成 OrderId={OrderId} ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity} Reason={Reason}",
            integrationEvent.OrderId, integrationEvent.ActivityId, integrationEvent.SkuId, integrationEvent.Quantity, integrationEvent.Reason);
    }
}

/// <summary>
/// 秒杀订单确认事件消费者，标记预占记录为已履约。
/// 通过 EventId 幂等去重（Redis 24h）。
/// 消费订单域发布的 SeckillOrderConfirmedIntegrationEvent 集成事件。
/// </summary>
public sealed class SeckillOrderConfirmedEventConsumer : IntegrationEventConsumerBase<SeckillOrderConfirmedIntegrationEvent>
{
    private readonly ISeckillPreOccupationRecordRepository _preOccupationRecordRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeckillOrderConfirmedEventConsumer(
        ISeckillPreOccupationRecordRepository preOccupationRecordRepository,
        IUnitOfWork unitOfWork,
        ILogger<SeckillOrderConfirmedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(preOccupationRecordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _preOccupationRecordRepository = preOccupationRecordRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(SeckillOrderConfirmedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var record = await _preOccupationRecordRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (record is null)
        {
            Logger.LogInformation("未找到预占记录 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        if (record.IsFulfilled)
        {
            Logger.LogInformation("预占记录已履约 OrderId={OrderId}，跳过重复处理", integrationEvent.OrderId);
            return;
        }

        record.MarkFulfilled();
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("秒杀预占记录已履约 OrderId={OrderId} ActivityId={ActivityId}",
            integrationEvent.OrderId, integrationEvent.ActivityId);
    }
}

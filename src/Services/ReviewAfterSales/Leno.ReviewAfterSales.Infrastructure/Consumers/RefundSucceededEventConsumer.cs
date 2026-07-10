using Leno.Infrastructure.EventBus;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ReviewAfterSales.Infrastructure.Consumers;

/// <summary>
/// 退款完成事件消费者，将退款中的售后单标记为已完成。
/// 通过状态检查幂等：售后单不存在、AfterSalesId 为空、已 Completed 或非 Refunding 态时跳过。
/// </summary>
public sealed class RefundSucceededEventConsumer : RedisIntegrationEventConsumerBase<RefundCompletedEvent>
{
    private readonly IAfterSalesRepository _afterSalesRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundSucceededEventConsumer(
        IAfterSalesRepository afterSalesRepository,
        IUnitOfWork unitOfWork,
        ILogger<RefundSucceededEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(afterSalesRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _afterSalesRepository = afterSalesRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RefundCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.AfterSalesId == Guid.Empty)
        {
            Logger.LogInformation("退款完成事件未关联售后单 AfterSalesId 为空 OrderId={OrderId}，跳过",
                integrationEvent.OrderId);
            return;
        }

        var afterSales = await _afterSalesRepository.GetByIdAsync(integrationEvent.AfterSalesId, ct);
        if (afterSales is null)
        {
            Logger.LogInformation("退款完成事件：售后单不存在 AfterSalesId={AfterSalesId}，跳过",
                integrationEvent.AfterSalesId);
            return;
        }

        if (afterSales.Status == AfterSalesStatus.Completed)
        {
            Logger.LogInformation("退款完成事件：售后单 {AfterSalesId} 已完成，跳过重复消费",
                integrationEvent.AfterSalesId);
            return;
        }

        if (afterSales.Status != AfterSalesStatus.Refunding)
        {
            Logger.LogWarning("退款完成事件：售后单 {AfterSalesId} 当前状态 {Status} 非退款中，跳过",
                integrationEvent.AfterSalesId, afterSales.Status);
            return;
        }

        afterSales.MarkRefundCompleted(integrationEvent.RefundId, integrationEvent.RefundAmount, channelRefundNo: null);

        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("售后单 {AfterSalesId} 已标记退款完成 RefundId={RefundId} Amount={Amount}",
            integrationEvent.AfterSalesId, integrationEvent.RefundId, integrationEvent.RefundAmount);
    }
}

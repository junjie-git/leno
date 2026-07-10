using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.Consumers;

/// <summary>
/// 订单完成事件消费者：维护店铺当日运营指标的订单数与销售额。
/// 事件契约 OrderCompletedEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 指标按订单完成日期（UTC）聚合，不存在则零值初始化后增量记录。
/// </summary>
public sealed class OrderCompletedEventConsumer : IntegrationEventConsumerBase<OrderCompletedEvent>
{
    private readonly IShopMetricsRepository _metricsRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCompletedEventConsumer(
        IShopMetricsRepository metricsRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCompletedEventConsumer> logger)
        : base(logger)
    {
        _metricsRepository = metricsRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCompletedEvent integrationEvent, CancellationToken ct)
    {
        var completedAt = integrationEvent.CompletedAt == default
            ? integrationEvent.OccurredAt
            : integrationEvent.CompletedAt;
        var date = DateOnly.FromDateTime(completedAt);
        var currency = string.IsNullOrWhiteSpace(integrationEvent.Currency) ? "CNY" : integrationEvent.Currency;

        var metrics = await _metricsRepository.GetByShopIdAsync(integrationEvent.SellerId, date, ct);
        if (metrics is null)
        {
            metrics = ShopMetrics.Create(Guid.NewGuid(), integrationEvent.SellerId, date, currency);
            await _metricsRepository.UpsertAsync(metrics, ct);
        }

        var salesAmount = Money.Create(integrationEvent.TotalAmount, currency);
        metrics.RecordOrder(salesAmount);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}

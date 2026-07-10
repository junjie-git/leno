using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.Consumers;

/// <summary>
/// 评价提交事件消费者：维护店铺当日运营指标的平均评分。
/// 事件契约 ReviewSubmittedEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 指标按评价发生日期（UTC）聚合，不存在则零值初始化后增量记录评分。
/// </summary>
public sealed class ReviewSubmittedEventConsumer : IntegrationEventConsumerBase<ReviewSubmittedEvent>
{
    private readonly IShopMetricsRepository _metricsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private const string DefaultCurrency = "CNY";

    public ReviewSubmittedEventConsumer(
        IShopMetricsRepository metricsRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewSubmittedEventConsumer> logger)
        : base(logger)
    {
        _metricsRepository = metricsRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(integrationEvent.OccurredAt);

        var metrics = await _metricsRepository.GetByShopIdAsync(integrationEvent.SellerId, date, ct);
        if (metrics is null)
        {
            metrics = ShopMetrics.Create(Guid.NewGuid(), integrationEvent.SellerId, date, DefaultCurrency);
            await _metricsRepository.UpsertAsync(metrics, ct);
        }

        metrics.RecordRating(integrationEvent.Rating);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}

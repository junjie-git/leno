using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SellerShop.Infrastructure.Consumers;

/// <summary>
/// 订单完成事件消费者：维护店铺当日运营指标的订单数与销售额，同时更新店铺经营数据。
/// 事件契约 OrderCompletedEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 指标按订单完成日期（UTC）聚合，不存在则零值初始化后增量记录。
/// </summary>
public sealed class OrderCompletedEventConsumer : RedisIntegrationEventConsumerBase<OrderCompletedEvent>
{
    private readonly IShopMetricsRepository _metricsRepository;
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCompletedEventConsumer(
        IShopMetricsRepository metricsRepository,
        IShopDashboardRepository dashboardRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCompletedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _metricsRepository = metricsRepository;
        _dashboardRepository = dashboardRepository;
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

        // 更新店铺经营数据：已完成订单 +1，待处理订单 -1
        var dashboard = await GetOrCreateDashboardAsync(integrationEvent.SellerId, ct);
        dashboard.OnOrderCompleted();
        await _dashboardRepository.UpdateAsync(dashboard, ct);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<ShopDashboardData> GetOrCreateDashboardAsync(Guid shopId, CancellationToken ct)
    {
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);
        if (dashboard is null)
        {
            dashboard = ShopDashboardData.Create(shopId);
            await _dashboardRepository.AddAsync(dashboard, ct);
        }

        return dashboard;
    }
}

/// <summary>
/// 订单创建事件消费者：维护店铺经营数据的总订单数与待处理订单数。
/// 事件契约 OrderCreatedEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 幂等：经 RedisIntegrationEventConsumerBase 以 EventId 去重。
/// </summary>
public sealed class OrderCreatedEventConsumer : RedisIntegrationEventConsumerBase<OrderCreatedEvent>
{
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCreatedEventConsumer(
        IShopDashboardRepository dashboardRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCreatedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _dashboardRepository = dashboardRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCreatedEvent integrationEvent, CancellationToken ct)
    {
        var dashboard = await GetOrCreateDashboardAsync(integrationEvent.SellerId, ct);
        dashboard.OnOrderCreated();
        await _dashboardRepository.UpdateAsync(dashboard, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单创建事件已处理 ShopId={ShopId} OrderId={OrderId} TotalOrders={TotalOrders}",
            integrationEvent.SellerId, integrationEvent.OrderId, dashboard.TotalOrders);
    }

    private async Task<ShopDashboardData> GetOrCreateDashboardAsync(Guid shopId, CancellationToken ct)
    {
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);
        if (dashboard is null)
        {
            dashboard = ShopDashboardData.Create(shopId);
            await _dashboardRepository.AddAsync(dashboard, ct);
        }

        return dashboard;
    }
}

/// <summary>
/// 订单支付成功事件消费者：维护店铺经营数据的累计收入。
/// 事件契约 OrderPaidEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 幂等：经 RedisIntegrationEventConsumerBase 以 EventId 去重。
/// </summary>
public sealed class OrderPaidEventConsumer : RedisIntegrationEventConsumerBase<OrderPaidEvent>
{
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderPaidEventConsumer(
        IShopDashboardRepository dashboardRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderPaidEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _dashboardRepository = dashboardRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderPaidEvent integrationEvent, CancellationToken ct)
    {
        var dashboard = await GetOrCreateDashboardAsync(integrationEvent.SellerId, ct);
        dashboard.OnOrderPaid(integrationEvent.Amount);
        await _dashboardRepository.UpdateAsync(dashboard, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单支付成功事件已处理 ShopId={ShopId} OrderId={OrderId} Amount={Amount} TotalRevenue={TotalRevenue}",
            integrationEvent.SellerId, integrationEvent.OrderId, integrationEvent.Amount, dashboard.TotalRevenue);
    }

    private async Task<ShopDashboardData> GetOrCreateDashboardAsync(Guid shopId, CancellationToken ct)
    {
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);
        if (dashboard is null)
        {
            dashboard = ShopDashboardData.Create(shopId);
            await _dashboardRepository.AddAsync(dashboard, ct);
        }

        return dashboard;
    }
}

/// <summary>
/// 订单取消事件消费者：维护店铺经营数据的待处理订单数。
/// 事件契约 OrderCancelledEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 幂等：经 RedisIntegrationEventConsumerBase 以 EventId 去重；OnOrderCancelled 已防负。
/// </summary>
public sealed class OrderCancelledEventConsumer : RedisIntegrationEventConsumerBase<OrderCancelledEvent>
{
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCancelledEventConsumer(
        IShopDashboardRepository dashboardRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCancelledEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _dashboardRepository = dashboardRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCancelledEvent integrationEvent, CancellationToken ct)
    {
        var dashboard = await GetOrCreateDashboardAsync(integrationEvent.SellerId, ct);
        dashboard.OnOrderCancelled();
        await _dashboardRepository.UpdateAsync(dashboard, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单取消事件已处理 ShopId={ShopId} OrderId={OrderId} PendingOrders={PendingOrders}",
            integrationEvent.SellerId, integrationEvent.OrderId, dashboard.PendingOrders);
    }

    private async Task<ShopDashboardData> GetOrCreateDashboardAsync(Guid shopId, CancellationToken ct)
    {
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);
        if (dashboard is null)
        {
            dashboard = ShopDashboardData.Create(shopId);
            await _dashboardRepository.AddAsync(dashboard, ct);
        }

        return dashboard;
    }
}
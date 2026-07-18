using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 订单创建事件触发的店铺工作台读模型同步消费者：消费 <see cref="OrderCreatedEvent"/>，
/// 调用 <see cref="IShopDashboardReadModelBuilder"/> 重建 <see cref="ShopDashboardReadModel"/>
/// 并通过 IndexAsync 覆盖更新到 Elasticsearch（不删除）。
/// 事件契约 <c>OrderCreatedEvent.SellerId</c> 语义等同卖家与店铺管理域的 ShopId。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列；店铺不存在时跳过同步。
/// 幂等：ES 索引以店铺标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class OrderCreatedShopDashboardSyncConsumer
    : ReadModelSyncConsumerBase<OrderCreatedEvent, ShopDashboardReadModel>
{
    private readonly IShopDashboardReadModelBuilder _builder;

    public OrderCreatedShopDashboardSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        IShopDashboardReadModelBuilder builder,
        ILogger<OrderCreatedShopDashboardSyncConsumer> logger)
        : base(repository, logger)
    {
        _builder = builder;
    }

    /// <inheritdoc />
    /// <remarks>订单创建事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> BuildReadModelAsync(
        OrderCreatedEvent integrationEvent, CancellationToken ct)
    {
        // OrderCreatedEvent.SellerId 语义等同 SellerShop BC 的 ShopId（参见现有 OrderEventConsumer 契约说明）
        var shopId = integrationEvent.SellerId;
        var readModel = await _builder.BuildAsync(shopId, ct);
        if (readModel is null)
        {
            Logger.LogWarning("订单创建事件触发的工作台读模型构建为空 ShopId={ShopId} OrderId={OrderId}",
                shopId, integrationEvent.OrderId);
            return (string.Empty, string.Empty, null);
        }

        return (shopId.ToString(), ShopDashboardReadModel.ShopDashboardIndexName, readModel);
    }

    /// <inheritdoc />
    /// <remarks>订单创建事件仅触发索引重建，不删除读模型。</remarks>
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        OrderCreatedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
}

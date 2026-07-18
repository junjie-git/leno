using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 评价提交事件触发的店铺工作台读模型同步消费者：消费 <see cref="ReviewSubmittedEvent"/>，
/// 调用 <see cref="IShopDashboardReadModelBuilder"/> 重建 <see cref="ShopDashboardReadModel"/>
/// 并通过 IndexAsync 覆盖更新到 Elasticsearch（不删除）。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列；店铺不存在时跳过同步。
/// 幂等：ES 索引以店铺标识为 _id，重复索引为覆盖更新。
/// </summary>
/// <remarks>
/// 事件契约限制说明：<see cref="ReviewSubmittedEvent"/> 仅含 <c>SpuId</c>（商品 SPU 标识），
/// 不直接携带 <c>ShopId</c>/<c>SellerId</c>。SellerShop BC 当前不持有 SpuId→ShopId 映射仓储，
/// 因此本消费者将 <c>SpuId</c> 作为 ShopId 传入 <see cref="IShopDashboardReadModelBuilder.BuildAsync"/>，
/// builder 在 <see cref="ShopDashboardReadModelBuilder"/> 中按 ShopId 查询店铺聚合；未匹配时返回 null，跳过同步。
/// 待后续接通 SpuId→ShopId 解析（跨 BC 查询或事件字段扩展）后，可直接替换此处传入的标识。
/// </remarks>
public sealed class ReviewSubmittedShopDashboardSyncConsumer
    : ReadModelSyncConsumerBase<ReviewSubmittedEvent, ShopDashboardReadModel>
{
    private readonly IShopDashboardReadModelBuilder _builder;

    public ReviewSubmittedShopDashboardSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        IShopDashboardReadModelBuilder builder,
        ILogger<ReviewSubmittedShopDashboardSyncConsumer> logger)
        : base(repository, logger)
    {
        _builder = builder;
    }

    /// <inheritdoc />
    /// <remarks>评价提交事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> BuildReadModelAsync(
        ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        // ReviewSubmittedEvent 无 ShopId/SellerId 字段，暂以 SpuId 作为 builder 入参。
        // 见类注释 remarks 了解限制与后续接通 SpuId→ShopId 解析的计划。
        var shopId = integrationEvent.SpuId;
        var readModel = await _builder.BuildAsync(shopId, ct);
        if (readModel is null)
        {
            Logger.LogWarning("评价提交事件触发的工作台读模型构建为空 SpuId={SpuId} ReviewId={ReviewId}",
                integrationEvent.SpuId, integrationEvent.ReviewId);
            return (string.Empty, string.Empty, null);
        }

        return (shopId.ToString(), ShopDashboardReadModel.ShopDashboardIndexName, readModel);
    }

    /// <inheritdoc />
    /// <remarks>评价提交事件仅触发索引重建，不删除读模型。</remarks>
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        ReviewSubmittedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
}

using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Application.Services;
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
/// 事件契约优先读取 <c>ReviewSubmittedEvent.ShopId</c>（由评价域发布时填充）；
/// 为 <c>Guid.Empty</c> 时（旧版发布方未填充），通过
/// <see cref="IProductAntiCorruptionService.GetSpuSellerIdAsync"/> 反查 SPU 归属卖家（即 ShopId）。
/// 反查仍失败时记 Warning 跳过同步，避免静默失败。
/// </remarks>
public sealed class ReviewSubmittedShopDashboardSyncConsumer
    : ReadModelSyncConsumerBase<ReviewSubmittedEvent, ShopDashboardReadModel>
{
    private readonly IShopDashboardReadModelBuilder _builder;
    private readonly IProductAntiCorruptionService? _productAntiCorruption;

    /// <summary>
    /// 生产环境构造函数：注入 <see cref="IProductAntiCorruptionService"/> 用于 ShopId 缺失时反查 SPU 归属卖家。
    /// </summary>
    public ReviewSubmittedShopDashboardSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        IShopDashboardReadModelBuilder builder,
        IProductAntiCorruptionService productAntiCorruption,
        ILogger<ReviewSubmittedShopDashboardSyncConsumer> logger)
        : base(repository, logger)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _productAntiCorruption = productAntiCorruption ?? throw new ArgumentNullException(nameof(productAntiCorruption));
    }

    /// <summary>
    /// 兼容构造函数（不注入防腐层）：当 ShopId 为空时退回旧行为，以 SpuId 作为 builder 入参。
    /// 仅供单元测试与历史调用方使用；生产环境请使用 4 参数构造函数注入 <see cref="IProductAntiCorruptionService"/>。
    /// </summary>
    public ReviewSubmittedShopDashboardSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        IShopDashboardReadModelBuilder builder,
        ILogger<ReviewSubmittedShopDashboardSyncConsumer> logger)
        : base(repository, logger)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _productAntiCorruption = null;
    }

    /// <inheritdoc />
    /// <remarks>评价提交事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> BuildReadModelAsync(
        ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        var shopId = integrationEvent.ShopId;

        // 旧版发布方未填充 ShopId 时，通过防腐层反查 SPU 归属卖家
        if (shopId == Guid.Empty)
        {
            if (_productAntiCorruption is null)
            {
                // 兼容路径：未注入防腐层（如单元测试或老调用方），退回旧行为：以 SpuId 作为 builder 入参
                Logger.LogWarning(
                    "评价提交事件 ShopId 为空且未注入防腐层，回退以 SpuId 作为 ShopId 入参 SpuId={SpuId} ReviewId={ReviewId}",
                    integrationEvent.SpuId, integrationEvent.ReviewId);
                shopId = integrationEvent.SpuId;
            }
            else
            {
                var sellerId = await _productAntiCorruption.GetSpuSellerIdAsync(integrationEvent.SpuId, ct)
                    .ConfigureAwait(false);
                if (sellerId.HasValue)
                {
                    shopId = sellerId.Value;
                }
                else
                {
                    Logger.LogWarning(
                        "评价提交事件无法解析 ShopId：SpuId={SpuId} ReviewId={ReviewId}，防腐层反查返回 null，跳过同步",
                        integrationEvent.SpuId, integrationEvent.ReviewId);
                    return (string.Empty, string.Empty, null);
                }
            }
        }

        var readModel = await _builder.BuildAsync(shopId, ct).ConfigureAwait(false);
        if (readModel is null)
        {
            Logger.LogWarning("评价提交事件触发的工作台读模型构建为空 ShopId={ShopId} ReviewId={ReviewId}",
                shopId, integrationEvent.ReviewId);
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

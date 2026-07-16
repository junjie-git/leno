using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.SellerShop.Infrastructure.Consumers;

/// <summary>
/// 商品上架事件消费者：维护店铺在售商品数 +1。
/// 事件契约 ProductPublishedEvent.SellerId 语义等同卖家与店铺管理域的 ShopId。
/// 幂等：经 <see cref="IntegrationEventConsumerBase{T}"/> 以 EventId 去重；DecrementProductCount 已防负。
/// </summary>
public sealed class ProductPublishedEventConsumer : IntegrationEventConsumerBase<ProductPublishedEvent>
{
    private readonly IShopRepository _shopRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductPublishedEventConsumer(
        IShopRepository shopRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProductPublishedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _shopRepository = shopRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductPublishedEvent integrationEvent, CancellationToken ct)
    {
        var shop = await _shopRepository.GetByIdAsync(integrationEvent.SellerId, ct);
        if (shop is null)
        {
            Logger.LogWarning("店铺不存在，跳过商品上架计数 ShopId={ShopId}", integrationEvent.SellerId);
            return;
        }

        shop.IncrementProductCount();
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}

/// <summary>
/// 商品下架事件消费者：维护店铺在售商品数 -1。
/// 幂等：DecrementProductCount 在商品数已为 0 时直接返回，重复消费不会产生负数。
/// </summary>
public sealed class ProductTakenDownEventConsumer : IntegrationEventConsumerBase<ProductTakenDownEvent>
{
    private readonly IShopRepository _shopRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductTakenDownEventConsumer(
        IShopRepository shopRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProductTakenDownEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _shopRepository = shopRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductTakenDownEvent integrationEvent, CancellationToken ct)
    {
        var shop = await _shopRepository.GetByIdAsync(integrationEvent.SellerId, ct);
        if (shop is null)
        {
            Logger.LogWarning("店铺不存在，跳过商品下架计数 ShopId={ShopId}", integrationEvent.SellerId);
            return;
        }

        shop.DecrementProductCount();
        await _shopRepository.UpdateAsync(shop, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }
}

using Leno.Infrastructure.EventBus;
using Leno.Product.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Product.Infrastructure.Consumers;

/// <summary>
/// 店铺暂停事件消费者：将店铺下全部在售商品置为不可售（SuspendedByShop）。
/// 幂等：SuspendByShop 仅在 OnSale 态流转，重复消费无副作用。
/// </summary>
public sealed class ShopSuspendedEventConsumer : RedisIntegrationEventConsumerBase<ShopSuspendedEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ShopSuspendedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopSuspendedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopSuspendedEvent integrationEvent, CancellationToken ct)
    {
        var products = await _spuRepository.GetByShopIdAsync(integrationEvent.ShopId, ct);
        foreach (var spu in products)
        {
            spu.SuspendByShop();
            await _spuRepository.UpdateAsync(spu, ct);
        }

        if (products.Count != 0)
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
    }
}

/// <summary>
/// 店铺恢复事件消费者：恢复店铺下被暂停的商品至在售态。
/// 幂等：ResumeByShop 仅在店铺暂停标记位为 true 时恢复，重复消费无副作用。
/// </summary>
public sealed class ShopResumedEventConsumer : RedisIntegrationEventConsumerBase<ShopResumedEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ShopResumedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopResumedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopResumedEvent integrationEvent, CancellationToken ct)
    {
        var products = await _spuRepository.GetByShopIdAsync(integrationEvent.ShopId, ct);
        foreach (var spu in products)
        {
            spu.ResumeByShop();
            await _spuRepository.UpdateAsync(spu, ct);
        }

        if (products.Count != 0)
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
    }
}

/// <summary>
/// 店铺关闭事件消费者：下架店铺下全部在售商品并发布 <see cref="ProductTakenDownEvent"/>。
/// 幂等：TakeDownForShopClosure 仅在 OnSale 态流转并发布下架事件，重复消费因状态已变更而无副作用。
/// </summary>
public sealed class ShopClosedEventConsumer : RedisIntegrationEventConsumerBase<ShopClosedEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ShopClosedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopClosedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopClosedEvent integrationEvent, CancellationToken ct)
    {
        var products = await _spuRepository.GetByShopIdAsync(integrationEvent.ShopId, ct);
        var reason = "店铺关闭，自动下架";
        foreach (var spu in products)
        {
            spu.TakeDownForShopClosure(reason);
            await _spuRepository.UpdateAsync(spu, ct);
        }

        if (products.Count != 0)
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
    }
}

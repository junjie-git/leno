using Leno.Infrastructure.EventBus;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Product.Infrastructure.Consumers;

/// <summary>
/// 店铺事件消费者基类，提供分页批量处理与幂等消费能力。
/// 通过 EventId + Redis SET NX 实现幂等去重，分页处理避免大事务。
/// </summary>
public abstract class ShopEventConsumerBase<TEvent> : RedisIntegrationEventConsumerBase<TEvent>
    where TEvent : class, Leno.SharedContracts.Events.IIntegrationEvent
{
    private const int BatchSize = 100;

    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    protected ShopEventConsumerBase(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 分页加载店铺商品并执行指定操作。
    /// 每批 BatchSize 条，每批独立提交事务，避免大事务锁表。
    /// </summary>
    protected async Task ProcessBatchAsync(
        Guid shopId,
        ProductStatus? statusFilter,
        Func<SPU, Task> operation,
        CancellationToken ct)
    {
        int page = 1;
        int processedCount = 0;
        bool hasMore;
        do
        {
            var (items, total) = await _spuRepository.QueryAsync(
                shopId: shopId,
                status: statusFilter,
                page: page,
                pageSize: BatchSize,
                ct: ct);

            foreach (var spu in items)
            {
                await operation(spu);
                await _spuRepository.UpdateAsync(spu, ct);
            }

            if (items.Count > 0)
            {
                await _unitOfWork.SaveEntitiesAsync(ct);
            }

            processedCount += items.Count;
            page++;
            hasMore = processedCount < total;
        } while (hasMore);
    }
}

/// <summary>
/// 店铺暂停事件消费者：将店铺下全部在售商品置为不可售（SuspendedByShop）。
/// 分页处理避免大事务；幂等：SuspendByShop 仅在 OnSale 态流转，重复消费无副作用。
/// </summary>
public sealed class ShopSuspendedEventConsumer : ShopEventConsumerBase<ShopSuspendedEvent>
{
    public ShopSuspendedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopSuspendedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(spuRepository, unitOfWork, logger, redisMultiplexer)
    {
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopSuspendedEvent integrationEvent, CancellationToken ct)
    {
        await ProcessBatchAsync(
            integrationEvent.ShopId,
            ProductStatus.OnSale,
            spu =>
            {
                spu.SuspendByShop();
                return Task.CompletedTask;
            },
            ct);
    }
}

/// <summary>
/// 店铺恢复事件消费者：恢复店铺下被暂停的商品至在售态。
/// 分页处理避免大事务；幂等：ResumeByShop 仅在店铺暂停标记位为 true 时恢复，重复消费无副作用。
/// </summary>
public sealed class ShopResumedEventConsumer : ShopEventConsumerBase<ShopResumedEvent>
{
    public ShopResumedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopResumedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(spuRepository, unitOfWork, logger, redisMultiplexer)
    {
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopResumedEvent integrationEvent, CancellationToken ct)
    {
        // 查询所有 ShopSuspended 商品，ResumeByShop 仅对 SuspendedByShop=true 的生效
        await ProcessBatchAsync(
            integrationEvent.ShopId,
            ProductStatus.ShopSuspended,
            spu =>
            {
                spu.ResumeByShop();
                return Task.CompletedTask;
            },
            ct);
    }
}

/// <summary>
/// 店铺关闭事件消费者：下架店铺下全部在售商品并发布 <see cref="ProductTakenDownEvent"/>。
/// 分页处理避免大事务；幂等：TakeDownForShopClosure 仅在 OnSale 态流转并发布下架事件，重复消费因状态已变更而无副作用。
/// </summary>
public sealed class ShopClosedEventConsumer : ShopEventConsumerBase<ShopClosedEvent>
{
    private const string ShopClosureReason = "店铺关闭，自动下架";

    public ShopClosedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShopClosedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(spuRepository, unitOfWork, logger, redisMultiplexer)
    {
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ShopClosedEvent integrationEvent, CancellationToken ct)
    {
        await ProcessBatchAsync(
            integrationEvent.ShopId,
            null, // 不限制状态，下架所有可下架商品
            spu =>
            {
                spu.TakeDownForShopClosure(ShopClosureReason);
                return Task.CompletedTask;
            },
            ct);
    }
}

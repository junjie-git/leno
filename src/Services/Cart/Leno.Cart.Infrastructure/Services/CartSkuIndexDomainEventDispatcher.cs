using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Services;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车-SKU 反向索引领域事件分发器。
/// 在 <c>IUnitOfWork.SaveEntitiesAsync</c> 落库前由 CartUnitOfWork 调用，
/// 遍历聚合收集的 <see cref="SkuAddedToCartEvent"/> / <see cref="SkuRemovedFromCartEvent"/>
/// 并调用 <see cref="ICartSkuIndexService"/> 维护 Redis Set 反向索引，
/// 与聚合状态变更保持顺序一致（索引先于 DB 事务提交）。
/// </summary>
public sealed class CartSkuIndexDomainEventDispatcher
{
    private readonly ICartSkuIndexService _indexService;

    public CartSkuIndexDomainEventDispatcher(ICartSkuIndexService indexService)
    {
        ArgumentNullException.ThrowIfNull(indexService);
        _indexService = indexService;
    }

    /// <summary>
    /// 按顺序分发购物车域事件到反向索引服务。
    /// 索引服务异常上抛，由调用方决定是否中断事务（默认应中断以避免索引与聚合状态不一致）。
    /// </summary>
    /// <param name="domainEvents">本次保存变更中收集到的所有领域事件（含非 SKU 索引事件，会被忽略）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task DispatchAsync(IReadOnlyList<object> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case SkuAddedToCartEvent added:
                    await _indexService.AddAsync(added.SkuId, added.CartId, ct);
                    break;
                case SkuRemovedFromCartEvent removed:
                    await _indexService.RemoveAsync(removed.SkuId, removed.CartId, ct);
                    break;
            }
        }
    }
}

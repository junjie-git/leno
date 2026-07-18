using Leno.Cart.Domain.Events;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;

namespace Leno.Cart.Infrastructure.EventBus;

/// <summary>
/// Cart BC 领域事件到集成事件的翻译器。
/// 将 Cart 聚合收集的领域事件翻译为 SharedContracts 中的集成事件，经发件箱对外发布。
/// </summary>
public class CartIntegrationEventMapper : IntegrationEventMapperBase
{
    public CartIntegrationEventMapper()
    {
        // CartMergedDomainEvent → CartMergedEvent（数据分析域用户行为追踪、消息通知域可选）
        RegisterHandler<CartMergedDomainEvent, CartMergedEvent>(e =>
            new CartMergedEvent(e.UserId, e.AnonymousId, e.MergedItemCount));

        // SkuAddedToCartEvent/SkuRemovedFromCartEvent 为上下文内部领域事件，仅维护购物车-SKU 反向索引，
        // 不映射为集成事件，保持内部处理。
    }
}

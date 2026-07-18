using Leno.Infrastructure.EventBus;
using Leno.Product.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Product.Infrastructure.EventBus;

/// <summary>
/// Product BC 领域事件到集成事件的翻译器。
/// 将 SPU/StockBaseline 聚合收集的领域事件翻译为 SharedContracts 中的集成事件。
/// </summary>
public class ProductIntegrationEventMapper : IntegrationEventMapperBase
{
    public ProductIntegrationEventMapper()
    {
        // ProductPublishedDomainEvent → ProductPublishedEvent（卖家域消费维护店铺商品数 +1）
        RegisterHandler<ProductPublishedDomainEvent, ProductPublishedEvent>(e =>
            new ProductPublishedEvent(e.ProductId, e.SellerId));

        // ProductTakenDownDomainEvent → ProductTakenDownEvent（卖家域消费维护店铺商品数 -1）
        RegisterHandler<ProductTakenDownDomainEvent, ProductTakenDownEvent>(e =>
            new ProductTakenDownEvent(e.ProductId, e.SellerId));

        // StockAdjustedDomainEvent → StockAdjustedEvent（订单域消费同步库存基线）
        RegisterHandler<StockAdjustedDomainEvent, StockAdjustedEvent>(e =>
            new StockAdjustedEvent(e.SkuId, e.ProductId, e.AvailableQty, e.Delta, e.AdjustedAt));

        // ProductUpdatedDomainEvent → ProductUpdatedEvent（购物车域刷新展示快照、搜索域同步 ES 读模型）
        RegisterHandler<ProductUpdatedDomainEvent, ProductUpdatedEvent>(e =>
            new ProductUpdatedEvent(e.ProductId, e.SellerId, e.Title, e.MainImageUrl));

        // ProductCreatedEvent/ProductReviewedEvent 为本地领域事件，无对应集成事件，不注册翻译。
    }
}

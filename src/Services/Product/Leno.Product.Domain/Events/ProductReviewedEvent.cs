using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Events;

/// <summary>
/// 商品审核结果本地领域事件，运营审核通过或驳回时由 SPU 聚合附加。
/// 非跨上下文事件，未列跨域消费方；仅实现 <see cref="IDomainEvent"/>。
/// </summary>
public sealed class ProductReviewedEvent : DomainEventBase
{
    /// <summary>商品标识。</summary>
    public Guid SpuId { get; }

    /// <summary>审核结果：通过或驳回。</summary>
    public ProductStatus Result { get; }

    /// <summary>审核人标识。</summary>
    public Guid ReviewedBy { get; }

    public ProductReviewedEvent(Guid spuId, ProductStatus result, Guid reviewedBy)
        : base(spuId)
    {
        SpuId = spuId;
        Result = result;
        ReviewedBy = reviewedBy;
    }
}

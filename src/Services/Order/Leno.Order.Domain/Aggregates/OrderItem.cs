using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Aggregates;

/// <summary>
/// 订单明细实体，隶属于 <see cref="Order"/> 聚合，记录单 SKU 的下单快照、单价、数量与分摊折扣。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>OrderItemId</c>。
/// </summary>
public sealed class OrderItem : Entity
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>商品快照，下单时固化，EF Core 作为 owned type 映射。</summary>
    public ProductSnapshot ProductSnapshot { get; private set; } = null!;

    /// <summary>成交单价，须 ≥ 0。</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>购买数量，须 &gt; 0。</summary>
    public int Quantity { get; private set; }

    /// <summary>本明细分摊的优惠金额，0 ≤ 分摊 ≤ <see cref="Subtotal"/>。</summary>
    public decimal DiscountAllocation { get; private set; }

    /// <summary>小计金额 = <see cref="UnitPrice"/> × <see cref="Quantity"/>。</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>来源购物车项标识，由购物车域清空已结算项使用，可为空（非购物车来源）。</summary>
    public Guid? SourceCartItemId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private OrderItem() { }

    private OrderItem(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验 SKU 非空、单价 ≥ 0、数量 &gt; 0，计算小计金额。
    /// </summary>
    /// <param name="id">明细标识，由应用层生成。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="snapshot">商品快照。</param>
    /// <param name="unitPrice">成交单价，须 ≥ 0。</param>
    /// <param name="quantity">购买数量，须 &gt; 0。</param>
    /// <param name="sourceCartItemId">来源购物车项标识，可为空。</param>
    internal static OrderItem Create(Guid id, Guid skuId, ProductSnapshot snapshot, decimal unitPrice, int quantity, Guid? sourceCartItemId)
    {
        if (skuId == Guid.Empty)
        {
            throw new OrderDomainException("SkuId 不可为空", "ORDER_ITEM_SKU_EMPTY");
        }

        if (snapshot is null)
        {
            throw new OrderDomainException("商品快照不可为空", "ORDER_ITEM_SNAPSHOT_EMPTY");
        }

        if (unitPrice < 0)
        {
            throw new OrderDomainException("成交单价不可为负", "ORDER_ITEM_PRICE_INVALID");
        }

        if (quantity <= 0)
        {
            throw new OrderDomainException("购买数量须大于 0", "ORDER_ITEM_QTY_INVALID");
        }

        return new OrderItem(id == Guid.Empty ? Guid.NewGuid() : id)
        {
            SkuId = skuId,
            ProductSnapshot = snapshot,
            UnitPrice = unitPrice,
            Quantity = quantity,
            DiscountAllocation = 0,
            Subtotal = unitPrice * quantity,
            SourceCartItemId = sourceCartItemId
        };
    }

    /// <summary>
    /// 分摊优惠金额，校验 0 ≤ 分摊 ≤ <see cref="Subtotal"/>，由订单聚合在 <see cref="Order.ApplyDiscount"/> 中调用。
    /// </summary>
    /// <param name="allocation">分摊金额。</param>
    internal void ApplyDiscount(decimal allocation)
    {
        if (allocation < 0 || allocation > Subtotal)
        {
            throw new OrderDomainException(
                $"优惠分摊金额非法：分摊 {allocation}，小计 {Subtotal}",
                "ORDER_ITEM_DISCOUNT_INVALID");
        }

        DiscountAllocation = allocation;
    }
}

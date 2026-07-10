using Leno.SharedKernel.Abstractions;

namespace Leno.Cart.Domain.Aggregates;

/// <summary>
/// 购物车项实体，表达买家选购的一个 SKU 行项。
/// 经聚合根 <see cref="Cart"/> 访问，不独立暴露仓储。
/// </summary>
public sealed class CartItem : Entity
{
    private const int MinQuantity = 1;
    private const int MaxQuantity = 99;

    /// <summary>所属购物车标识。</summary>
    public Guid CartId { get; private set; }

    /// <summary>商品 SKU 标识（引用商品域 SkuId）。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>所属卖家（店铺）标识，用于结算时按卖家分组。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>购买数量，1-99。</summary>
    public int Quantity { get; private set; }

    /// <summary>是否选中参与结算。</summary>
    public bool IsSelected { get; private set; }

    /// <summary>
    /// 结算来源购物车项标识，用于订单创建后清空已结算项。
    /// 默认与 <see cref="Entity.Id"/> 一致；订单创建时携带此标识列表。
    /// </summary>
    public Guid SourceCartItemId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private CartItem() { }

    internal CartItem(Guid id, Guid cartId, Guid skuId, Guid sellerId, int quantity) : base(id)
    {
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("SellerId 不可为空", nameof(sellerId));
        }

        CartId = cartId;
        SkuId = skuId;
        SellerId = sellerId;
        SetQuantity(quantity);
        IsSelected = true;
        SourceCartItemId = Id;
    }

    /// <summary>设置数量，校验 1-99 范围。</summary>
    internal void SetQuantity(int quantity)
    {
        if (quantity < MinQuantity || quantity > MaxQuantity)
        {
            throw new ArgumentException($"购买数量须在 {MinQuantity}-{MaxQuantity} 之间", nameof(quantity));
        }

        Quantity = quantity;
    }

    /// <summary>选中参与结算。</summary>
    internal void Select() => IsSelected = true;

    /// <summary>取消选中。</summary>
    internal void Deselect() => IsSelected = false;
}

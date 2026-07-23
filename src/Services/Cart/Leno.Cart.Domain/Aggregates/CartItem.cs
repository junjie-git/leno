using System.Text.Json.Serialization;
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

    /// <summary>是否有效（商品域事件驱动，下架时标记无效，上架时恢复）。</summary>
    public bool IsValid { get; private set; } = true;

    /// <summary>失效原因（商品下架时记录）。</summary>
    public string? InvalidReason { get; private set; }

    /// <summary>展示用商品标题（商品域信息更新时刷新）。</summary>
    public string DisplayTitle { get; private set; } = string.Empty;

    /// <summary>展示用主图 URL（商品域信息更新时刷新）。</summary>
    public string DisplayImageUrl { get; private set; } = string.Empty;

    /// <summary>
    /// EF Core 无参构造；同时作为 System.Text.Json 反序列化入口（P1-1 修复）。
    /// <para>
    /// P1-1：购物车聚合经 <c>Cart.Items</c> 集合随父聚合一起序列化到 Redis Hash，
    /// 本类存在两个构造函数（无参 + internal 带参），默认策略无法决定使用哪个，
    /// 故在此显式标注 <see cref="JsonConstructorAttribute"/> 指定无参构造。
    /// EF Core 物化行为不受影响。
    /// </para>
    /// </summary>
    [JsonConstructor]
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
        // IsValid 由字段初始化器 = true 初始化，此处不重复赋值
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

    /// <summary>标记为无效（商品下架事件驱动），记录原因。</summary>
    internal void MarkInvalid(string reason)
    {
        IsValid = false;
        InvalidReason = string.IsNullOrWhiteSpace(reason) ? "商品已下架" : reason;
    }

    /// <summary>标记为有效（商品重新上架事件驱动），清除失效原因。</summary>
    internal void MarkValid()
    {
        IsValid = true;
        InvalidReason = null;
    }

    /// <summary>刷新展示快照（商品信息更新事件驱动）。</summary>
    internal void RefreshDisplaySnapshot(string title, string mainImageUrl)
    {
        DisplayTitle = title ?? string.Empty;
        DisplayImageUrl = mainImageUrl ?? string.Empty;
    }
}

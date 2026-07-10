using Leno.Cart.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Cart.Domain.Aggregates;

/// <summary>
/// 购物车聚合根，管理买家选购商品行项集合，封装合并/数量/选中/清空等不变量。
/// 一个买家对应一辆购物车（UserId 唯一键）。
/// </summary>
public sealed class Cart : AggregateRoot
{
    private readonly List<CartItem> _items = new();

    /// <summary>所属买家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; private set; }

    /// <summary>购物车项集合，聚合内实体，仅经聚合根访问。</summary>
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    /// <summary>EF Core 无参构造。</summary>
    private Cart() { }

    private Cart(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，为买家初始化空购物车。
    /// </summary>
    /// <param name="cartId">购物车标识，由应用层生成。</param>
    /// <param name="userId">买家账号标识。</param>
    public static Cart Create(Guid cartId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        var cart = new Cart(cartId == Guid.Empty ? Guid.NewGuid() : cartId)
        {
            UserId = userId
        };

        return cart;
    }

    /// <summary>
    /// 添加购物车项，同 SKU 合并数量（校验上限 99），不同 SKU 新增。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="quantity">购买数量。</param>
    /// <param name="sellerId">所属卖家标识。</param>
    public void AddItem(Guid skuId, int quantity, Guid sellerId)
    {
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }

        var existing = _items.FirstOrDefault(i => i.SkuId == skuId);
        if (existing is not null)
        {
            // 合并数量，校验上限
            var merged = existing.Quantity + quantity;
            if (merged > 99)
            {
                throw new CartDomainException($"SKU {skuId} 合并后数量 {merged} 超过上限 99", "CART_QTY_OVERFLOW");
            }

            existing.SetQuantity(merged);
            return;
        }

        var item = new CartItem(Guid.NewGuid(), Id, skuId, sellerId, quantity);
        _items.Add(item);
    }

    /// <summary>
    /// 更新指定 SKU 的数量，校验 1-99。SKU 不存在抛出异常。
    /// </summary>
    public void UpdateItemQuantity(Guid skuId, int quantity)
    {
        var item = FindItem(skuId)
                   ?? throw new CartDomainException($"购物车中不存在 SKU {skuId}", "CART_ITEM_NOT_FOUND", 404);

        item.SetQuantity(quantity);
    }

    /// <summary>
    /// 移除指定 SKU 的购物车项。SKU 不存在抛出异常。
    /// </summary>
    public void RemoveItem(Guid skuId)
    {
        var item = FindItem(skuId)
                   ?? throw new CartDomainException($"购物车中不存在 SKU {skuId}", "CART_ITEM_NOT_FOUND", 404);

        _items.Remove(item);
    }

    /// <summary>
    /// 批量选中指定 SKU 项。不存在的 SKU 忽略。
    /// </summary>
    public void SelectItems(IEnumerable<Guid> skuIds)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        var set = new HashSet<Guid>(skuIds);
        foreach (var item in _items)
        {
            if (set.Contains(item.SkuId))
            {
                item.Select();
            }
        }
    }

    /// <summary>
    /// 批量取消选中指定 SKU 项。不存在的 SKU 忽略。
    /// </summary>
    public void DeselectItems(IEnumerable<Guid> skuIds)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        var set = new HashSet<Guid>(skuIds);
        foreach (var item in _items)
        {
            if (set.Contains(item.SkuId))
            {
                item.Deselect();
            }
        }
    }

    /// <summary>
    /// 清空已选中项（订单创建后调用，提取 SourceCartItemId 用于事件关联）。
    /// 返回被清除项的来源标识列表。
    /// </summary>
    public IReadOnlyList<Guid> ClearSelectedItems()
    {
        var selected = _items.Where(i => i.IsSelected).ToList();
        var sourceIds = selected.Select(i => i.SourceCartItemId).ToList();

        foreach (var item in selected)
        {
            _items.Remove(item);
        }

        return sourceIds;
    }

    /// <summary>
    /// 按来源购物车项标识列表清空对应项（订单创建事件携带 SourceCartItemIds 时调用）。
    /// 幂等：不存在的标识忽略。
    /// </summary>
    public void ClearItemsBySourceIds(IEnumerable<Guid> sourceCartItemIds)
    {
        ArgumentNullException.ThrowIfNull(sourceCartItemIds);
        var set = new HashSet<Guid>(sourceCartItemIds);
        var toRemove = _items.Where(i => set.Contains(i.SourceCartItemId)).ToList();
        foreach (var item in toRemove)
        {
            _items.Remove(item);
        }
    }

    private CartItem? FindItem(Guid skuId) => _items.FirstOrDefault(i => i.SkuId == skuId);
}

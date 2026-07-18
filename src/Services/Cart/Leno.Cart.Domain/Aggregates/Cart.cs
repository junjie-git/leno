using Leno.Cart.Domain.Events;
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
    /// 工厂方法，为匿名用户初始化空购物车（UserId 为 Guid.Empty）。
    /// </summary>
    /// <param name="cartId">购物车标识，由应用层生成。</param>
    public static Cart CreateAnonymous(Guid cartId)
    {
        var cart = new Cart(cartId == Guid.Empty ? Guid.NewGuid() : cartId)
        {
            UserId = Guid.Empty
        };

        return cart;
    }

    /// <summary>
    /// 添加购物车项，同 SKU 合并数量（校验上限 99），不同 SKU 新增。
    /// 新增 SKU 时发布 <see cref="SkuAddedToCartEvent"/> 供基础设施层维护反向索引。
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
        AddDomainEvent(new SkuAddedToCartEvent(Id, skuId));
    }

    /// <summary>
    /// 添加购物车项并设置展示快照（标题、主图），用于测试与初始化场景。
    /// 同 SKU 合并数量并刷新展示快照；不同 SKU 新增并设置展示快照。
    /// 注意：unitPrice 参数接受但不持久化，购物车项价格在查看时由价格防腐层实时查询。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="title">展示标题。</param>
    /// <param name="mainImageUrl">主图 URL。</param>
    /// <param name="unitPrice">单价（接受但不持久化，价格在查看时实时查询）。</param>
    /// <param name="quantity">购买数量。</param>
    /// <param name="sellerId">所属卖家标识。</param>
    public void AddItem(Guid skuId, string title, string mainImageUrl, decimal unitPrice, int quantity, Guid sellerId)
    {
        AddItem(skuId, quantity, sellerId);
        FindItem(skuId)?.RefreshDisplaySnapshot(title, mainImageUrl);
    }

    /// <summary>
    /// 更新指定 SKU 的数量，校验 1-99。SKU 不存在抛出异常。
    /// </summary>
    public void UpdateItemQuantity(Guid skuId, int quantity)
    {
        var item = FindItem(skuId)
                   ?? throw new CartDomainException($"购物车中不存在 SKU {skuId}", "CART_ITEM_NOT_FOUND");

        item.SetQuantity(quantity);
    }

    /// <summary>
    /// 移除指定 SKU 的购物车项。SKU 不存在抛出异常。
    /// 移除后发布 <see cref="SkuRemovedFromCartEvent"/> 供基础设施层维护反向索引。
    /// </summary>
    public void RemoveItem(Guid skuId)
    {
        var item = FindItem(skuId)
                   ?? throw new CartDomainException($"购物车中不存在 SKU {skuId}", "CART_ITEM_NOT_FOUND");

        _items.Remove(item);
        AddDomainEvent(new SkuRemovedFromCartEvent(Id, skuId));
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
    /// 全选/取消全选所有有效项。无效项（IsValid=false）不参与操作，保持未选中状态。
    /// 空购物车无副作用，直接返回。
    /// </summary>
    /// <param name="isSelected">true=全选，false=取消全选。</param>
    public void ToggleAllSelection(bool isSelected)
    {
        foreach (var item in _items)
        {
            if (item.IsValid)
            {
                if (isSelected)
                {
                    item.Select();
                }
                else
                {
                    item.Deselect();
                }
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

    /// <summary>
    /// 合并匿名购物车：遍历匿名购物车项，逐项调用 AddItem 合并数量或新增。
    /// 合并后校验：单 SKU 数量上限 99，品类上限 50。
    /// 选中状态：若任一来源选中则选中。
    /// 返回合并项数量。
    /// </summary>
    /// <param name="anonymousCart">匿名购物车聚合。</param>
    /// <returns>合并的购物车项数量（以匿名购物车项数计）。</returns>
    public int MergeFrom(Cart anonymousCart)
    {
        ArgumentNullException.ThrowIfNull(anonymousCart);
        const int maxVariety = 50;

        var mergedCount = 0;
        foreach (var item in anonymousCart.Items)
        {
            // 检查品类上限（新增项时）
            var existing = FindItem(item.SkuId);
            if (existing is null && _items.Count >= maxVariety)
            {
                throw new CartDomainException($"购物车品类数量已达上限 {maxVariety}", "CART_VARIETY_LIMIT");
            }

            AddItem(item.SkuId, item.Quantity, item.SellerId);

            // 选中状态：任一来源选中则选中
            if (item.IsSelected)
            {
                var merged = FindItem(item.SkuId);
                merged?.Select();
            }

            mergedCount++;
        }

        return mergedCount;
    }

    /// <summary>
    /// 记录匿名购物车合并完成的领域事件。
    /// 由应用层在调用 <see cref="MergeFrom"/> 后调用，聚合收集 <see cref="CartMergedDomainEvent"/>，
    /// 经 UnitOfWork 的发件箱与 <c>IIntegrationEventMapper</c> 翻译为
    /// <see cref="Leno.SharedContracts.Events.CartMergedEvent"/> 集成事件对外发布。
    /// </summary>
    /// <param name="anonymousId">匿名会话标识（合并前匿名购物车的 SessionId）。</param>
    /// <param name="mergedItemCount">合并的购物车项数量。</param>
    public void RecordMergedEvent(string anonymousId, int mergedItemCount)
    {
        if (string.IsNullOrWhiteSpace(anonymousId))
        {
            throw new ArgumentException("AnonymousId 不可为空", nameof(anonymousId));
        }

        AddDomainEvent(new CartMergedDomainEvent(Id, UserId, anonymousId, mergedItemCount));
    }

    /// <summary>
    /// 标记指定 SKU 的购物车项为无效（商品下架时调用），同时自动取消选中。
    /// 幂等：已标记无效的项重复标记无副作用。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="reason">失效原因。</param>
    public void MarkInvalid(Guid skuId, string reason)
    {
        var item = FindItem(skuId);
        if (item is null) return;

        item.MarkInvalid(reason);
        item.Deselect(); // 自动取消选中
    }

    /// <summary>
    /// 标记指定 SKU 的购物车项为有效（商品重新上架时调用），恢复可售性。
    /// 幂等：已标记有效的项重复标记无副作用。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    public void MarkValid(Guid skuId)
    {
        var item = FindItem(skuId);
        if (item is null) return;

        item.MarkValid();
    }

    /// <summary>
    /// 刷新指定 SKU 购物车项的展示快照（商品信息更新时调用）。
    /// 幂等：不存在的 SKU 忽略。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="title">商品标题。</param>
    /// <param name="mainImageUrl">主图 URL。</param>
    public void RefreshDisplaySnapshot(Guid skuId, string title, string mainImageUrl)
    {
        var item = FindItem(skuId);
        if (item is null) return;

        item.RefreshDisplaySnapshot(title, mainImageUrl);
    }

    private CartItem? FindItem(Guid skuId) => _items.FirstOrDefault(i => i.SkuId == skuId);
}

using System.Text.Json.Serialization;
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
    /// <summary>购物车品类数量上限（不同 SKU 数）。</summary>
    private const int MaxVariety = 50;

    private readonly List<CartItem> _items = new();

    /// <summary>所属买家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; private set; }

    /// <summary>购物车项集合，聚合内实体，仅经聚合根访问。</summary>
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    /// <summary>
    /// 匿名购物车 Redis CAS 乐观并发版本号（P1-1 修复）。
    /// <para>
    /// 仅用于 <c>RedisAnonymousCartRepository</c> 路径的 Compare-And-Swap 原子更新：
    /// 仓储加载时通过 <see cref="MarkLoaded"/> 设置为 Redis Hash 中存储的 version 字段；
    /// 保存时作为 expectedVersion 传入 Lua 脚本，保存成功后通过 <see cref="MarkSaved"/> 递增。
    /// </para>
    /// <para>
    /// EF Core 认证购物车路径不使用此字段（已在 <c>CartConfiguration</c> 中 Ignore），
    /// 认证路径的乐观并发由 SQL Server rowversion shadow property 保证。
    /// </para>
    /// <para>
    /// 默认值 0 表示新创建的购物车，尚未持久化到 Redis。
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// 仓储加载购物车后调用，将聚合的 Revision 同步为 Redis Hash 中持久化的版本号。
    /// </summary>
    /// <param name="loadedRevision">从 Redis Hash version 字段读取的当前版本号。</param>
    public void MarkLoaded(int loadedRevision)
    {
        if (loadedRevision < 0)
        {
            throw new ArgumentException("加载的版本号不可为负数", nameof(loadedRevision));
        }
        Revision = loadedRevision;
    }

    /// <summary>
    /// 仓储成功执行 CAS 保存后调用，将聚合的 Revision 递增为新版本号。
    /// </summary>
    /// <param name="newRevision">保存成功后的新版本号（expectedVersion + 1）。</param>
    public void MarkSaved(int newRevision)
    {
        if (newRevision <= Revision)
        {
            throw new ArgumentException(
                $"新版本号 {newRevision} 必须大于当前版本号 {Revision}", nameof(newRevision));
        }
        Revision = newRevision;
    }

    /// <summary>
    /// EF Core 无参构造；同时作为 System.Text.Json 反序列化入口（P1-1 修复）。
    /// <para>
    /// P1-1：匿名购物车通过 <c>RedisAnonymousCartRepository</c> 用 <c>System.Text.Json</c>
    /// 序列化/反序列化到 Redis Hash。本类存在两个构造函数（无参 + <c>Guid id</c>），
    /// 默认策略无法决定使用哪个，故在此显式标注 <see cref="JsonConstructorAttribute"/> 指定无参构造。
    /// </para>
    /// <para>
    /// EF Core 物化行为不受影响：EF Core 通过反射识别无参构造函数，不依赖此特性。
    /// </para>
    /// </summary>
    [JsonConstructor]
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

        if (TryGetItem(skuId, out var existing))
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

        // 新增 SKU 前校验品类上限（聚合不变量统一由聚合根保证）
        if (_items.Count >= MaxVariety)
        {
            throw new CartDomainException($"购物车品类数量已达上限 {MaxVariety}", "CART_VARIETY_LIMIT");
        }

        var item = new CartItem(Guid.NewGuid(), Id, skuId, sellerId, quantity);
        _items.Add(item);
        AddDomainEvent(new SkuAddedToCartEvent(Id, skuId));
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
    /// 每移除一项发布 <see cref="SkuRemovedFromCartEvent"/>，与 <see cref="RemoveItem"/> 行为一致，
    /// 由基础设施层维护购物车-SKU 反向索引。
    /// 返回被清除项的来源标识列表。
    /// </summary>
    public IReadOnlyList<Guid> ClearSelectedItems()
    {
        var selected = _items.Where(i => i.IsSelected).ToList();
        var sourceIds = selected.Select(i => i.SourceCartItemId).ToList();

        foreach (var item in selected)
        {
            _items.Remove(item);
            AddDomainEvent(new SkuRemovedFromCartEvent(Id, item.SkuId));
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
    /// 跳过无效项（<see cref="CartItem.IsValid"/>=false，例如商品已下架），不参与合并。
    /// 合并后校验：单 SKU 数量上限 99，品类上限 50。
    /// 选中状态：若任一来源选中则选中。
    /// 返回合并项数量（仅计入有效项）。
    /// </summary>
    /// <param name="anonymousCart">匿名购物车聚合。</param>
    /// <returns>合并的购物车项数量（仅有效项，以匿名购物车项数计）。</returns>
    public int MergeFrom(Cart anonymousCart)
    {
        ArgumentNullException.ThrowIfNull(anonymousCart);

        var mergedCount = 0;
        foreach (var item in anonymousCart.Items.Where(i => i.IsValid))
        {
            // 检查品类上限（新增项时）
            if (!TryGetItem(item.SkuId, out _) && _items.Count >= MaxVariety)
            {
                throw new CartDomainException($"购物车品类数量已达上限 {MaxVariety}", "CART_VARIETY_LIMIT");
            }

            AddItem(item.SkuId, item.Quantity, item.SellerId);

            // 选中状态：任一来源选中则选中
            if (item.IsSelected && TryGetItem(item.SkuId, out var merged))
            {
                merged.Select();
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

    /// <summary>
    /// 按 SKU 标识查找购物车项，避免在 <see cref="AddItem"/>/<see cref="MergeFrom"/> 等场景重复扫描。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="item">匹配到的购物车项；未找到为 null。</param>
    /// <returns>是否找到匹配项。</returns>
    private bool TryGetItem(Guid skuId, out CartItem? item)
    {
        foreach (var i in _items)
        {
            if (i.SkuId == skuId)
            {
                item = i;
                return true;
            }
        }

        item = null;
        return false;
    }

    private CartItem? FindItem(Guid skuId) => _items.FirstOrDefault(i => i.SkuId == skuId);
}

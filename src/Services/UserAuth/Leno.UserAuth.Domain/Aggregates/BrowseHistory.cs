using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 浏览历史聚合根，记录用户浏览商品 SPU/SKU 的时序事件。
/// 同一用户对同一 SPU 在短时间内（默认 5 秒）仅记录一次，由应用层调用幂等检查保证。
/// </summary>
public sealed class BrowseHistory : AggregateRoot
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>商品 SPU 标识（跨 BC 引用）。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>商品 SKU 标识（可空，进入详情页时可能未选 SKU）。</summary>
    public Guid? SkuId { get; private set; }

    /// <summary>浏览时间（UTC）。</summary>
    public DateTime ViewedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private BrowseHistory() { }

    private BrowseHistory(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建一条浏览历史记录。<paramref name="viewedAt"/> 缺省为当前 UTC 时间。
    /// </summary>
    public static BrowseHistory Create(Guid id, Guid userId, Guid spuId, Guid? skuId = null, DateTime? viewedAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("浏览历史标识不可为空", "BROWSE_HISTORY_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "BROWSE_HISTORY_USER_EMPTY");
        }

        if (spuId == Guid.Empty)
        {
            throw new UserAuthDomainException("商品 SPU 标识不可为空", "BROWSE_HISTORY_SPU_EMPTY");
        }

        if (skuId.HasValue && skuId.Value == Guid.Empty)
        {
            throw new UserAuthDomainException("商品 SKU 标识不可为空 GUID", "BROWSE_HISTORY_SKU_EMPTY");
        }

        return new BrowseHistory(id)
        {
            UserId = userId,
            SpuId = spuId,
            SkuId = skuId,
            ViewedAt = viewedAt ?? DateTime.UtcNow
        };
    }

    /// <summary>更新浏览时间到最新（重复浏览时由应用层调用，避免新增重复记录）。</summary>
    public void MarkRevisited(DateTime? viewedAt = null)
    {
        ViewedAt = viewedAt ?? DateTime.UtcNow;
    }
}

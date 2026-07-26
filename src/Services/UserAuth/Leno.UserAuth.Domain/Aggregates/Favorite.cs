using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 商品收藏聚合根，记录用户对 SPU 的收藏关系。
/// 同一用户对同一 SPU 仅存在一条收藏记录（唯一约束由仓储/数据库保证）。
/// 收藏为软删除可选场景，本域采用硬删除以简化语义（取消收藏即移除记录）。
/// </summary>
public sealed class Favorite : AggregateRoot
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>商品 SPU 标识（跨 BC 引用，本域不持有 SPU 实体）。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>收藏时间（UTC）。</summary>
    public DateTime FavoritedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Favorite() { }

    private Favorite(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建一条收藏记录。<paramref name="favoritedAt"/> 缺省为当前 UTC 时间。
    /// </summary>
    public static Favorite Create(Guid id, Guid userId, Guid spuId, DateTime? favoritedAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("收藏标识不可为空", "FAVORITE_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "FAVORITE_USER_EMPTY");
        }

        if (spuId == Guid.Empty)
        {
            throw new UserAuthDomainException("商品 SPU 标识不可为空", "FAVORITE_SPU_EMPTY");
        }

        return new Favorite(id)
        {
            UserId = userId,
            SpuId = spuId,
            FavoritedAt = favoritedAt ?? DateTime.UtcNow
        };
    }
}

using Leno.SharedKernel.Abstractions;
using Leno.UserCenter.Domain.Aggregates;

namespace Leno.UserCenter.Domain.Repositories;

/// <summary>
/// 商品收藏仓储接口，定义在领域层，由基础设施层实现。
/// 支持按用户分页查询、按用户与 SPU 唯一查询、批量删除与计数。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public interface IFavoriteRepository : IRepository<Favorite>
{
    /// <summary>按用户与 SPU 查询收藏记录，不存在返回 null。用于幂等检查。</summary>
    Task<Favorite?> GetByUserAndSpuAsync(Guid userId, Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询用户收藏列表，按指定排序字段与方向返回。
    /// </summary>
    Task<(IReadOnlyList<Favorite> Items, int Total)> QueryAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        string sort = "created",
        string order = "desc",
        CancellationToken ct = default);

    /// <summary>统计用户收藏总数。</summary>
    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>批量删除用户对指定 SPU 列表的收藏记录，返回实际删除条数。</summary>
    Task<int> BatchDeleteAsync(Guid userId, IReadOnlyCollection<Guid> spuIds, CancellationToken ct = default);
}

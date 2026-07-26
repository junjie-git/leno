using Leno.UserCenter.Application.DTOs;

namespace Leno.UserCenter.Application;

/// <summary>
/// 商品收藏应用服务，编排收藏增删查与批量操作用例。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public interface IFavoritesAppService
{
    /// <summary>分页查询当前用户收藏列表。</summary>
    Task<PagedResult<FavoriteDto>> ListAsync(Guid userId, FavoriteQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// 收藏商品。幂等：若已收藏同一 SPU 则返回已有记录，不重复新增。
    /// </summary>
    Task<FavoriteDto> AddAsync(Guid userId, AddFavoriteDto dto, CancellationToken ct = default);

    /// <summary>取消收藏单个 SPU。若记录不存在视为成功（幂等）。</summary>
    Task RemoveAsync(Guid userId, Guid spuId, CancellationToken ct = default);

    /// <summary>批量取消收藏。返回实际删除条数。</summary>
    Task<int> BatchDeleteAsync(Guid userId, BatchDeleteFavoritesDto dto, CancellationToken ct = default);

    /// <summary>查询当前用户收藏总数（用于「我的」页角标）。</summary>
    Task<FavoriteCountDto> CountAsync(Guid userId, CancellationToken ct = default);
}

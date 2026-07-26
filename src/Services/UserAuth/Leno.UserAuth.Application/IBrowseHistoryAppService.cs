using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application;

/// <summary>
/// 浏览历史应用服务，编排浏览历史记录、查询、删除与清空用例。
/// </summary>
public interface IBrowseHistoryAppService
{
    /// <summary>分页查询当前用户浏览历史（按浏览时间倒序）。</summary>
    Task<PagedResult<BrowseHistoryDto>> ListAsync(Guid userId, BrowseHistoryQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// 记录浏览历史。幂等：相同 SPU 在短时间内（默认 5 秒）仅更新 <see cref="Domain.Aggregates.BrowseHistory.ViewedAt"/>，不新增记录。
    /// </summary>
    Task<BrowseHistoryDto> AddAsync(Guid userId, AddBrowseHistoryDto dto, CancellationToken ct = default);

    /// <summary>删除单条浏览历史。若记录不存在或归属他人视为失败。</summary>
    Task RemoveAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>批量删除浏览历史。返回实际删除条数。</summary>
    Task<int> BatchDeleteAsync(Guid userId, BatchDeleteBrowseHistoryDto dto, CancellationToken ct = default);

    /// <summary>清空当前用户全部浏览历史。返回实际删除条数。</summary>
    Task<int> ClearAllAsync(Guid userId, CancellationToken ct = default);
}

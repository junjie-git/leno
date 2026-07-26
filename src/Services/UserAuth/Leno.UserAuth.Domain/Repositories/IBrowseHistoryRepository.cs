using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Aggregates;

namespace Leno.UserAuth.Domain.Repositories;

/// <summary>
/// 浏览历史仓储接口，定义在领域层，由基础设施层实现。
/// 支持按用户分页查询、按用户与 SPU 唯一查询（幂等用）、批量删除与清空。
/// </summary>
public interface IBrowseHistoryRepository : IRepository<BrowseHistory>
{
    /// <summary>按用户与 SPU 查询最近一条浏览历史，用于幂等检查。</summary>
    Task<BrowseHistory?> FindLatestByUserAndSpuAsync(Guid userId, Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询用户浏览历史，按 <see cref="BrowseHistory.ViewedAt"/> 倒序返回。
    /// </summary>
    Task<(IReadOnlyList<BrowseHistory> Items, int Total)> QueryAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>批量删除用户指定 ID 列表的浏览历史，返回实际删除条数。</summary>
    Task<int> BatchDeleteAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>清空用户全部浏览历史，返回实际删除条数。</summary>
    Task<int> ClearAllByUserAsync(Guid userId, CancellationToken ct = default);
}

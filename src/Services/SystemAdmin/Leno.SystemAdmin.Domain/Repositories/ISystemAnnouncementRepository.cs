using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 系统公告仓储接口，定义在领域层，由基础设施层实现。
/// 支持按类型、状态查询及已发布公告查询，写操作由工作单元统一提交。
/// </summary>
public interface ISystemAnnouncementRepository : IRepository<SystemAnnouncement>
{
    /// <summary>
    /// 分页查询公告，支持类型与状态过滤。
    /// </summary>
    /// <param name="announcementType">类型过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<SystemAnnouncement>> QueryAsync(AnnouncementType? announcementType, AnnouncementStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计公告数量，支持类型与状态过滤。
    /// </summary>
    /// <param name="announcementType">类型过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(AnnouncementType? announcementType, AnnouncementStatus? status, CancellationToken ct = default);

    /// <summary>
    /// 分页查询当前有效（已发布且未过期）的公告。
    /// </summary>
    /// <param name="now">当前时间（UTC）。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<SystemAnnouncement>> GetPublishedAsync(DateTime now, int page, int pageSize, CancellationToken ct = default);
}

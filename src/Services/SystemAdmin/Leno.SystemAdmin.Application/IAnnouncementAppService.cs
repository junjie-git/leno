using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 系统公告管理应用服务接口。
/// </summary>
public interface IAnnouncementAppService
{
    /// <summary>创建公告（初始为草稿态）。</summary>
    Task<AnnouncementDto> CreateAsync(SaveAnnouncementDto dto, CancellationToken ct = default);

    /// <summary>更新公告（仅草稿态可更新）。</summary>
    Task<AnnouncementDto> UpdateAsync(Guid announcementId, SaveAnnouncementDto dto, CancellationToken ct = default);

    /// <summary>发布公告并发布集成事件。</summary>
    Task PublishAsync(Guid announcementId, CancellationToken ct = default);

    /// <summary>撤回公告（仅已发布态可撤回）。</summary>
    Task UnpublishAsync(Guid announcementId, CancellationToken ct = default);

    /// <summary>按标识获取公告。</summary>
    Task<AnnouncementDto?> GetByIdAsync(Guid announcementId, CancellationToken ct = default);

    /// <summary>分页查询公告，支持类型与状态过滤。</summary>
    Task<AnnouncementListResultDto> QueryAsync(AnnouncementType? type, AnnouncementStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>分页查询当前有效（已发布且未过期）的公告。</summary>
    Task<AnnouncementListResultDto> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default);
}

using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application;

/// <summary>
/// 通知模板管理应用服务接口（运营端）。
/// </summary>
public interface INotificationTemplateAppService
{
    /// <summary>创建通知模板。</summary>
    Task<NotificationTemplateDto> CreateAsync(SaveNotificationTemplateDto dto, CancellationToken ct = default);

    /// <summary>更新通知模板。</summary>
    Task<NotificationTemplateDto> UpdateAsync(Guid templateId, SaveNotificationTemplateDto dto, CancellationToken ct = default);

    /// <summary>启用模板。</summary>
    Task EnableAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>禁用模板。</summary>
    Task DisableAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>分页查询模板列表。</summary>
    Task<NotificationTemplateListResultDto> QueryTemplatesAsync(string? code, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 按模板标识查询。走主键查询，避免全表加载后内存查找。
    /// </summary>
    Task<NotificationTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>预览模板渲染结果。</summary>
    Task<TemplatePreviewResultDto> PreviewAsync(Guid templateId, PreviewTemplateDto dto, CancellationToken ct = default);
}

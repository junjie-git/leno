using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using NotificationTemplateAggregate = Leno.Notification.Domain.Aggregates.NotificationTemplate;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 通知模板管理应用服务实现（运营端 CRUD + 预览渲染）。
/// </summary>
public sealed class NotificationTemplateAppService : INotificationTemplateAppService
{
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ITemplateRenderer _renderer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationTemplateAppService> _logger;

    public NotificationTemplateAppService(
        INotificationTemplateRepository templateRepository,
        ITemplateRenderer renderer,
        IUnitOfWork unitOfWork,
        ILogger<NotificationTemplateAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _templateRepository = templateRepository;
        _renderer = renderer;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto> CreateAsync(SaveNotificationTemplateDto dto, CancellationToken ct = default)
    {
        var templateId = Guid.NewGuid();
        var template = NotificationTemplateAggregate.Create(
            templateId, dto.EventType, dto.Channel, dto.TitleTemplate, dto.ContentTemplate, dto.Variables);

        await _templateRepository.AddAsync(template, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("通知模板已创建 TemplateId={TemplateId} EventType={EventType} Channel={Channel}", templateId, dto.EventType, dto.Channel);
        return ToDto(template);
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto> UpdateAsync(Guid templateId, SaveNotificationTemplateDto dto, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"通知模板不存在 TemplateId={templateId}");

        template.Update(dto.TitleTemplate, dto.ContentTemplate, dto.Variables);
        await _templateRepository.UpdateAsync(template, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(template);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"通知模板不存在 TemplateId={templateId}");

        template.Enable();
        await _templateRepository.UpdateAsync(template, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"通知模板不存在 TemplateId={templateId}");

        template.Disable();
        await _templateRepository.UpdateAsync(template, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateListResultDto> QueryTemplatesAsync(string? eventType, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _templateRepository.QueryAsync(eventType, channel, page, pageSize, ct);
        var total = await _templateRepository.CountAsync(eventType, channel, ct);

        return new NotificationTemplateListResultDto
        {
            Items = items.ConvertAll(ToDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<TemplatePreviewResultDto> PreviewAsync(Guid templateId, PreviewTemplateDto dto, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"通知模板不存在 TemplateId={templateId}");

        var (title, content) = _renderer.Render(template, dto.Variables);
        return new TemplatePreviewResultDto { Title = title, Content = content };
    }

    private static NotificationTemplateDto ToDto(NotificationTemplateAggregate template)
    {
        return new NotificationTemplateDto
        {
            TemplateId = template.Id,
            EventType = template.EventType,
            Channel = template.Channel,
            TitleTemplate = template.TitleTemplate,
            ContentTemplate = template.ContentTemplate,
            Variables = template.Variables,
            Status = template.Status,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}

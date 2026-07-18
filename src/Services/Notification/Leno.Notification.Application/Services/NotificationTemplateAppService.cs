using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
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
    private readonly ITemplateRenderService _templateRenderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationTemplateAppService> _logger;

    public NotificationTemplateAppService(
        INotificationTemplateRepository templateRepository,
        ITemplateRenderer renderer,
        ITemplateRenderService templateRenderService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationTemplateAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(templateRenderService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _templateRepository = templateRepository;
        _renderer = renderer;
        _templateRenderService = templateRenderService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto> CreateAsync(SaveNotificationTemplateDto dto, CancellationToken ct = default)
    {
        var templateId = Guid.NewGuid();
        var template = NotificationTemplateAggregate.Create(
            templateId, dto.Code, dto.Name, dto.Channel, dto.Subject, dto.Body, dto.Variables,
            dto.SmsTemplateCode, dto.Description);

        // 校验未定义占位符
        var undefined = _templateRenderService.ValidateUndefinedPlaceholders(template);
        if (undefined.Count > 0)
        {
            throw new NotificationDomainException(
                $"模板中存在未定义的占位符：{string.Join(", ", undefined)}",
                "TEMPLATE_UNDEFINED_PLACEHOLDERS");
        }

        await _templateRepository.AddAsync(template, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("通知模板已创建 TemplateId={TemplateId} Code={Code} Channel={Channel}", templateId, dto.Code, dto.Channel);
        return ToDto(template);
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto> UpdateAsync(Guid templateId, SaveNotificationTemplateDto dto, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"通知模板不存在 TemplateId={templateId}");

        // 禁用的模板必须先启用才能编辑
        if (template.Status == TemplateStatus.Disabled)
        {
            throw new NotificationDomainException(
                "禁用的模板必须先启用才能编辑",
                "TEMPLATE_DISABLED_CANNOT_EDIT");
        }

        template.Update(dto.Subject, dto.Body, dto.Variables);

        // 校验未定义占位符（模板中有 {{xxx}} 但未在 Variables 中声明）
        var undefined = _templateRenderService.ValidateUndefinedPlaceholders(template);
        if (undefined.Count > 0)
        {
            throw new NotificationDomainException(
                $"模板中存在未定义的占位符：{string.Join(", ", undefined)}",
                "TEMPLATE_UNDEFINED_PLACEHOLDERS");
        }

        // 校验未使用的变量（Variables 中声明了但模板中无 {{xxx}}）
        var unused = _templateRenderService.ValidateUnusedVariables(template);
        if (unused.Count > 0)
        {
            throw new NotificationDomainException(
                $"模板中存在未使用的变量：{string.Join(", ", unused)}",
                "TEMPLATE_UNUSED_VARIABLES");
        }

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
    public async Task<NotificationTemplateListResultDto> QueryTemplatesAsync(string? code, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _templateRepository.QueryAsync(code, channel, page, pageSize, ct);
        var total = await _templateRepository.CountAsync(code, channel, ct);

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
            Code = template.Code,
            Name = template.Name,
            Channel = template.Channel,
            Subject = template.Subject,
            Body = template.Body,
            SmsTemplateCode = template.SmsTemplateCode,
            Description = template.Description,
            Variables = template.Variables,
            Status = template.Status,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}
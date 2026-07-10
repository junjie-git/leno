using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统公告管理应用服务实现。
/// 发布公告后发布 <see cref="AnnouncementPublishedEvent"/> 集成事件，驱动消息通知域推送。
/// </summary>
public sealed class AnnouncementAppService : IAnnouncementAppService
{
    private readonly ISystemAnnouncementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AnnouncementAppService> _logger;

    public AnnouncementAppService(
        ISystemAnnouncementRepository repository,
        IUnitOfWork unitOfWork,
        IEventBus eventBus,
        ILogger<AnnouncementAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> CreateAsync(SaveAnnouncementDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var announcementId = Guid.NewGuid();
        var entity = SystemAnnouncement.Create(
            announcementId, dto.Title, dto.Content, dto.Type, dto.TargetAudience, dto.PublishAt, dto.ExpireAt);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已创建：{AnnouncementId}", announcementId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> UpdateAsync(Guid announcementId, SaveAnnouncementDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Update(dto.Title, dto.Content, dto.Type, dto.TargetAudience, dto.PublishAt, dto.ExpireAt);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已更新：{AnnouncementId}", announcementId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task PublishAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Publish();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        await _eventBus.PublishAsync(new AnnouncementPublishedEvent(entity.AnnouncementId, entity.Title, (int)entity.Type), ct);

        _logger.LogInformation("公告已发布：{AnnouncementId}", announcementId);
    }

    /// <inheritdoc />
    public async Task UnpublishAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await RequireAnnouncementAsync(announcementId, ct);
        entity.Unpublish();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("公告已撤回：{AnnouncementId}", announcementId);
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto?> GetByIdAsync(Guid announcementId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(announcementId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<AnnouncementListResultDto> QueryAsync(AnnouncementType? type, AnnouncementStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(type, status, page, pageSize, ct);
        var total = await _repository.CountAsync(type, status, ct);

        return new AnnouncementListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<AnnouncementListResultDto> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.GetPublishedAsync(DateTime.UtcNow, page, pageSize, ct);

        return new AnnouncementListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = items.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<SystemAnnouncement> RequireAnnouncementAsync(Guid announcementId, CancellationToken ct)
        => await _repository.GetByIdAsync(announcementId, ct)
           ?? throw new InvalidOperationException($"公告 {announcementId} 不存在");

    private static AnnouncementDto ToDto(SystemAnnouncement entity)
        => new()
        {
            AnnouncementId = entity.AnnouncementId,
            Title = entity.Title,
            Content = entity.Content,
            Type = entity.Type,
            TargetAudience = entity.TargetAudience,
            PublishAt = entity.PublishAt,
            ExpireAt = entity.ExpireAt,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}

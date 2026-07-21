using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 系统公告管理应用服务实现。
/// 发布公告经聚合根附加 <see cref="Leno.SystemAdmin.Domain.Events.AnnouncementPublishedEvent"/> 领域事件，
/// 由工作单元的发件箱机制在同一事务内持久化并发布，不手动调用 IEventBus。
/// </summary>
public sealed class AnnouncementAppService : IAnnouncementAppService
{
    private readonly ISystemAnnouncementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnnouncementAppService> _logger;

    public AnnouncementAppService(
        ISystemAnnouncementRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AnnouncementAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
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

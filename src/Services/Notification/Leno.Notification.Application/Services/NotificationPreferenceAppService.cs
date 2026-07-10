using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using NotificationPreferenceAggregate = Leno.Notification.Domain.Aggregates.NotificationPreference;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 用户通知偏好管理应用服务实现。
/// </summary>
public sealed class NotificationPreferenceAppService : INotificationPreferenceAppService
{
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationPreferenceAppService> _logger;

    public NotificationPreferenceAppService(
        INotificationPreferenceRepository preferenceRepository,
        IUnitOfWork unitOfWork,
        ILogger<NotificationPreferenceAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(preferenceRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _preferenceRepository = preferenceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationPreferenceDto> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var preference = await _preferenceRepository.GetByUserIdAsync(userId, ct);

        if (preference is null)
        {
            // 用户未配置偏好时返回默认空偏好
            return new NotificationPreferenceDto
            {
                PreferenceId = Guid.Empty,
                UserId = userId,
                EventChannels = [],
                Status = Domain.ValueObjects.PreferenceStatus.Active
            };
        }

        return ToDto(preference);
    }

    /// <inheritdoc />
    public async Task SetChannelPreferenceAsync(Guid userId, SetChannelPreferenceDto dto, CancellationToken ct = default)
    {
        var preference = await _preferenceRepository.GetByUserIdAsync(userId, ct);

        if (preference is null)
        {
            preference = NotificationPreferenceAggregate.Create(Guid.NewGuid(), userId);
            preference.SetChannelPreference(dto.EventType, dto.Channels);
            await _preferenceRepository.AddAsync(preference, ct);
        }
        else
        {
            preference.SetChannelPreference(dto.EventType, dto.Channels);
            await _preferenceRepository.UpdateAsync(preference, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static NotificationPreferenceDto ToDto(NotificationPreferenceAggregate preference)
    {
        return new NotificationPreferenceDto
        {
            PreferenceId = preference.Id,
            UserId = preference.UserId,
            EventChannels = preference.EventChannels,
            Status = preference.Status
        };
    }
}

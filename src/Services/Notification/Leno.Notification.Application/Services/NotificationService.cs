using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 通知统一发送服务实现，封装模板查找、渲染、渠道发送、状态更新全流程。
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const int SendTimeoutSeconds = 3;

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationRecordRepository _recordRepository;
    private readonly ITemplateRenderer _renderer;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly IUserContactService _userContactService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationTemplateRepository templateRepository,
        INotificationRecordRepository recordRepository,
        ITemplateRenderer renderer,
        IEnumerable<INotificationChannel> channels,
        IUserContactService userContactService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _templateRepository = templateRepository;
        _recordRepository = recordRepository;
        _renderer = renderer;
        _channels = channels;
        _userContactService = userContactService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationSendResult> SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        // 1. Idempotency check
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _recordRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            if (existing is not null)
            {
                _logger.LogInformation("幂等命中 IdempotencyKey={Key} RecordId={RecordId}", request.IdempotencyKey, existing.Id);
                return new NotificationSendResult
                {
                    Succeeded = existing.Status == NotificationStatus.Succeeded,
                    RecordId = existing.Id,
                    ErrorCode = existing.ErrorCode,
                    ErrorMessage = existing.ErrorMessage
                };
            }
        }

        // 2. Template lookup
        var template = await _templateRepository.GetEnabledByCodeAsync(request.TemplateCode, ct);
        if (template is null)
        {
            _logger.LogWarning("未找到启用模板 TemplateCode={Code}", request.TemplateCode);
            return new NotificationSendResult
            {
                Succeeded = false,
                ErrorCode = "TEMPLATE_NOT_FOUND",
                ErrorMessage = $"模板 {request.TemplateCode} 不存在或未启用"
            };
        }

        // 3. Template rendering
        string title;
        string content;
        try
        {
            (title, content) = _renderer.Render(template, request.Variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模板渲染失败 TemplateCode={Code}", request.TemplateCode);
            return new NotificationSendResult
            {
                Succeeded = false,
                ErrorCode = "TEMPLATE_RENDER_FAILED",
                ErrorMessage = $"模板渲染失败：{ex.Message}"
            };
        }

        // 4. Create NotificationRecord
        var recordId = Guid.NewGuid();
        var record = NotificationRecord.Create(
            recordId,
            request.UserId,
            request.TemplateCode,
            eventId: null,
            template.Channel,
            title,
            content,
            businessRef: string.IsNullOrWhiteSpace(request.BusinessRef) ? null : request.BusinessRef,
            idempotencyKey: string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey);

        await _recordRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 5. Get the right channel
        var channel = _channels.FirstOrDefault(c => c.Channel == template.Channel);
        if (channel is null)
        {
            _logger.LogWarning("未找到渠道实现 Channel={Channel}", template.Channel);
            return new NotificationSendResult
            {
                Succeeded = false,
                RecordId = recordId,
                ErrorCode = "CHANNEL_NOT_FOUND",
                ErrorMessage = $"未找到渠道 {template.Channel} 的实现"
            };
        }

        // 6. Channel send with 3s timeout
        record.MarkSending();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(SendTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var sendRequest = await BuildChannelSendRequestAsync(record, linkedCts.Token);
            var result = await channel.SendAsync(sendRequest, linkedCts.Token);
            if (result.Succeeded)
            {
                record.MarkSucceeded(result.ChannelMessageId);
            }
            else
            {
                record.MarkFailed(result.ErrorMessage ?? "发送失败", result.ErrorCode);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // 7. Timeout: return accepted, processing asynchronously
            _logger.LogWarning("通知发送超时 RecordId={RecordId} TemplateCode={Code} Channel={Channel}",
                recordId, request.TemplateCode, template.Channel);

            await _recordRepository.UpdateAsync(record, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new NotificationSendResult
            {
                Succeeded = true,
                RecordId = recordId,
                ErrorCode = "ACCEPTED_TIMEOUT",
                ErrorMessage = "通知已接受，正在异步处理中"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知发送异常 RecordId={RecordId} Channel={Channel}", recordId, template.Channel);
            record.MarkFailed(ex.Message, "SEND_EXCEPTION");
        }

        // 8. Save changes
        await _recordRepository.UpdateAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new NotificationSendResult
        {
            Succeeded = record.Status == NotificationStatus.Succeeded,
            RecordId = recordId,
            ErrorCode = record.ErrorCode,
            ErrorMessage = record.ErrorMessage
        };
    }

    private async Task<ChannelSendRequest> BuildChannelSendRequestAsync(NotificationRecord record, CancellationToken ct)
    {
        var contacts = await _userContactService.GetContactsAsync(record.UserId, ct);
        var recipient = Recipient.Create(
            record.UserId,
            contacts?.Email,
            contacts?.PhoneNumber);

        return new ChannelSendRequest(
            record.Channel,
            recipient,
            record.Title,
            record.Content,
            record.IdempotencyKey ?? string.Empty);
    }
}
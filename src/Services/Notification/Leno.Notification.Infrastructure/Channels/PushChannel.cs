using System;
using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 推送渠道 mock 实现，用于验证"新增渠道实现 IChannel + DI 注册即可被注册表自动发现，零侵入核心调度"。
/// 实际投递逻辑（FCM / APNs / Web Push）由后续迭代接入，当前实现始终返回成功结果，
/// 仅验证注册表汇总与能力声明链路可用。
/// </summary>
public sealed class PushChannel : INotificationChannel
{
    private static readonly NotificationChannelMetadata MetadataValue = new(
        ChannelKey.Push,
        "推送",
        new NotificationChannelCapabilities(
            RequiresRateLimit: false,
            SupportsAsyncReceipt: true,
            IsIdempotent: false,
            SupportsTemplate: true,
            Timeout: TimeSpan.FromSeconds(15)),
        IsEnabled: true,
        Priority: 40);

    private readonly ILogger<PushChannel> _logger;

    public PushChannel(ILogger<PushChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => (NotificationChannel)(-1);

    /// <inheritdoc />
    public ChannelKey ChannelKey => ChannelKey.Push;

    /// <inheritdoc />
    public NotificationChannelMetadata Metadata => MetadataValue;

    /// <inheritdoc />
    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messageId = $"push-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "推送渠道 mock 发送完成 UserId={UserId} Subject={Subject} MessageId={MessageId}",
            request.Recipient.UserId, request.Subject, messageId);

        return Task.FromResult(new ChannelSendResult(true, null, null, messageId));
    }
}

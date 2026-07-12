namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 渠道发送结果记录，封装单次渠道发送的结果。
/// </summary>
public sealed record ChannelSendResult(
    bool Succeeded,
    string? ErrorMessage,
    string? ErrorCode,
    string? ChannelMessageId);
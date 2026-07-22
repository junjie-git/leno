using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 渠道选择器领域服务实现，管理主备适配器选择与 failover 决策。
///
/// 规则：
/// - Email 渠道仅 SMTP，无备选
/// - SMS 渠道根据 Provider 配置选择 Aliyun 或 Tencent 作为主适配器，另一家为备选
/// - 仅可重试错误触发同渠道内 failover，不可跨渠道
/// - 所有适配器不可用时记录失败并告警
///
/// 可重试错误码（允许 failover）：
///   SMTP_RETRYABLE, SMTP_CONNECT_TIMEOUT, SMS_TIMEOUT,
///   EMAIL_EXCEPTION, SMS_EXCEPTION, SEND_EXCEPTION, ACCEPTED_TIMEOUT
///
/// 不可重试错误码（不触发 failover）：
///   SMTP_NON_RETRYABLE, EMAIL_EMPTY, EMAIL_CONFIG_MISSING,
///   SMS_PHONE_EMPTY, SMS_CONFIG_MISSING, SMS_HTTP_ERROR
///
/// P2-42：未知错误码默认不可重试（不触发 failover），与 RetryPolicy.ShouldRetry 行为对齐。
///       如需对特定错误码 failover，应显式加入 RetryableErrorCodes 白名单。
/// </summary>
public sealed class ChannelSelector : IChannelSelector
{
    private static readonly HashSet<string> RetryableErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SMTP_RETRYABLE",
        "SMTP_CONNECT_TIMEOUT",
        "SMS_TIMEOUT",
        "EMAIL_EXCEPTION",
        "SMS_EXCEPTION",
        "SEND_EXCEPTION",
        "ACCEPTED_TIMEOUT",
        "DISPATCH_EXCEPTION",
        "RETRY_EXCEPTION"
    };

    private static readonly HashSet<string> NonRetryableErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SMTP_NON_RETRYABLE",
        "EMAIL_EMPTY",
        "EMAIL_CONFIG_MISSING",
        "SMS_PHONE_EMPTY",
        "SMS_CONFIG_MISSING",
        "SMS_HTTP_ERROR",
        "TEMPLATE_NOT_FOUND",
        "TEMPLATE_RENDER_FAILED",
        "CHANNEL_NOT_FOUND"
    };

    private readonly string _smsProvider;

    /// <summary>
    /// 初始化渠道选择器。
    /// </summary>
    /// <param name="smsProvider">短信服务商，默认 "Aliyun"，可选 "Tencent"。</param>
    public ChannelSelector(string smsProvider = "Aliyun")
    {
        if (string.IsNullOrWhiteSpace(smsProvider))
        {
            throw new NotificationDomainException("短信服务商不可为空", "CHANNEL_SELECTOR_SMS_PROVIDER_EMPTY");
        }

        _smsProvider = smsProvider;
    }

    /// <inheritdoc />
    public string SelectProvider(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => "SMTP",
            NotificationChannel.Sms => NormalizeProvider(_smsProvider),
            NotificationChannel.InApp => "InApp",
            _ => throw new NotificationDomainException(
                $"未知渠道 {channel}", "CHANNEL_SELECTOR_UNKNOWN_CHANNEL")
        };
    }

    /// <inheritdoc />
    public string SelectSmsProvider()
    {
        return NormalizeProvider(_smsProvider);
    }

    /// <inheritdoc />
    public string? SelectFallbackProvider(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => null, // SMTP only, no fallback
            NotificationChannel.Sms => GetSmsFallback(),
            NotificationChannel.InApp => null, // InApp has no fallback
            _ => null
        };
    }

    /// <inheritdoc />
    public bool IsRetryableError(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            // P2-42：未知/空错误码默认不可重试，与 RetryPolicy.ShouldRetry 行为对齐
            return false;
        }

        if (NonRetryableErrorCodes.Contains(errorCode))
        {
            return false;
        }

        if (RetryableErrorCodes.Contains(errorCode))
        {
            return true;
        }

        // 5xx 类错误码（如 SMTP 5xx、HTTP 5xx）→ 服务端错误，可重试
        if (errorCode.StartsWith('5'))
        {
            return true;
        }

        // P2-42：未在白名单内的未知错误码默认不可重试，不触发 failover
        return false;
    }

    /// <inheritdoc />
    public bool ShouldFailover(NotificationChannel channel, string? errorCode)
    {
        if (!IsRetryableError(errorCode))
        {
            return false;
        }

        var fallback = SelectFallbackProvider(channel);
        return fallback is not null;
    }

    private string? GetSmsFallback()
    {
        var normalized = NormalizeProvider(_smsProvider);
        return normalized switch
        {
            "Aliyun" => "Tencent",
            "Tencent" => "Aliyun",
            _ => null
        };
    }

    private static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return provider;
        }

        var trimmed = provider.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed.ToUpperInvariant();
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}
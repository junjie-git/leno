using Leno.Notification.Domain.Services;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 重试策略实现，基于错误码分类判断是否可重试，并提供指数退避延迟。
/// 
/// 可重试错误码：
///   SMTP 421/450/452 → 临时失败
///   SMTP_RETRYABLE → 邮件临时失败
///   5xx HTTP 错误 → 服务端错误
///   SMS_TIMEOUT → 短信超时
///   SMTP_CONNECT_TIMEOUT → SMTP连接超时
///   DISPATCH_EXCEPTION / RETRY_EXCEPTION → 未知异常（可重试）
///   
/// 不可重试错误码（直接死信）：
///   SMTP_NON_RETRYABLE → 550 邮箱不存在
///   EMAIL_EMPTY / SMS_PHONE_EMPTY → 联系方式缺失
///   EMAIL_CONFIG_MISSING / SMS_CONFIG_MISSING → 配置缺失
///   SMS_HTTP_ERROR → HTTP 4xx 错误
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
    private static readonly HashSet<string> NonRetryableErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // 邮箱不存在 / 黑名单 / 签名不匹配
        "SMTP_NON_RETRYABLE",
        "EMAIL_EMPTY",
        "EMAIL_CONFIG_MISSING",
        "SMS_PHONE_EMPTY",
        "SMS_CONFIG_MISSING",
        "SMS_HTTP_ERROR",
        "TEMPLATE_NOT_FOUND",
        "TEMPLATE_RENDER_FAILED",
        "CHANNEL_NOT_FOUND",
        "NOTIFICATION_RECORD_ID_EMPTY",
        "NOTIFICATION_USER_EMPTY",
        "NOTIFICATION_TEMPLATE_CODE_EMPTY",
        "NOTIFICATION_TITLE_EMPTY",
        "NOTIFICATION_CONTENT_EMPTY",
        "NOTIFICATION_CHANNEL_INVALID"
    };

    private static readonly HashSet<string> RetryableErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // SMTP 421/450/452 临时失败
        "SMTP_RETRYABLE",
        // 连接超时
        "SMTP_CONNECT_TIMEOUT",
        // 短信超时
        "SMS_TIMEOUT",
        // 通用异常（可能是网络抖动）
        "EMAIL_EXCEPTION",
        "SMS_EXCEPTION",
        "DISPATCH_EXCEPTION",
        "RETRY_EXCEPTION",
        "SEND_EXCEPTION",
        "ACCEPTED_TIMEOUT"
    };

    /// <summary>
    /// 指数退避延迟：30s / 2min / 10min
    /// </summary>
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10)
    ];

    /// <inheritdoc />
    public bool ShouldRetry(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            // 未知错误保守处理：可重试
            return true;
        }

        if (NonRetryableErrorCodes.Contains(errorCode))
        {
            return false;
        }

        if (RetryableErrorCodes.Contains(errorCode))
        {
            return true;
        }

        // 5xx 类错误码（如 SMTP 5xx、HTTP 5xx）→ 可重试
        if (errorCode.StartsWith('5'))
        {
            return true;
        }

        // 默认保守策略：可重试
        return true;
    }

    /// <inheritdoc />
    public TimeSpan NextDelay(int retryCount)
    {
        if (retryCount <= 0)
        {
            return BackoffDelays[0];
        }

        var index = retryCount - 1;
        if (index >= BackoffDelays.Length)
        {
            return BackoffDelays[^1];
        }

        return BackoffDelays[index];
    }
}
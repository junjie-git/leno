using Leno.Notification.Domain.Services;
using Leno.Notification.Infrastructure.Options;
using Microsoft.Extensions.Options;

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
///
/// P2-42：未知错误码默认不重试（直接死信），避免对未知错误盲目重试造成资源浪费。
///       如需对特定错误码重试，应显式加入 RetryableErrorCodes 白名单。
///
/// P1-7：退避序列、错误码白/黑名单改为通过 <see cref="IOptionsMonitor{RetryPolicyOptions}"/> 注入，
///       支持运行时热更新，缺省值与原 const 完全对齐（零行为变更）。
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
    /// <summary>
    /// 错误码大小写不敏感比较器，与原 static HashSet 使用的 <c>StringComparer.OrdinalIgnoreCase</c> 行为一致。
    /// </summary>
    private static readonly StringComparer ErrorCodeComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IOptionsMonitor<RetryPolicyOptions> _options;

    public RetryPolicy(IOptionsMonitor<RetryPolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public bool ShouldRetry(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            // P2-42：未知/空错误码默认不重试，避免盲目重试未知错误
            return false;
        }

        var opts = _options.CurrentValue;

        if (opts.NonRetryableErrorCodes.Contains(errorCode, ErrorCodeComparer))
        {
            return false;
        }

        if (opts.RetryableErrorCodes.Contains(errorCode, ErrorCodeComparer))
        {
            return true;
        }

        // 5xx 类错误码（如 SMTP 5xx、HTTP 5xx）→ 服务端错误，可重试
        if (errorCode.StartsWith('5'))
        {
            return true;
        }

        // P2-42：未在白名单内的未知错误码默认不重试，直接进入死信
        return false;
    }

    /// <inheritdoc />
    public TimeSpan NextDelay(int retryCount)
    {
        var backoffSeconds = _options.CurrentValue.BackoffSeconds;

        if (backoffSeconds.Length == 0)
        {
            return TimeSpan.Zero;
        }

        if (retryCount <= 0)
        {
            return TimeSpan.FromSeconds(backoffSeconds[0]);
        }

        var index = retryCount - 1;
        if (index >= backoffSeconds.Length)
        {
            return TimeSpan.FromSeconds(backoffSeconds[^1]);
        }

        return TimeSpan.FromSeconds(backoffSeconds[index]);
    }
}

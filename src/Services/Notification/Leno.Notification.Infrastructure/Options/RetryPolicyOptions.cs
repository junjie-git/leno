namespace Leno.Notification.Infrastructure.Options;

/// <summary>
/// 重试策略配置项。
/// 缺省值与原 <c>RetryPolicy.cs</c> 静态常量完全对齐（零行为变更门禁）。
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "Notification:RetryPolicy";

    /// <summary>
    /// 指数退避延迟序列（秒）。
    /// 缺省值：30s / 2min / 10min，与原 <c>BackoffDelays</c> 静态数组完全一致。
    /// </summary>
    public int[] BackoffSeconds { get; set; } = [30, 120, 600];

    /// <summary>
    /// 可重试错误码白名单（大小写不敏感比较由服务层保证）。
    /// 缺省值与原 <c>RetryableErrorCodes</c> 静态 HashSet 完全一致。
    /// </summary>
    public string[] RetryableErrorCodes { get; set; } =
    [
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
    ];

    /// <summary>
    /// 不可重试错误码黑名单（优先级高于白名单与 5xx 前缀规则）。
    /// 缺省值与原 <c>NonRetryableErrorCodes</c> 静态 HashSet 完全一致。
    /// </summary>
    public string[] NonRetryableErrorCodes { get; set; } =
    [
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
    ];
}

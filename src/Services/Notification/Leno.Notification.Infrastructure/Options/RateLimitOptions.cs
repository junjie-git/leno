namespace Leno.Notification.Infrastructure.Options;

/// <summary>
/// 频率限制配置项。
/// 缺省值与原 <c>RedisRateLimiter.cs</c> 静态常量完全对齐（零行为变更门禁）。
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "Notification:RateLimit";

    /// <summary>
    /// 邮件小时限流阈值。
    /// 缺省值与原 <c>EmailHourlyLimit</c> const 完全一致。
    /// </summary>
    public int EmailHourlyLimit { get; set; } = 10;

    /// <summary>
    /// 短信小时限流阈值。
    /// 缺省值与原 <c>SmsHourlyLimit</c> const 完全一致。
    /// </summary>
    public int SmsHourlyLimit { get; set; } = 5;

    /// <summary>
    /// 短信日限流阈值。
    /// 缺省值与原 <c>SmsDailyLimit</c> const 完全一致。
    /// </summary>
    public int SmsDailyLimit { get; set; } = 20;

    /// <summary>
    /// 验证码类通知小时限流阈值。
    /// 缺省值与原 <c>VerificationCodeHourlyLimit</c> const 完全一致。
    /// </summary>
    public int VerificationCodeHourlyLimit { get; set; } = 5;

    /// <summary>
    /// 小时窗口时长（秒）。
    /// 缺省值与原 <c>HourlyWindow</c>（1 小时）完全一致。
    /// </summary>
    public int HourlyWindowSeconds { get; set; } = 3600;

    /// <summary>
    /// 日窗口时长（秒）。
    /// 缺省值与原 <c>DailyWindow</c>（24 小时）完全一致。
    /// </summary>
    public int DailyWindowSeconds { get; set; } = 86400;

    /// <summary>
    /// 按模板编码维度的限流规则覆盖。
    /// key = templateCode（大小写敏感），value = 该模板的限流规则覆盖项。
    /// 仅覆盖配置中显式设置的字段，其余字段回退到缺省值。
    /// </summary>
    public Dictionary<string, TemplateRateLimitRule> PerTemplateCode { get; set; } = new();
}

/// <summary>
/// 按模板编码维度的限流规则覆盖项。
/// 所有字段均可空，未设置的字段回退到 <see cref="RateLimitOptions"/> 缺省值。
/// </summary>
public sealed class TemplateRateLimitRule
{
    /// <summary>邮件小时限流覆盖（null 表示回退缺省值）。</summary>
    public int? EmailHourlyLimit { get; set; }

    /// <summary>短信小时限流覆盖（null 表示回退缺省值）。</summary>
    public int? SmsHourlyLimit { get; set; }

    /// <summary>短信日限流覆盖（null 表示回退缺省值）。</summary>
    public int? SmsDailyLimit { get; set; }
}

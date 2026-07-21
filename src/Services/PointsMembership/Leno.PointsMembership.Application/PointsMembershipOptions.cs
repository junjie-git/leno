namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分与会员域配置选项，集中管理硬编码的业务阈值与时区设置。
/// 通过 IOptions&lt;PointsMembershipOptions&gt; 模式从 appsettings.json 的 PointsMembership 节绑定。
/// </summary>
public sealed class PointsMembershipOptions
{
    /// <summary>积分过期阈值（月），默认 12 个月。</summary>
    public int ExpiryMonths { get; init; } = 12;

    /// <summary>每日评价返积分上限（条），默认 5 条。</summary>
    public int ReviewDailyLimit { get; init; } = 5;

    /// <summary>默认用户时区（IANA 时区标识），用于签到日期与 Redis Key 的"日"计算，默认 Asia/Shanghai。</summary>
    public string DefaultTimeZone { get; init; } = "Asia/Shanghai";

    /// <summary>Redis 每日计数 Key 的过期时间（小时），默认 25 小时（覆盖所有时区的当日窗口）。</summary>
    public int RedisDailyKeyTtlHours { get; init; } = 25;
}

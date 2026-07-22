namespace Leno.SellerShop.Infrastructure.Configuration;

/// <summary>
/// 资质到期提醒配置，通过 appsettings.json 的 "QualificationReminder" 节绑定。
/// 控制扫描间隔与提醒天数阈值，支持不同环境（测试/生产）灵活配置。
/// </summary>
public sealed class QualificationReminderOptions
{
    public const string SectionName = "QualificationReminder";

    /// <summary>
    /// 提醒天数阈值列表，资质在距到期日 N 天时触发提醒。
    /// 默认 [30, 7, 1]，生产环境每日扫描一次。
    /// </summary>
    public int[] ReminderDays { get; init; } = [30, 7, 1];

    /// <summary>
    /// 扫描间隔（小时），默认 24 小时。
    /// 测试环境可配置为更短间隔（如 1/24≈0.04 小时≈2.5 分钟）以便快速验证。
    /// </summary>
    public int ScanIntervalHours { get; init; } = 24;
}

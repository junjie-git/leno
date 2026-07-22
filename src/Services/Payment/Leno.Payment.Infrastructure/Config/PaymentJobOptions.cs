namespace Leno.Payment.Infrastructure.Config;

/// <summary>
/// 支付补偿任务配置选项，绑定 appsettings 中 <c>Payment:Jobs</c> 节。
/// P2-20：将 PaymentStatusCheckJob 中硬编码的 ThresholdMinutes/BatchSize 提取为可配置项，
/// 允许不同环境按负载调整扫描阈值与批次大小。
/// </summary>
public sealed class PaymentJobOptions
{
    /// <summary>
    /// 补偿扫描阈值（分钟）。超过此时长仍未收到异步通知的支付单触发主动渠道查询。
    /// 默认 5 分钟，与原硬编码常量保持一致。
    /// </summary>
    public int ThresholdMinutes { get; set; } = 5;

    /// <summary>
    /// 单次扫描批次大小。控制每轮查询与关单处理的支付单数量，避免单次扫描对数据库造成过大压力。
    /// 默认 100，与原硬编码常量保持一致。小环境可调小以降低 DB 压力，大环境可调大以减少扫描轮次。
    /// </summary>
    public int BatchSize { get; set; } = 100;
}

namespace Leno.Payment.Application.Services;

/// <summary>
/// 对账服务接口，由基础设施层实现。
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// 执行对账：下载渠道账单、解析、与系统支付单对比、记录差异。
    /// </summary>
    Task ReconcileAsync(DateTime billDate, CancellationToken ct = default);
}
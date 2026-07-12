using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Domain.Services;

/// <summary>
/// 物流轨迹查询领域服务接口，封装第三方物流轨迹查询能力。
/// 接口定义在领域层，实现位于基础设施层，屏蔽第三方物流 API 细节。
/// </summary>
public interface ILogisticsTrackingService
{
    /// <summary>
    /// 查询物流单号对应的物流轨迹。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    /// <param name="companyCode">物流公司编码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>物流轨迹查询结果，包含轨迹节点列表与缓存状态。</returns>
    Task<LogisticsTraceResult> QueryTraceAsync(string logisticsNo, string companyCode, CancellationToken ct = default);
}
using Leno.Points.Application.DTOs;

namespace Leno.Points.Application;

/// <summary>
/// 运营手动发放积分应用服务接口，对应 POST /api/admin/points/award 端点。
/// </summary>
public interface IAwardAppService
{
    /// <summary>
    /// 运营手动发放积分，校验账户存在与发放数量合法，累加余额与累计获取。
    /// </summary>
    /// <param name="userId">目标用户标识。</param>
    /// <param name="amount">发放积分数量，须 &gt; 0。</param>
    /// <param name="reason">发放原因（用于流水审计）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>发放结果，包含发放后余额与累计统计。</returns>
    Task<AwardResultDto> AwardAsync(Guid userId, int amount, string reason, CancellationToken ct = default);
}

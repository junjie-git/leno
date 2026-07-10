using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分管理应用服务，编排签到、积分余额查询、流水查询与运营手动发放用例。
/// </summary>
public interface IPointsAppService
{
    /// <summary>
    /// 每日签到，计算连续签到天数与奖励积分，发放积分到账户。
    /// </summary>
    /// <param name="userId">签到用户标识。</param>
    Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 查询用户积分账户余额与累计统计。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<PointsAccountDto> GetPointsAccountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询用户积分流水。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页条数。</param>
    Task<List<PointsLedgerDto>> GetLedgerAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 运营手动发放积分。
    /// </summary>
    /// <param name="dto">发放参数。</param>
    Task AwardPointsAsync(AwardPointsDto dto, CancellationToken ct = default);
}

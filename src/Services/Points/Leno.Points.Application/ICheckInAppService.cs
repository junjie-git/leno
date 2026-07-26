using Leno.Points.Application.DTOs;

namespace Leno.Points.Application;

/// <summary>
/// 签到应用服务接口，编排每日签到用例。
/// </summary>
public interface ICheckInAppService
{
    /// <summary>
    /// 每日签到，计算连续签到天数与奖励积分，发放积分到账户。
    /// 当日重复签到抛出 <c>PointsDomainException</c>（错误码 CHECKIN_ALREADY）。
    /// </summary>
    /// <param name="userId">签到用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>签到结果，包含签到记录与奖励积分。</returns>
    Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default);
}

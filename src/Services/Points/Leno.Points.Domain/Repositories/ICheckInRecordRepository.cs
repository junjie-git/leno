using Leno.Points.Domain.Aggregates.CheckInRecord;
using Leno.SharedKernel.Abstractions;
using CheckInRecordAggregate = Leno.Points.Domain.Aggregates.CheckInRecord.CheckInRecord;

namespace Leno.Points.Domain.Repositories;

/// <summary>
/// 签到记录仓储接口，管理 <see cref="CheckInRecord"/> 聚合。
/// </summary>
public interface ICheckInRecordRepository : IRepository<CheckInRecordAggregate>
{
    /// <summary>
    /// 按用户标识与签到日期查询签到记录，用于判定当日是否已签到。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="checkInDate">签到日期。</param>
    Task<CheckInRecordAggregate?> GetByUserIdAndDateAsync(Guid userId, DateOnly checkInDate, CancellationToken ct = default);

    /// <summary>
    /// 查询用户最近一次签到记录，用于计算连续签到天数。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<CheckInRecordAggregate?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default);
}

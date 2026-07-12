using Leno.Promotion.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 秒杀预占记录仓储接口。
/// </summary>
public interface ISeckillPreOccupationRecordRepository : IRepository<SeckillPreOccupationRecord>
{
    /// <summary>
    /// 查询超时未履约的预占记录（补偿任务扫描）。
    /// </summary>
    /// <param name="timeout">超时阈值，预占时间早于此时间视为超时。</param>
    /// <param name="skip">跳过的记录数。</param>
    /// <param name="take">每次取回的记录数。</param>
    Task<List<SeckillPreOccupationRecord>> GetUnfulfilledAsync(
        DateTime timeout,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询预占记录。
    /// </summary>
    Task<SeckillPreOccupationRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}
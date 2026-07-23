using Leno.Inventory.Domain.Aggregates;

namespace Leno.Inventory.Domain.Repositories;

/// <summary>
/// 库存预占回滚补偿仓储接口（T18），定义在领域层，由基础设施层实现。
/// </summary>
public interface IStockReservationCompensationRepository
{
    /// <summary>
    /// 按标识获取补偿记录。
    /// </summary>
    /// <param name="id">补偿记录标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockReservationCompensation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 新增补偿记录。
    /// </summary>
    /// <param name="compensation">补偿记录聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(StockReservationCompensation compensation, CancellationToken ct = default);

    /// <summary>
    /// 更新补偿记录（重试后状态变更）。
    /// </summary>
    /// <param name="compensation">补偿记录聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task UpdateAsync(StockReservationCompensation compensation, CancellationToken ct = default);

    /// <summary>
    /// 分页查询待重试（Pending）的补偿记录，按创建时间升序（先入先重试）。
    /// </summary>
    /// <param name="batchSize">单批拉取数量。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<StockReservationCompensation>> GetPendingAsync(int batchSize, CancellationToken ct = default);
}

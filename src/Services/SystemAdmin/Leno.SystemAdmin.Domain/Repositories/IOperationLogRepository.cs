using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 操作日志仓储接口，定义在领域层，由基础设施层实现。
/// 操作日志仅追加，不提供更新/删除操作，故不继承 <see cref="Leno.SharedKernel.Abstractions.IRepository{T}"/>。
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// 追加操作日志。
    /// </summary>
    /// <param name="log">操作日志聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(OperationLog log, CancellationToken ct = default);

    /// <summary>
    /// 按标识获取操作日志。
    /// </summary>
    /// <param name="id">日志标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<OperationLog?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 按来源事件标识获取操作日志，用于幂等去重。
    /// </summary>
    /// <param name="eventId">来源集成事件标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<OperationLog?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询操作日志，支持运营人员、模块与时间区间过滤。
    /// </summary>
    /// <param name="operatorId">运营人员标识，可空表示不限。</param>
    /// <param name="moduleName">模块，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<OperationLog>> QueryAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计操作日志数量，支持运营人员、模块与时间区间过滤。
    /// </summary>
    /// <param name="operatorId">运营人员标识，可空表示不限。</param>
    /// <param name="moduleName">模块，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default);
}

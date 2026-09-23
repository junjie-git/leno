namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 工作单元事务句柄，抽象 EF Core 的 <c>IDbContextTransaction</c>，避免领域层引用 EF Core。
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable, IDisposable
{
    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}

/// <summary>
/// 工作单元接口，管理事务边界与变更提交。
/// 一个应用服务用例方法对应一个事务边界。
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 保存聚合变更、将集成事件写入发件箱表（同一事务）、清除领域事件。
    /// <para>
    /// 原有的 <c>SaveChangesAsync</c> 已于 2026-09-21 移除（双轨下线 F2）：
    /// 该方法名暗示"仅保存变更"，历史上确曾旁路 Outbox 导致领域事件丢失，
    /// 后虽改为内部委托 Outbox 路径，但名称仍具误导性，且 29 处调用使其
    /// <c>[Obsolete]</c> 告警成为常态噪声。统一使用本方法表达"提交一个用例的事务边界"。
    /// </para>
    /// </summary>
    Task<bool> SaveEntitiesAsync(CancellationToken ct = default);

    /// <summary>开启数据库事务。</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

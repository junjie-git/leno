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
    /// <summary>保存变更并返回受影响行数。</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// 保存聚合变更、将集成事件写入发件箱表（同一事务）、清除领域事件。
    /// </summary>
    Task<bool> SaveEntitiesAsync(CancellationToken ct = default);

    /// <summary>开启数据库事务。</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

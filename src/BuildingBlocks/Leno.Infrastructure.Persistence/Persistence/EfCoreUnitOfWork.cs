using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 泛型 EF Core 工作单元实现，包装业务上下文 <typeparamref name="TDbContext"/>。
/// </summary>
/// <remarks>
/// <para>
/// 抽取自各 BC（Order/Cart/Payment 等）100% 同构的 <c>UnitOfWork</c> 副本，消除约 680 行重复代码。
/// 业务上下文只需提供 <c>DbContext</c> 与 <see cref="IIntegrationEventMapper"/> 两项依赖即可复用本类。
/// </para>
/// <para>
/// <see cref="SaveEntitiesAsync"/> 经 <see cref="OutboxDbContextExtensions.SaveChangesWithOutboxAsync"/>
/// 在同一事务内保存聚合变更与集成事件，保证原子性；事务提交后清除领域事件。
/// </para>
/// <para>
/// 各 BC DI 注册示例（在 <c>AddXxxInfrastructure</c> 中）：
/// <code>
/// services.AddScoped&lt;IUnitOfWork, EfCoreUnitOfWork&lt;OrderDbContext&gt;&gt;();
/// </code>
/// 业务上下文 <c>DbContext</c> 与 <see cref="IIntegrationEventMapper"/> 须先于本类注册。
/// </para>
/// </remarks>
/// <typeparam name="TDbContext">业务上下文 DbContext 类型，约束为 <see cref="DbContext"/>。</typeparam>
public sealed class EfCoreUnitOfWork<TDbContext> : IUnitOfWork
    where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly IIntegrationEventMapper _mapper;

    /// <summary>
    /// 初始化 <see cref="EfCoreUnitOfWork{TDbContext}"/> 的新实例。
    /// </summary>
    /// <param name="context">业务上下文 <typeparamref name="TDbContext"/> 实例。</param>
    /// <param name="mapper">领域事件到集成事件的翻译器。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> 或 <paramref name="mapper"/> 为 null。</exception>
    public EfCoreUnitOfWork(TDbContext context, IIntegrationEventMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mapper);
        _context = context;
        _mapper = mapper;
    }

    /// <inheritdoc />
    /// <summary>
    /// 已废弃：使用 <see cref="SaveEntitiesAsync"/> 替代，确保领域事件经 Outbox 持久化。
    /// 此方法保留仅为向后兼容，内部委托给 <see cref="OutboxDbContextExtensions.SaveChangesWithOutboxAsync"/>，
    /// 不再直接调 <c>DbContext.SaveChangesAsync</c> 旁路 Outbox（避免领域事件丢失或双发）。
    /// </summary>
    [Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox. 此方法旁路 Outbox 会导致事件丢失或双发。")]
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesWithOutboxAsync(_mapper, ct);

    /// <inheritdoc />
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesWithOutboxAsync(_mapper, ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfCoreUnitOfWorkTransaction(transaction);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// 工作单元事务句柄实现，包装 EF Core 的 <see cref="IDbContextTransaction"/>。
    /// </summary>
    private sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        /// <inheritdoc />
        public void Dispose() => _transaction.Dispose();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}

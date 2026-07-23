using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leno.Cart.Infrastructure;

/// <summary>
/// Cart BC 专用工作单元，在落库前分发 SkuAddedToCartEvent/SkuRemovedFromCartEvent 到反向索引服务。
/// 替代 <see cref="EfCoreUnitOfWork{TDbContext}"/> 的默认行为以维护购物车-SKU 反向索引。
/// </summary>
public sealed class CartUnitOfWork : IUnitOfWork
{
    private readonly CartDbContext _context;
    private readonly IIntegrationEventMapper _mapper;
    private readonly CartSkuIndexDomainEventDispatcher _indexDispatcher;

    public CartUnitOfWork(
        CartDbContext context,
        IIntegrationEventMapper mapper,
        CartSkuIndexDomainEventDispatcher indexDispatcher)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(indexDispatcher);
        _context = context;
        _mapper = mapper;
        _indexDispatcher = indexDispatcher;
    }

    /// <inheritdoc />
    /// <summary>
    /// 已废弃：使用 <see cref="SaveEntitiesAsync"/> 替代，确保领域事件经 Outbox 持久化。
    /// 此方法保留仅为向后兼容，内部委托给 <see cref="OutboxDbContextExtensions.SaveChangesWithOutboxAsync"/>，
    /// 不再直接调 <c>DbContext.SaveChangesAsync</c> 旁路 Outbox（避免 Cart 领域事件丢失或双发）。
    /// </summary>
    [Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox. 此方法旁路 Outbox 会导致事件丢失或双发。")]
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesWithOutboxAsync(_mapper, ct);

    /// <inheritdoc />
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        // 步骤 1：在事务开启前，从 ChangeTracker 收集 Cart 聚合的领域事件并分发到反向索引。
        // 索引维护失败时直接抛出，由全局异常处理，避免聚合状态已落库但索引缺失。
        var aggregates = _context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        var skuIndexEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .OfType<object>()
            .ToList();

        if (skuIndexEvents.Count > 0)
        {
            await _indexDispatcher.DispatchAsync(skuIndexEvents, ct);
        }

        // 步骤 2：在事务内保存聚合变更 + 集成事件落 Outbox（同时清空领域事件）
        await _context.SaveChangesWithOutboxAsync(_mapper, ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new UnitOfWorkTransaction(transaction);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// 工作单元事务句柄实现，包装 EF Core 的 <see cref="IDbContextTransaction"/>。
    /// </summary>
    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public UnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        public void Dispose() => _transaction.Dispose();

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}

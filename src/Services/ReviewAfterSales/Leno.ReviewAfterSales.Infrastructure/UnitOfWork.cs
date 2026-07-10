using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leno.ReviewAfterSales.Infrastructure;

/// <summary>
/// 工作单元实现，包装 <see cref="ReviewAfterSalesDbContext"/>。
/// <see cref="SaveEntitiesAsync"/> 经发件箱扩展将聚合产生的集成事件与状态变更在同一事务保存。
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReviewAfterSalesDbContext _context;

    public UnitOfWork(ReviewAfterSalesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesWithOutboxAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfCoreUnitOfWorkTransaction(transaction);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        public void Dispose() => _transaction.Dispose();

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}

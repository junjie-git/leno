using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 积分账户 EF Core 仓储实现。
/// 读取时一并加载 FrozenEntries 冻结明细集合，保证聚合内不变量操作完整。
/// </summary>
public sealed class EfCorePointsAccountRepository : IPointsAccountRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCorePointsAccountRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PointsAccountAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PointsAccounts
            .Include(a => a.FrozenEntries)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<PointsAccountAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.PointsAccounts
            .Include(a => a.FrozenEntries)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<PointsAccountAggregate?> GetByFrozenOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.PointsAccounts
            .Include(a => a.FrozenEntries)
            .FirstOrDefaultAsync(a => a.FrozenEntries.Any(e => e.OrderId == orderId), ct);

    /// <inheritdoc />
    public async Task AddAsync(PointsAccountAggregate aggregate, CancellationToken ct = default)
        => await _context.PointsAccounts.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PointsAccountAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsAccounts.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PointsAccountAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsAccounts.Remove(aggregate);
        return Task.CompletedTask;
    }
}

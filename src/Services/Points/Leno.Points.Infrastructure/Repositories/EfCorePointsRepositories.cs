using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;
using PointsFlowAggregate = Leno.Points.Domain.Aggregates.PointsFlow.PointsFlow;

namespace Leno.Points.Infrastructure.Repositories;

/// <summary>
/// 积分账户 EF Core 仓储实现（Points BC 独立维护）。
/// 读取时一并加载 FrozenEntries 冻结明细集合，保证聚合内不变量操作完整。
/// </summary>
public sealed class EfCorePointsAccountRepository : IPointsAccountRepository
{
    private readonly PointsDbContext _context;

    public EfCorePointsAccountRepository(PointsDbContext context)
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
    {
        var frozenEntry = await _context.PointsFrozenEntries
            .FirstOrDefaultAsync(e => e.OrderId == orderId, ct);

        if (frozenEntry is null)
        {
            return null;
        }

        var accountId = _context.Entry(frozenEntry).Property<Guid>("PointsAccountId").CurrentValue;

        return await _context.PointsAccounts
            .Include(a => a.FrozenEntries)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);
    }

    /// <inheritdoc />
    public async Task<List<PointsAccountAggregate>> GetAllWithPositiveBalanceAsync(
        int skip, int take, CancellationToken ct = default)
        => await _context.PointsAccounts
            .Include(a => a.FrozenEntries)
            .Where(a => a.Balance.Available > 0)
            .OrderBy(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PointsFlowAggregate>> GetEarnFlowsByAccountIdAsync(
        Guid accountId, CancellationToken ct = default)
        => await _context.PointsFlows
            .Where(l => l.AccountId == accountId && l.TxType == PointsTxType.Earn)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PointsFlowAggregate>> GetFlowsByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var accountId = await _context.PointsAccounts
            .Where(a => a.UserId == userId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (accountId is null)
        {
            return new List<PointsFlowAggregate>();
        }

        var skip = (page - 1) * pageSize;
        return await _context.PointsFlows
            .Where(l => l.AccountId == accountId.Value)
            .OrderByDescending(l => l.OccurredAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);
    }

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

/// <summary>
/// 积分流水 EF Core 仓储实现（Points BC 独立维护）。
/// </summary>
public sealed class EfCorePointsFlowRepository : IPointsFlowRepository
{
    private readonly PointsDbContext _context;

    public EfCorePointsFlowRepository(PointsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<PointsFlowAggregate>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await _context.PointsFlows
            .Where(l => l.AccountId == accountId)
            .OrderByDescending(l => l.OccurredAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PointsFlowAggregate>> GetEarnFlowsByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await _context.PointsFlows
            .Where(l => l.AccountId == accountId && l.TxType == PointsTxType.Earn)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(ct);
}

/// <summary>
/// 积分兑换 EF Core 仓储实现（Points BC 独立维护）。
/// </summary>
public sealed class EfCorePointsExchangeRepository : IPointsExchangeRepository
{
    private readonly PointsDbContext _context;

    public EfCorePointsExchangeRepository(PointsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PointsExchangeAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PointsExchanges.FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<PointsExchangeAggregate>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.PointsExchanges
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.RequestedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<PointsExchangeAggregate?> GetByTargetAsync(Guid targetId, Guid userId, CancellationToken ct = default)
        => await _context.PointsExchanges
            .FirstOrDefaultAsync(e => e.TargetId == targetId && e.UserId == userId, ct);

    /// <inheritdoc />
    public async Task AddAsync(PointsExchangeAggregate aggregate, CancellationToken ct = default)
        => await _context.PointsExchanges.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PointsExchangeAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsExchanges.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PointsExchangeAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsExchanges.Remove(aggregate);
        return Task.CompletedTask;
    }
}

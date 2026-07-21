using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
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
    {
        // PM-M01 修复：先按 order_id 直接查冻结明细（命中 ix_points_frozen_entries_order_id 索引），
        // 取出影子外键 PointsAccountId，再按账户标识加载聚合并 Include FrozenEntries，
        // 避免 FrozenEntries.Any 集合扫描导致索引失效
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
            .Where(a => a.Balance > 0)
            .OrderBy(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PointsLedger>> GetEarnLedgersByAccountIdAsync(
        Guid accountId, CancellationToken ct = default)
        => await _context.PointsLedgers
            .Where(l => l.AccountId == accountId && l.TxType == PointsTxType.Earn)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PointsLedger>> GetLedgersByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        // PM-M07 修复：按用户标识分页查询积分流水
        // 先按 UserId 查询账户标识（每用户仅一个账户），再按 AccountId 分页查询流水
        // 按发生时间倒序（最新在前），分页参数 page 从 1 开始，skip = (page - 1) * pageSize
        var accountId = await _context.PointsAccounts
            .Where(a => a.UserId == userId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (accountId is null)
        {
            return new List<PointsLedger>();
        }

        var skip = (page - 1) * pageSize;
        return await _context.PointsLedgers
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

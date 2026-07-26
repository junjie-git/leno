using Leno.PointsMembership.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PointsRuleAggregate = Leno.PointsMembership.Domain.Aggregates.PointsRule;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 积分规则 EF Core 仓储实现。
/// 编码唯一性由数据库唯一索引（ix_points_rules_code）兜底，防并发插入冲突。
/// </summary>
public sealed class EfCorePointsRuleRepository : IPointsRuleRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCorePointsRuleRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PointsRuleAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PointsRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<PointsRuleAggregate?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.PointsRules.FirstOrDefaultAsync(r => r.Code == code, ct);

    /// <inheritdoc />
    public async Task<List<PointsRuleAggregate>> GetAllAsync(CancellationToken ct = default)
        => await _context.PointsRules
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(PointsRuleAggregate aggregate, CancellationToken ct = default)
        => await _context.PointsRules.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PointsRuleAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsRules.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PointsRuleAggregate aggregate, CancellationToken ct = default)
    {
        _context.PointsRules.Remove(aggregate);
        return Task.CompletedTask;
    }
}

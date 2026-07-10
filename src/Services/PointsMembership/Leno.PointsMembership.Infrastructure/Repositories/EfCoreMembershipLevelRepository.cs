using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using MembershipLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipLevel;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 会员等级 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreMembershipLevelRepository : IMembershipLevelRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreMembershipLevelRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MembershipLevelAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.MembershipLevels.FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public async Task<MembershipLevelAggregate?> GetByLevelAsync(int level, CancellationToken ct = default)
        => await _context.MembershipLevels.FirstOrDefaultAsync(l => l.Level == level, ct);

    /// <inheritdoc />
    public async Task<List<MembershipLevelAggregate>> GetAllEnabledAsync(CancellationToken ct = default)
        => await _context.MembershipLevels
            .Where(l => l.Status == MembershipLevelStatus.Enabled)
            .OrderBy(l => l.Level)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<MembershipLevelAggregate>> GetAllAsync(CancellationToken ct = default)
        => await _context.MembershipLevels
            .OrderBy(l => l.Level)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(MembershipLevelAggregate aggregate, CancellationToken ct = default)
        => await _context.MembershipLevels.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MembershipLevelAggregate aggregate, CancellationToken ct = default)
    {
        _context.MembershipLevels.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(MembershipLevelAggregate aggregate, CancellationToken ct = default)
    {
        _context.MembershipLevels.Remove(aggregate);
        return Task.CompletedTask;
    }
}

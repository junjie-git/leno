using Leno.PointsMembership.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using MemberLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MemberLevel;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 会员等级（成长值体系）EF Core 仓储实现。
/// </summary>
public sealed class EfCoreMemberLevelRepository : IMemberLevelRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreMemberLevelRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MemberLevelAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.MemberLevels.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public async Task<MemberLevelAggregate?> GetByLevelAsync(int level, CancellationToken ct = default)
        => await _context.MemberLevels.FirstOrDefaultAsync(m => m.Level == level, ct);

    /// <inheritdoc />
    public async Task<List<MemberLevelAggregate>> GetAllAsync(CancellationToken ct = default)
        => await _context.MemberLevels.OrderBy(m => m.MinGrowthValue).ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(MemberLevelAggregate aggregate, CancellationToken ct = default)
        => await _context.MemberLevels.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MemberLevelAggregate aggregate, CancellationToken ct = default)
    {
        _context.MemberLevels.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(MemberLevelAggregate aggregate, CancellationToken ct = default)
    {
        _context.MemberLevels.Remove(aggregate);
        return Task.CompletedTask;
    }
}
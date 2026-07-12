using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using MemberAggregate = Leno.PointsMembership.Domain.Aggregates.Member;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 会员 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreMemberRepository : IMemberRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreMemberRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MemberAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Members.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public async Task<MemberAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<List<MemberAggregate>> GetAllActiveAsync(int skip, int take, CancellationToken ct = default)
        => await _context.Members
            .Where(m => m.Status == MemberStatus.Active)
            .OrderBy(m => m.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(MemberAggregate aggregate, CancellationToken ct = default)
        => await _context.Members.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MemberAggregate aggregate, CancellationToken ct = default)
    {
        _context.Members.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(MemberAggregate aggregate, CancellationToken ct = default)
    {
        _context.Members.Remove(aggregate);
        return Task.CompletedTask;
    }
}

using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using UserMembershipAggregate = Leno.PointsMembership.Domain.Aggregates.UserMembership;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 用户会员权益 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreUserMembershipRepository : IUserMembershipRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreUserMembershipRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<UserMembershipAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.UserMemberships.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    public async Task<UserMembershipAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.UserMemberships.FirstOrDefaultAsync(u => u.OrderId == orderId, ct);

    /// <inheritdoc />
    public async Task<UserMembershipAggregate?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.UserMemberships
            .Where(u => u.UserId == userId
                && u.Status == UserMembershipStatus.Active
                && u.EndTime > now)
            .OrderByDescending(u => u.EndTime)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(UserMembershipAggregate aggregate, CancellationToken ct = default)
        => await _context.UserMemberships.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(UserMembershipAggregate aggregate, CancellationToken ct = default)
    {
        _context.UserMemberships.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(UserMembershipAggregate aggregate, CancellationToken ct = default)
    {
        _context.UserMemberships.Remove(aggregate);
        return Task.CompletedTask;
    }
}

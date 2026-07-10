using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using MembershipPackageAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipPackage;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 会员套餐 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreMembershipPackageRepository : IMembershipPackageRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreMembershipPackageRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MembershipPackageAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.MembershipPackages.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<MembershipPackageAggregate>> GetAllEnabledAsync(CancellationToken ct = default)
        => await _context.MembershipPackages
            .Where(p => p.Status == PackageStatus.Enabled)
            .OrderBy(p => p.Level)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(MembershipPackageAggregate aggregate, CancellationToken ct = default)
        => await _context.MembershipPackages.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MembershipPackageAggregate aggregate, CancellationToken ct = default)
    {
        _context.MembershipPackages.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(MembershipPackageAggregate aggregate, CancellationToken ct = default)
    {
        _context.MembershipPackages.Remove(aggregate);
        return Task.CompletedTask;
    }
}

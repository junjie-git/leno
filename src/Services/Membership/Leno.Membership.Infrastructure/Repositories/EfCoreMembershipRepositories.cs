using Leno.Membership.Domain.Aggregates.Member;
using Leno.Membership.Domain.Aggregates.MemberLevelDefinition;
using Leno.Membership.Domain.Aggregates.MembershipPackage;
using Leno.Membership.Domain.Repositories;
using Leno.Membership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using MemberAggregate = Leno.Membership.Domain.Aggregates.Member.Member;
using MemberLevelDefinitionAggregate = Leno.Membership.Domain.Aggregates.MemberLevelDefinition.MemberLevelDefinition;
using MembershipPackageAggregate = Leno.Membership.Domain.Aggregates.MembershipPackage.MembershipPackage;

namespace Leno.Membership.Infrastructure.Repositories;

/// <summary>
/// 会员 EF Core 仓储实现（Membership BC 独立维护）。
/// </summary>
public sealed class EfCoreMemberRepository : IMemberRepository
{
    private readonly MembershipDbContext _context;

    public EfCoreMemberRepository(MembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MemberAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Members
            .Include(m => m.LevelChangeHistories)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public async Task<MemberAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Members
            .Include(m => m.LevelChangeHistories)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<List<MemberAggregate>> GetAllActiveAsync(int skip, int take, CancellationToken ct = default)
        => await _context.Members
            .Include(m => m.LevelChangeHistories)
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

/// <summary>
/// 会员等级定义 EF Core 仓储实现（Membership BC 独立维护）。
/// </summary>
public sealed class EfCoreMemberLevelDefinitionRepository : IMemberLevelDefinitionRepository
{
    private readonly MembershipDbContext _context;

    public EfCoreMemberLevelDefinitionRepository(MembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MemberLevelDefinitionAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.MemberLevelDefinitions.FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public async Task<MemberLevelDefinitionAggregate?> GetByLevelAsync(int level, CancellationToken ct = default)
        => await _context.MemberLevelDefinitions.FirstOrDefaultAsync(l => l.Level == level, ct);

    /// <inheritdoc />
    public async Task<List<MemberLevelDefinitionAggregate>> GetAllAsync(CancellationToken ct = default)
        => await _context.MemberLevelDefinitions
            .OrderBy(l => l.MinGrowthValue)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(MemberLevelDefinitionAggregate aggregate, CancellationToken ct = default)
        => await _context.MemberLevelDefinitions.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(MemberLevelDefinitionAggregate aggregate, CancellationToken ct = default)
    {
        _context.MemberLevelDefinitions.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(MemberLevelDefinitionAggregate aggregate, CancellationToken ct = default)
    {
        _context.MemberLevelDefinitions.Remove(aggregate);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 会员套餐 EF Core 仓储实现（Membership BC 独立维护）。
/// </summary>
public sealed class EfCoreMembershipPackageRepository : IMembershipPackageRepository
{
    private readonly MembershipDbContext _context;

    public EfCoreMembershipPackageRepository(MembershipDbContext context)
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

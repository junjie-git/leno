using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.Repositories;
using Leno.AccessControl.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.AccessControl.Infrastructure.Repositories;

/// <summary>
/// 用户角色分配仓储 EF Core 实现。
/// 从 UserAuth BC 的 User._roles 内联集合拆出（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class EfCoreUserRoleAssignmentRepository : IUserRoleAssignmentRepository
{
    private readonly AccessControlDbContext _context;

    public EfCoreUserRoleAssignmentRepository(AccessControlDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<UserRoleAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.UserRoleAssignments.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _context.UserRoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.IsActive)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetActiveRoleCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var roleCodes = await _context.UserRoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.IsActive)
            .Select(a => a.Role.ToString())
            .ToListAsync(ct);

        return roleCodes;
    }

    /// <inheritdoc />
    public Task<bool> HasRoleAsync(Guid userId, RoleType role, CancellationToken ct = default)
        => _context.UserRoleAssignments
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Role == role && a.IsActive, ct);

    /// <inheritdoc />
    public Task<int> CountActiveRolesAsync(Guid userId, CancellationToken ct = default)
        => _context.UserRoleAssignments
            .AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.IsActive, ct);

    /// <inheritdoc />
    public Task<UserRoleAssignment?> GetActiveAssignmentAsync(Guid userId, RoleType role, CancellationToken ct = default)
        => _context.UserRoleAssignments
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Role == role && a.IsActive, ct);

    /// <inheritdoc />
    public Task AddAsync(UserRoleAssignment assignment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _context.UserRoleAssignments.Add(assignment);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(UserRoleAssignment assignment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (_context.Entry(assignment).State == EntityState.Detached)
        {
            _context.UserRoleAssignments.Attach(assignment);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(UserRoleAssignment assignment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _context.UserRoleAssignments.Remove(assignment);
        return Task.CompletedTask;
    }
}

using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 运营人员 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreOperatorRepository : IOperatorRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreOperatorRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Operator?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Operators.FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public Task<Operator?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Operators.FirstOrDefaultAsync(o => o.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<List<Operator>> QueryAsync(OperatorRole? role, OperatorStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.Operators.AsQueryable(), role, status);
        return await query
            .OrderByDescending(o => o.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(OperatorRole? role, OperatorStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.Operators.AsQueryable(), role, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(Operator aggregate, CancellationToken ct = default)
        => await _context.Operators.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(Operator aggregate, CancellationToken ct = default)
    {
        _context.Operators.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Operator aggregate, CancellationToken ct = default)
    {
        _context.Operators.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<Operator> ApplyFilters(
        IQueryable<Operator> query,
        OperatorRole? role,
        OperatorStatus? status)
    {
        if (role.HasValue)
        {
            query = query.Where(o => o.Role == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return query;
    }
}

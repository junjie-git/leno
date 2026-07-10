using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 物流公司 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreLogisticsCompanyRepository : ILogisticsCompanyRepository
{
    private readonly OrderDbContext _context;

    public EfCoreLogisticsCompanyRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<LogisticsCompany?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.LogisticsCompanies
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<LogisticsCompany>> ListAsync(int page, int pageSize, CancellationToken ct = default)
        => await _context.LogisticsCompanies
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(LogisticsCompany aggregate, CancellationToken ct = default)
        => await _context.LogisticsCompanies.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(LogisticsCompany aggregate, CancellationToken ct = default)
    {
        _context.LogisticsCompanies.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(LogisticsCompany aggregate, CancellationToken ct = default)
    {
        _context.LogisticsCompanies.Remove(aggregate);
        return Task.CompletedTask;
    }
}

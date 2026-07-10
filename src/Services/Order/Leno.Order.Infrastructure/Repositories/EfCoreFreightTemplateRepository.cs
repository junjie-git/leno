using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 运费模板 EF Core 仓储实现。
/// 读取时一并加载 RegionRules 区域运费规则集合。
/// </summary>
public sealed class EfCoreFreightTemplateRepository : IFreightTemplateRepository
{
    private readonly OrderDbContext _context;

    public EfCoreFreightTemplateRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<FreightTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.FreightTemplates
            .Include(f => f.RegionRules)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <inheritdoc />
    public async Task<FreightTemplate?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default)
        => await _context.FreightTemplates
            .Include(f => f.RegionRules)
            .FirstOrDefaultAsync(f => f.SellerId == sellerId, ct);

    /// <inheritdoc />
    public async Task<List<FreightTemplate>> ListAsync(int page, int pageSize, CancellationToken ct = default)
        => await _context.FreightTemplates
            .Include(f => f.RegionRules)
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(FreightTemplate aggregate, CancellationToken ct = default)
        => await _context.FreightTemplates.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(FreightTemplate aggregate, CancellationToken ct = default)
    {
        _context.FreightTemplates.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(FreightTemplate aggregate, CancellationToken ct = default)
    {
        _context.FreightTemplates.Remove(aggregate);
        return Task.CompletedTask;
    }
}

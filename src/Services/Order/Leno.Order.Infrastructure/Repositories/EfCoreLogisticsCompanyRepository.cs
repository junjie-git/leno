using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
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
    public async Task<List<LogisticsCompany>> ListAsync(int page, int pageSize, string? keyword, LogisticsCompanyStatus? status, CancellationToken ct = default)
    {
        // 防御性参数归一化：page/pageSize 不合法时退化为安全默认值，避免 Skip 负数或 Take 非正。
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 200 ? 20 : pageSize;

        var query = _context.LogisticsCompanies.AsQueryable();

        // 关键词模糊匹配 Name 或 Code（Contains 大小写不敏感由 EF Core 提供程序与数据库排序规则决定）。
        // 仅在 keyword 非空白时应用，避免空字符串意外过滤掉全部记录。
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(l => l.Name.Contains(keyword) || l.Code.Contains(keyword));
        }

        // 状态过滤：仅当 status 非空时按 LogisticsCompanyStatus 枚举值精确匹配。
        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LogisticsCompany?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return await _context.LogisticsCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

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

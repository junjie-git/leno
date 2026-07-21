using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 数据字典 EF Core 仓储实现，查询时显式包含字典项子集合。
/// </summary>
public sealed class EfCoreDataDictionaryRepository : IDataDictionaryRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreDataDictionaryRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<DataDictionary?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.DataDictionaries
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public Task<DataDictionary?> GetByCodeAsync(string code, CancellationToken ct = default)
        => _context.DataDictionaries
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Code == code, ct);

    /// <inheritdoc />
    public async Task<List<DataDictionary>> QueryAsync(string? name, DictionaryStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        // ApplyFilters 返回公共 IQueryable，QueryAsync 在其基础上 .Include 子集合后分页查询
        var query = ApplyFilters(_context.DataDictionaries.AsQueryable(), name, status)
            .Include(d => d.Items);
        return await query
            .OrderByDescending(d => d.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? name, DictionaryStatus? status, CancellationToken ct = default)
    {
        // Count 不需要 Include：Include 仅影响返回实体的子集合填充，不影响行数统计。
        // 直接对 ApplyFilters 返回的 IQueryable 计数，与 QueryAsync 过滤逻辑保持一致。
        var query = ApplyFilters(_context.DataDictionaries.AsQueryable(), name, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(DataDictionary aggregate, CancellationToken ct = default)
        => await _context.DataDictionaries.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(DataDictionary aggregate, CancellationToken ct = default)
    {
        _context.DataDictionaries.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(DataDictionary aggregate, CancellationToken ct = default)
    {
        _context.DataDictionaries.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<DataDictionary> ApplyFilters(
        IQueryable<DataDictionary> query,
        string? name,
        DictionaryStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(d => d.Name.Contains(name));
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        return query;
    }
}

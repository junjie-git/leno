using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.ReviewAfterSales.Infrastructure.Repositories;

/// <summary>
/// 评价 EF Core 仓储实现。
/// 按订单行查询主评价、判断重复评价、分页条件查询评价列表。
/// </summary>
public sealed class EfCoreReviewRepository : IReviewRepository
{
    private readonly ReviewAfterSalesDbContext _context;

    public EfCoreReviewRepository(ReviewAfterSalesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<Review?> GetByOrderLineAsync(Guid orderLineId, CancellationToken ct = default)
        => await _context.Reviews.FirstOrDefaultAsync(r => r.OrderLineId == orderLineId, ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByOrderLineAsync(Guid orderLineId, CancellationToken ct = default)
        => await _context.Reviews.AnyAsync(r => r.OrderLineId == orderLineId, ct);

    /// <inheritdoc />
    public async Task<List<Review>> QueryAsync(
        Guid? spuId,
        Guid? userId,
        ReviewStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.Reviews.AsQueryable(), spuId, userId, status);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? spuId,
        Guid? userId,
        ReviewStatus? status,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.Reviews.AsQueryable(), spuId, userId, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<Review>> GetBySpuIdAsync(
        Guid spuId,
        ReviewStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Reviews.AsQueryable().Where(r => r.SpuId == spuId);
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<Review>> GetByOrderIdAsync(
        Guid orderId,
        ReviewStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Reviews.AsQueryable().Where(r => r.OrderId == orderId);
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(Review aggregate, CancellationToken ct = default)
        => await _context.Reviews.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(Review aggregate, CancellationToken ct = default)
    {
        _context.Reviews.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Review aggregate, CancellationToken ct = default)
    {
        _context.Reviews.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// </summary>
    private static IQueryable<Review> ApplyFilters(
        IQueryable<Review> query,
        Guid? spuId,
        Guid? userId,
        ReviewStatus? status)
    {
        if (spuId.HasValue)
        {
            query = query.Where(r => r.SpuId == spuId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(r => r.UserId == userId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        return query;
    }
}

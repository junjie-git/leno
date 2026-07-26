using Leno.Review.Domain.Aggregates;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using ReviewAggregate = Leno.Review.Domain.Aggregates.Review;

namespace Leno.Review.Infrastructure.Repositories;

/// <summary>
/// 评价 EF Core 仓储实现。
/// 按订单行查询主评价、判断重复评价、分页条件查询评价列表。
/// </summary>
public sealed class EfCoreReviewRepository : IReviewRepository
{
    private readonly ReviewDbContext _context;

    public EfCoreReviewRepository(ReviewDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ReviewAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<ReviewAggregate?> GetByOrderLineAsync(Guid orderLineId, CancellationToken ct = default)
        => await _context.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderLineId == orderLineId, ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByOrderLineAsync(Guid orderLineId, CancellationToken ct = default)
        => await _context.Reviews
            .AsNoTracking()
            .AnyAsync(r => r.OrderLineId == orderLineId, ct);

    /// <inheritdoc />
    public async Task<List<ReviewAggregate>> QueryAsync(
        Guid? spuId,
        Guid? userId,
        ReviewStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.Reviews.AsNoTracking(), spuId, userId, status);

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
        var query = ApplyFilters(_context.Reviews.AsNoTracking(), spuId, userId, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ReviewAggregate>> GetBySpuIdAsync(
        Guid spuId,
        ReviewStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Reviews.AsNoTracking().Where(r => r.SpuId == spuId);
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ProductRatingSnapshot?> GetRatingSnapshotAsync(Guid spuId, CancellationToken ct = default)
    {
        // 合并审计 3.4：使用 SQL 聚合替代内存计算，避免加载全部 Approved 评价到内存。
        // AsNoTracking + GroupBy + 单次查询返回 totalCount/averageRating/positiveCount 三个聚合值。
        var snapshot = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.SpuId == spuId && r.Status == ReviewStatus.Approved)
            .GroupBy(r => r.SpuId)
            .Select(g => new ProductRatingSnapshot
            {
                SpuId = g.Key,
                TotalCount = g.Count(),
                AverageRating = g.Average(r => (double)r.Rating),
                PositiveCount = g.Count(r => r.Rating >= 4)
            })
            .FirstOrDefaultAsync(ct);

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<List<ReviewAggregate>> GetByOrderIdAsync(
        Guid orderId,
        ReviewStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Reviews.AsNoTracking().Where(r => r.OrderId == orderId);
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ReviewAggregate>> QueryBySellerAsync(
        Guid sellerId,
        int? rating,
        bool? replied,
        IReadOnlyList<Guid>? spuIds,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ApplySellerFilters(_context.Reviews.AsNoTracking(), sellerId, rating, replied, spuIds, startDate, endDate);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountBySellerAsync(
        Guid sellerId,
        int? rating,
        bool? replied,
        IReadOnlyList<Guid>? spuIds,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default)
    {
        var query = ApplySellerFilters(_context.Reviews.AsNoTracking(), sellerId, rating, replied, spuIds, startDate, endDate);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetDistinctSpuIdsBySellerAsync(Guid sellerId, CancellationToken ct = default)
    {
        return await _context.Reviews
            .AsNoTracking()
            .Where(r => r.SellerId == sellerId && r.Status == ReviewStatus.Approved)
            .Select(r => r.SpuId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(ReviewAggregate aggregate, CancellationToken ct = default)
        => await _context.Reviews.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(ReviewAggregate aggregate, CancellationToken ct = default)
    {
        _context.Reviews.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ReviewAggregate aggregate, CancellationToken ct = default)
    {
        _context.Reviews.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// 审计 4.6：用 <c>is not null</c> 模式匹配替代 <c>HasValue</c>/<c>Value</c> 冗余判断，
    /// EF Core 翻译 <c>r.SpuId == spuId</c>（spuId 为 Guid?）时会自动展开为参数化等值比较。
    /// </summary>
    private static IQueryable<ReviewAggregate> ApplyFilters(
        IQueryable<ReviewAggregate> query,
        Guid? spuId,
        Guid? userId,
        ReviewStatus? status)
    {
        if (spuId is not null)
        {
            query = query.Where(r => r.SpuId == spuId);
        }

        if (userId is not null)
        {
            query = query.Where(r => r.UserId == userId);
        }

        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        return query;
    }

    /// <summary>
    /// 卖家端查询统一过滤条件：强制 sellerId + Approved 状态，叠加评分/回复状态/SpuId 列表/时间范围。
    /// 回复状态通过 SellerReplyAt 是否为空判定（已回复 = SellerReplyAt != null）。
    /// </summary>
    private static IQueryable<ReviewAggregate> ApplySellerFilters(
        IQueryable<ReviewAggregate> query,
        Guid sellerId,
        int? rating,
        bool? replied,
        IReadOnlyList<Guid>? spuIds,
        DateTime? startDate,
        DateTime? endDate)
    {
        query = query.Where(r => r.SellerId == sellerId && r.Status == ReviewStatus.Approved);

        if (rating is not null)
        {
            query = query.Where(r => r.Rating == rating);
        }

        if (replied is not null)
        {
            query = replied.Value
                ? query.Where(r => r.SellerReplyAt != null)
                : query.Where(r => r.SellerReplyAt == null);
        }

        if (spuIds is not null && spuIds.Count > 0)
        {
            query = query.Where(r => spuIds.Contains(r.SpuId));
        }

        if (startDate is not null)
        {
            query = query.Where(r => r.SubmittedAt >= startDate);
        }

        if (endDate is not null)
        {
            query = query.Where(r => r.SubmittedAt <= endDate);
        }

        return query;
    }
}

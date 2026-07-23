using Leno.AfterSales.Domain.Aggregates;
using Leno.AfterSales.Domain.Repositories;
using Leno.AfterSales.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.AfterSales.Infrastructure.Repositories;

/// <summary>
/// 售后单 EF Core 仓储实现（售后 BC 独立维护）。
/// 按订单查询售后单、判断同订单行是否存在进行中同类型售后单、分页条件查询售后单列表。
/// </summary>
public sealed class EfCoreAfterSalesRepository : IAfterSalesRepository
{
    private readonly AfterSalesDbContext _context;

    public EfCoreAfterSalesRepository(AfterSalesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AfterSalesOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.AfterSalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<AfterSalesOrder>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.AfterSalesOrders
            .AsNoTracking()
            .Where(a => a.OrderId == orderId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<bool> HasActiveByOrderLineAsync(Guid orderLineId, AfterSalesType type, CancellationToken ct = default)
    {
        // 活跃状态包含全部进行中状态：
        // - Pending/Approved：等待审核或已同意未进入下一阶段
        // - ReturnGoods/ConfirmReturn：退货退款流程中（已退货/已确认收货，待退款）
        // - Refunding：退款处理中
        // 遗漏 ReturnGoods/ConfirmReturn 会允许同订单行在退货流程中重复提交售后单。
        var activeStatuses = new List<AfterSalesStatus>
        {
            AfterSalesStatus.Pending,
            AfterSalesStatus.Approved,
            AfterSalesStatus.ReturnGoods,
            AfterSalesStatus.ConfirmReturn,
            AfterSalesStatus.Refunding
        };

        return await _context.AfterSalesOrders
            .AsNoTracking()
            .AnyAsync(a => a.OrderLineId == orderLineId && a.Type == type && activeStatuses.Contains(a.Status), ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasActiveByOrderAsync(Guid orderId, AfterSalesType type, CancellationToken ct = default)
    {
        // 整单售后（orderLineId 为 null）的重复申请校验：
        // 仅匹配 OrderLineId == null 的整单售后记录，避免与按订单行的售后单混淆。
        var activeStatuses = new List<AfterSalesStatus>
        {
            AfterSalesStatus.Pending,
            AfterSalesStatus.Approved,
            AfterSalesStatus.ReturnGoods,
            AfterSalesStatus.ConfirmReturn,
            AfterSalesStatus.Refunding
        };

        return await _context.AfterSalesOrders
            .AsNoTracking()
            .AnyAsync(a => a.OrderId == orderId && a.OrderLineId == null && a.Type == type && activeStatuses.Contains(a.Status), ct);
    }

    /// <inheritdoc />
    public async Task<List<AfterSalesOrder>> QueryAsync(
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AfterSalesOrders.AsNoTracking(), orderId, userId, sellerId, status);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AfterSalesOrders.AsNoTracking(), orderId, userId, sellerId, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(AfterSalesOrder aggregate, CancellationToken ct = default)
        => await _context.AfterSalesOrders.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(AfterSalesOrder aggregate, CancellationToken ct = default)
    {
        _context.AfterSalesOrders.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(AfterSalesOrder aggregate, CancellationToken ct = default)
    {
        _context.AfterSalesOrders.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一应用查询过滤条件，供 QueryAsync 与 CountAsync 复用。
    /// 审计 4.6：用 <c>is not null</c> 模式匹配替代 <c>HasValue</c>/<c>Value</c> 冗余判断，
    /// EF Core 翻译 <c>a.OrderId == orderId</c>（orderId 为 Guid?）时会自动展开为参数化等值比较。
    /// </summary>
    private static IQueryable<AfterSalesOrder> ApplyFilters(
        IQueryable<AfterSalesOrder> query,
        Guid? orderId,
        Guid? userId,
        Guid? sellerId,
        AfterSalesStatus? status)
    {
        if (orderId is not null)
        {
            query = query.Where(a => a.OrderId == orderId);
        }

        if (userId is not null)
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (sellerId is not null)
        {
            query = query.Where(a => a.SellerId == sellerId);
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        return query;
    }
}

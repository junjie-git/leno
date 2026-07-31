using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.SharedContracts.Responses;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表查询处理器。
/// 经 <see cref="IOrderReadModelAccessor"/>（端口由 Infrastructure 层 <c>OrderReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="PageResult{T}"/>（统一分页契约）。
/// </summary>
public sealed class OrderListQueryHandler : IQueryHandler<OrderListQuery, PageResult<OrderSummaryDto>>
{
    private readonly IOrderReadModelAccessor _readModelAccessor;

    public OrderListQueryHandler(IOrderReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<PageResult<OrderSummaryDto>> HandleAsync(OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _readModelAccessor.ListAsync(query, ct);
    }
}

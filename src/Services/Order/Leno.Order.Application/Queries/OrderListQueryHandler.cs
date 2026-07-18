using Leno.Infrastructure.Abstractions.Cqrs;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表查询处理器。
/// 经 <see cref="IOrderReadModelAccessor"/>（端口由 Infrastructure 层 <c>OrderReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="OrderListResult"/>。
/// 双发期 2 周内与 <c>OrderAppService.QueryAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class OrderListQueryHandler : IQueryHandler<OrderListQuery, OrderListResult>
{
    private readonly IOrderReadModelAccessor _readModelAccessor;

    public OrderListQueryHandler(IOrderReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<OrderListResult> HandleAsync(OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _readModelAccessor.ListAsync(query, ct);
    }
}

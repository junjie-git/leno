using Leno.Infrastructure.Abstractions.Cqrs;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单详情查询处理器。
/// 经 <see cref="IOrderReadModelAccessor"/>（端口由 Infrastructure 层 <c>OrderReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="OrderDetailResult"/>，不存在返回 null。
/// 双发期 2 周内与 <c>OrderAppService.GetByIdAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class OrderDetailQueryHandler : IQueryHandler<OrderDetailQuery, OrderDetailResult?>
{
    private readonly IOrderReadModelAccessor _readModelAccessor;

    public OrderDetailQueryHandler(IOrderReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<OrderDetailResult?> HandleAsync(OrderDetailQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // CurrentUserId 当前用于权限校验的预留扩展点（如买家/卖家身份过滤）；本实现暂不消费，
        // 与既有 OrderAppService.GetByIdAsync 行为一致（权限校验由 Controller 层完成）。
        _ = query.CurrentUserId;

        return _readModelAccessor.GetDetailAsync(query.OrderId, ct);
    }
}

using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Queries;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 订单控制器。
/// 买家端（/api/orders）：下单、立即购买、预览、列表、详情、确认收货、取消，需 Buyer 角色。
/// 卖家端（/api/seller/orders）：发货，需 Seller 角色。
/// 运营端（/api/admin/orders）：全量订单查询、强制取消，需 Operator/Admin 角色。
/// 读操作（详情、列表）已迁移至 CQRS QueryHandler，走 ES 读模型；写操作仍走 IOrderAppService。
/// </summary>
[ApiController]
public sealed class OrdersController : OrderControllerBase
{
    private readonly IOrderAppService _orderAppService;
    private readonly IQueryHandler<OrderDetailQuery, OrderDetailResult?> _orderDetailQueryHandler;
    private readonly IQueryHandler<OrderListQuery, OrderListResult> _orderListQueryHandler;

    public OrdersController(
        ICurrentUserContext currentUser,
        IOrderAppService orderAppService,
        IQueryHandler<OrderDetailQuery, OrderDetailResult?> orderDetailQueryHandler,
        IQueryHandler<OrderListQuery, OrderListResult> orderListQueryHandler)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(orderAppService);
        ArgumentNullException.ThrowIfNull(orderDetailQueryHandler);
        ArgumentNullException.ThrowIfNull(orderListQueryHandler);
        _orderAppService = orderAppService;
        _orderDetailQueryHandler = orderDetailQueryHandler;
        _orderListQueryHandler = orderListQueryHandler;
    }

    // ========== 买家端 ==========

    /// <summary>创建订单（按卖家自动拆单）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateOrderDto dto, CancellationToken ct)
    {
        var order = await _orderAppService.CreateOrderAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(order));
    }

    /// <summary>立即购买（单 SKU，内部转换为创建订单）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/orders/buy-now")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuyNowAsync([FromBody] BuyNowDto dto, CancellationToken ct)
    {
        var order = await _orderAppService.BuyNowAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(order));
    }

    /// <summary>下单预览，计算预估金额不落库。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/orders/preview")]
    [ProducesResponseType(typeof(ApiResponse<OrderPreviewResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewAsync([FromBody] CreateOrderDto dto, CancellationToken ct)
    {
        var preview = await _orderAppService.PreviewAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(preview));
    }

    /// <summary>分页查询当前用户的订单（按状态可选过滤）。走 CQRS 读侧 ES 读模型。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderListResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMineAsync([FromQuery] OrderStatus? status, [FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new OrderListQuery
        {
            UserId = GetCurrentUserId(),
            Status = status?.ToString(),
            PageIndex = page,
            PageSize = pageSize
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取订单详情。走 CQRS 读侧 ES 读模型，按当前用户做权限校验。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/orders/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailResult?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var query = new OrderDetailQuery
        {
            OrderId = id,
            CurrentUserId = GetCurrentUserId()
        };
        var result = await _orderDetailQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>确认收货。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/orders/{id:guid}/confirm")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmReceiptAsync(Guid id, CancellationToken ct)
    {
        await _orderAppService.ConfirmReceiptAsync(id, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家取消订单（待支付态）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/orders/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(Guid id, [FromBody] CancelOrderDto dto, CancellationToken ct)
    {
        await _orderAppService.CancelAsync(id, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 卖家端 ==========

    /// <summary>卖家发货。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/seller/orders/{id:guid}/ship")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ShipAsync(Guid id, [FromBody] ShipOrderDto dto, CancellationToken ct)
    {
        await _orderAppService.ShipAsync(id, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// 分页查询当前卖家的订单（按状态/下单时间范围可选过滤）。
    /// 走 CQRS 读侧 ES 读模型，SellerId 取自 JWT 强制过滤，不可查看他店订单。
    /// 复用 OrderListQuery（已支持 SellerId/Status/StartDate/EndDate 字段）与现有 OrderListQueryHandler。
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("api/seller/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderListResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSellerOrdersAsync(
        [FromQuery] OrderStatus? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OrderListQuery
        {
            SellerId = GetCurrentUserId(),
            Status = status?.ToString(),
            StartDate = startDate,
            EndDate = endDate,
            PageIndex = page,
            PageSize = pageSize
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 运营端 ==========

    /// <summary>分页查询全部订单（按用户、卖家、状态可选过滤）。走 CQRS 读侧 ES 读模型。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderListResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? userId, [FromQuery] Guid? sellerId, [FromQuery] OrderStatus? status, [FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new OrderListQuery
        {
            UserId = userId,
            SellerId = sellerId,
            Status = status?.ToString(),
            PageIndex = page,
            PageSize = pageSize
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>运营强制取消订单（待支付/已支付/已发货态）。</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/orders/{id:guid}/force-cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForceCancelAsync(Guid id, [FromBody] ForceCancelOrderDto dto, CancellationToken ct)
    {
        await _orderAppService.ForceCancelAsync(id, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success());
    }
}

using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
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
/// </summary>
[ApiController]
public sealed class OrdersController : OrderControllerBase
{
    private readonly IOrderAppService _orderAppService;

    public OrdersController(ICurrentUserContext currentUser, IOrderAppService orderAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(orderAppService);
        _orderAppService = orderAppService;
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

    /// <summary>分页查询当前用户的订单（按状态可选过滤）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMineAsync([FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _orderAppService.QueryAsync(GetCurrentUserId(), null, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取订单详情。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/orders/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var order = await _orderAppService.GetByIdAsync(id, ct);
        return Ok(ApiResponse.Success(order));
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

    // ========== 运营端 ==========

    /// <summary>分页查询全部订单（按用户、卖家、状态可选过滤）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/orders")]
    [ProducesResponseType(typeof(ApiResponse<OrderListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? userId, [FromQuery] Guid? sellerId, [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _orderAppService.QueryAsync(userId, sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>运营强制取消订单（已支付/已发货态）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/orders/{id:guid}/force-cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForceCancelAsync(Guid id, [FromBody] ForceCancelOrderDto dto, CancellationToken ct)
    {
        await _orderAppService.ForceCancelAsync(id, dto, ct);
        return Ok(ApiResponse.Success());
    }
}

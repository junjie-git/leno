using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 支付控制器。
/// 买家端（/api/payments）：发起支付，发布支付请求集成事件，需 Buyer 角色。
/// </summary>
[ApiController]
public sealed class PaymentsController : OrderControllerBase
{
    private readonly IOrderAppService _orderAppService;

    public PaymentsController(ICurrentUserContext currentUser, IOrderAppService orderAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(orderAppService);
        _orderAppService = orderAppService;
    }

    /// <summary>发起支付，发布支付请求集成事件。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/payments")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayAsync([FromQuery] Guid orderId, [FromBody] PayOrderDto dto, CancellationToken ct)
    {
        await _orderAppService.PayAsync(orderId, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success());
    }
}

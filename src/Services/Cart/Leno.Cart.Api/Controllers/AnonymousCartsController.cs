using Leno.Cart.Application;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Exceptions;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Leno.Cart.Api.Controllers;

/// <summary>
/// 匿名购物车控制器，提供无需认证的购物车端点。
/// 以会话标识（sessionId）为键管理匿名购物车，sessionId 由服务端创建时生成。
/// P1-7：启用 IP 维度限流（10 次/分钟）防止匿名接口滥用。
/// P2-6：sessionId 通过 X-Cart-Session 请求头传递，不出现在 URL 路径，避免访问日志泄露。
/// </summary>
[ApiController]
[Route("api/cart/anonymous")]
[EnableRateLimiting("anonymous-cart")]
public sealed class AnonymousCartsController : ControllerBase
{
    private const string CartSessionHeader = "X-Cart-Session";
    private readonly IAnonymousCartAppService _cartAppService;

    public AnonymousCartsController(IAnonymousCartAppService cartAppService)
    {
        ArgumentNullException.ThrowIfNull(cartAppService);
        _cartAppService = cartAppService;
    }

    /// <summary>创建匿名购物车，返回会话标识与空购物车。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AnonymousCartResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCartAsync(CancellationToken ct)
    {
        var result = await _cartAppService.CreateCartAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取匿名购物车（含实时价格与可售状态）。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCartAsync([FromHeader(Name = CartSessionHeader)] string sessionId, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var cart = await _cartAppService.GetCartAsync(sessionId, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>添加购物车项。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItemAsync([FromHeader(Name = CartSessionHeader)] string sessionId, [FromBody] AddCartItemDto dto, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var cart = await _cartAppService.AddItemAsync(sessionId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>更新购物车项数量。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpPut("items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateQuantityAsync([FromHeader(Name = CartSessionHeader)] string sessionId, Guid skuId, [FromBody] UpdateCartItemQuantityDto dto, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var cart = await _cartAppService.UpdateQuantityAsync(sessionId, skuId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>移除购物车项。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpDelete("items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItemAsync([FromHeader(Name = CartSessionHeader)] string sessionId, Guid skuId, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var cart = await _cartAppService.RemoveItemAsync(sessionId, skuId, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>批量选中/取消选中购物车项。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpPost("items/select")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectItemsAsync([FromHeader(Name = CartSessionHeader)] string sessionId, [FromBody] SelectCartItemsDto dto, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var cart = await _cartAppService.SelectItemsAsync(sessionId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>结算预览（按卖家分组返回选中项，含价格试算）。sessionId 通过 X-Cart-Session 请求头传递。</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewCheckoutAsync([FromHeader(Name = CartSessionHeader)] string sessionId, CancellationToken ct)
    {
        RequireSessionId(sessionId);
        var preview = await _cartAppService.PreviewCheckoutAsync(sessionId, ct);
        return Ok(ApiResponse.Success(preview));
    }

    private static void RequireSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new CartDomainException("匿名购物车会话标识缺失，请通过 X-Cart-Session 请求头传递", "CART_ANONYMOUS_ID_REQUIRED");
        }
    }
}

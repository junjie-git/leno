using Leno.Cart.Application;
using Leno.Cart.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Cart.Api.Controllers;

/// <summary>
/// 匿名购物车控制器，提供无需认证的购物车端点。
/// 以会话标识（sessionId）为键管理匿名购物车，sessionId 由服务端创建时生成。
/// </summary>
[ApiController]
[Route("api/cart/anonymous")]
public sealed class AnonymousCartsController : ControllerBase
{
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

    /// <summary>获取匿名购物车（含实时价格与可售状态）。</summary>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCartAsync(string sessionId, CancellationToken ct)
    {
        var cart = await _cartAppService.GetCartAsync(sessionId, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>添加购物车项。</summary>
    [HttpPost("{sessionId}/items")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItemAsync(string sessionId, [FromBody] AddCartItemDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.AddItemAsync(sessionId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>更新购物车项数量。</summary>
    [HttpPut("{sessionId}/items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateQuantityAsync(string sessionId, Guid skuId, [FromBody] UpdateCartItemQuantityDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.UpdateQuantityAsync(sessionId, skuId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>移除购物车项。</summary>
    [HttpDelete("{sessionId}/items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItemAsync(string sessionId, Guid skuId, CancellationToken ct)
    {
        var cart = await _cartAppService.RemoveItemAsync(sessionId, skuId, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>批量选中/取消选中购物车项。</summary>
    [HttpPost("{sessionId}/items/select")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectItemsAsync(string sessionId, [FromBody] SelectCartItemsDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.SelectItemsAsync(sessionId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>结算预览（按卖家分组返回选中项，含价格试算）。</summary>
    [HttpPost("{sessionId}/preview")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewCheckoutAsync(string sessionId, CancellationToken ct)
    {
        var preview = await _cartAppService.PreviewCheckoutAsync(sessionId, ct);
        return Ok(ApiResponse.Success(preview));
    }
}
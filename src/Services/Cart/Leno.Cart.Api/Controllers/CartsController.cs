using Leno.Cart.Application;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Cart.Api.Controllers;

/// <summary>
/// 购物车控制器，提供购物车查询、添加/修改/删除项、选中、结算预览与合并端点。
/// 全部端点需买家角色认证，仅可操作自身购物车。
/// </summary>
[Authorize(Roles = "Buyer")]
[ApiController]
[Route("api/cart")]
public sealed class CartsController : CartControllerBase
{
    private readonly ICartAppService _cartAppService;

    public CartsController(ICurrentUserContext currentUser, ICartAppService cartAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(cartAppService);
        _cartAppService = cartAppService;
    }

    /// <summary>获取当前买家购物车（含实时价格与可售状态）。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCartAsync(CancellationToken ct)
    {
        var cart = await _cartAppService.GetCartAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>添加购物车项。</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItemAsync([FromBody] AddCartItemDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.AddItemAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>更新购物车项数量。</summary>
    [HttpPut("items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateQuantityAsync(Guid skuId, [FromBody] UpdateCartItemQuantityDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.UpdateQuantityAsync(GetCurrentUserId(), skuId, dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>移除购物车项。</summary>
    [HttpDelete("items/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItemAsync(Guid skuId, CancellationToken ct)
    {
        var cart = await _cartAppService.RemoveItemAsync(GetCurrentUserId(), skuId, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>批量选中/取消选中购物车项。</summary>
    [HttpPost("items/select")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectItemsAsync([FromBody] SelectCartItemsDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.SelectItemsAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(cart));
    }

    /// <summary>结算预览（按卖家分组返回选中项，含价格试算）。</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewCheckoutAsync(CancellationToken ct)
    {
        var preview = await _cartAppService.PreviewCheckoutAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(preview));
    }

    /// <summary>登录时合并匿名购物车。</summary>
    [HttpPost("merge")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MergeAsync([FromBody] MergeCartRequestDto dto, CancellationToken ct)
    {
        var cart = await _cartAppService.MergeAnonymousCartAsync(GetCurrentUserId(), dto.AnonymousId, ct);
        return Ok(ApiResponse.Success(cart));
    }
}
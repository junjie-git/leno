using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 商品收藏控制器，提供收藏增删查、批量取消与计数端点。
/// 全部端点需 Buyer 角色认证；用户隔离由应用层按 userId 强制过滤。
/// 端点契约对齐 docs/design-prompts/buyer-app/13-profile/favorites.md。
/// </summary>
[Authorize(Roles = "Buyer")]
[ApiController]
[Route("api/users/me/favorites")]
public sealed class FavoritesController : UserAuthControllerBase
{
    private readonly IFavoritesAppService _appService;

    public FavoritesController(ICurrentUserContext currentUser, IFavoritesAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>分页查询当前用户收藏列表。支持 sort（comprehensive/price/sales/created）与 order（asc/desc）。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FavoriteDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] FavoriteQueryDto query, CancellationToken ct)
    {
        var result = await _appService.ListAsync(GetCurrentUserId(), query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>收藏商品。幂等：已收藏同一 SPU 返回成功。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<FavoriteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAsync([FromBody] AddFavoriteDto dto, CancellationToken ct)
    {
        var result = await _appService.AddAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>取消收藏单个 SPU。幂等：未收藏视为成功。</summary>
    [HttpDelete("{spuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAsync(Guid spuId, CancellationToken ct)
    {
        await _appService.RemoveAsync(GetCurrentUserId(), spuId, ct);
        return Ok(ApiResponse.Success("已取消收藏"));
    }

    /// <summary>批量取消收藏。返回实际删除条数。</summary>
    [HttpPost("batch-delete")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteFavoritesDto dto, CancellationToken ct)
    {
        var deleted = await _appService.BatchDeleteAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(deleted));
    }

    /// <summary>查询当前用户收藏总数（用于「我的」页角标）。</summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(ApiResponse<FavoriteCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CountAsync(CancellationToken ct)
    {
        var result = await _appService.CountAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }
}

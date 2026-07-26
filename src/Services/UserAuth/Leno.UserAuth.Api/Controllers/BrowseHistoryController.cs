using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 浏览历史控制器，提供浏览历史记录、查询、删除、批量删除与清空端点。
/// 全部端点需 Buyer 角色认证；用户隔离由应用层按 userId 强制过滤。
/// 端点契约对齐 docs/design-prompts/buyer-app/13-profile/history.md。
/// </summary>
[Authorize(Roles = "Buyer")]
[ApiController]
[Route("api/users/me/browse-history")]
public sealed class BrowseHistoryController : UserAuthControllerBase
{
    private readonly IBrowseHistoryAppService _appService;

    public BrowseHistoryController(ICurrentUserContext currentUser, IBrowseHistoryAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>分页查询当前用户浏览历史（按浏览时间倒序）。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BrowseHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] BrowseHistoryQueryDto query, CancellationToken ct)
    {
        var result = await _appService.ListAsync(GetCurrentUserId(), query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>记录浏览历史。幂等：相同 SPU 5 秒内仅更新浏览时间，不新增记录。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BrowseHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAsync([FromBody] AddBrowseHistoryDto dto, CancellationToken ct)
    {
        var result = await _appService.AddAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>删除单条浏览历史。仅可删除归属当前用户的记录。</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAsync(Guid id, CancellationToken ct)
    {
        await _appService.RemoveAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success("已删除"));
    }

    /// <summary>批量删除浏览历史。返回实际删除条数。</summary>
    [HttpPost("batch-delete")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteBrowseHistoryDto dto, CancellationToken ct)
    {
        var deleted = await _appService.BatchDeleteAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(deleted));
    }

    /// <summary>清空当前用户全部浏览历史。返回实际删除条数。</summary>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearAllAsync(CancellationToken ct)
    {
        var deleted = await _appService.ClearAllAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(deleted));
    }
}

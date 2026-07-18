using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 用户域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护。
/// </summary>
[ApiController]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IUserInternalQueryService _queryService;

    public InternalUsersController(IUserInternalQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    [HttpGet("internal/v1/users/{userId:guid}/contacts")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpGet("internal/users/{userId:guid}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<UserContactsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactsAsync(Guid userId, CancellationToken ct)
    {
        var result = await _queryService.GetContactsAsync(userId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "用户不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }
}

using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 用户域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护（/internal 路径前缀）。
/// 默认返回脱敏联系方式（<see cref="UserContactsMaskedDto"/>）；
/// 完整 PII（<see cref="UserContactsDto"/>）须显式调用 /contacts/full 端点并携带 <c>X-Internal-Key</c> 头（fail-closed）。
/// </summary>
[ApiController]
public sealed class InternalUsersController : ControllerBase
{
    private const string InternalKeyHeader = "X-Internal-Key";

    private readonly IUserInternalQueryService _queryService;

    public InternalUsersController(IUserInternalQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    /// <summary>
    /// 默认内部查询：返回脱敏联系方式（手机号前 3 后 4，邮箱首字符 + 域名）。
    /// 即使 InternalApiKeyMiddleware 在开发环境被跳过，响应也不泄露完整 PII。
    /// </summary>
    [HttpGet("internal/v1/users/{userId:guid}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<UserContactsMaskedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactsAsync(Guid userId, CancellationToken ct)
    {
        var result = await _queryService.GetMaskedContactsAsync(userId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "用户不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 完整 PII 查询：返回未脱敏联系方式。
    /// Fail-closed：要求请求携带 <c>X-Internal-Key</c> 头（由 InternalApiKeyMiddleware 校验），
    /// 头缺失表示中间件被跳过或请求未鉴权，直接返回 403。
    /// 下游需要完整 PII 的服务（如 Notification 发送短信/邮件）应调用本端点并携带内部 Key。
    /// </summary>
    [HttpGet("internal/v1/users/{userId:guid}/contacts/full")]
    [ProducesResponseType(typeof(ApiResponse<UserContactsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullContactsAsync(Guid userId, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(InternalKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail(StatusCodes.Status403Forbidden, "完整 PII 查询需要 X-Internal-Key 鉴权"));
        }

        var result = await _queryService.GetContactsAsync(userId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "用户不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }
}

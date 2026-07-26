using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 用户域内部查询控制器（Identity BC，Task A4 新建 2 端点）。
/// <para>
/// 供其他微服务（如 Notification / Order）获取用户联系方式。
/// 类级无 <c>[Route]</c>，方法级路径以 <c>internal/v1/users/...</c> 开头，
/// 受 <c>InternalApiKeyMiddleware</c> 在路径前缀层统一保护。
/// </para>
/// <para>
/// 安全策略：
/// <list type="bullet">
/// <item>默认脱敏端点 <c>/contacts</c>：返回 <see cref="UserContactsMaskedDto"/>，手机号前 3 后 4，邮箱首字符 + 域名。</item>
/// <item>完整 PII 端点 <c>/contacts/full</c>：<b>fail-closed</b>，必须携带 <c>X-Internal-Key</c> 头，
/// 否则 Controller 直接返回 403，防止中间件被绕过或开发环境配置错误导致 PII 泄露。</item>
/// </list>
/// </para>
/// <para>
/// Identity 改用 <see cref="IUserInternalAppService"/>（旧域 UserAuth 用 IUserInternalQueryService），
/// 接口签名 <c>GetContactsAsync</c> 返回 <see cref="UserContactsMaskedDto"/>，
/// <c>GetFullContactsAsync</c> 返回 <see cref="UserContactsDto"/>，用户不存在抛 <c>IdentityDomainException</c>。
/// </para>
/// </summary>
[ApiController]
public sealed class InternalUsersController : ControllerBase
{
    /// <summary>内部 PII 鉴权请求头名称。</summary>
    private const string InternalKeyHeader = "X-Internal-Key";

    private readonly IUserInternalAppService _userInternalAppService;

    public InternalUsersController(IUserInternalAppService userInternalAppService)
    {
        _userInternalAppService = userInternalAppService ?? throw new ArgumentNullException(nameof(userInternalAppService));
    }

    /// <summary>
    /// 默认内部查询：返回脱敏后的联系方式（手机号前 3 后 4，邮箱首字符 + 域名）。
    /// 即使 <c>InternalApiKeyMiddleware</c> 在开发环境被跳过，响应也不泄露完整 PII。
    /// 用户不存在由 Service 层抛 <c>IdentityDomainException</c>，全局异常中间件映射为 404。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回脱敏联系方式。</response>
    /// <response code="404">用户不存在。</response>
    [HttpGet("internal/v1/users/{userId:guid}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<UserContactsMaskedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactsAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _userInternalAppService.GetContactsAsync(userId, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 完整 PII 查询：返回未脱敏的联系方式。
    /// <para>
    /// <b>Fail-closed</b>：必须携带 <c>X-Internal-Key</c> 头，缺失或为空时 Controller 直接返回 403，
    /// 防止 <c>InternalApiKeyMiddleware</c> 被绕过或开发环境配置错误导致 PII 泄露。
    /// </para>
    /// <para>
    /// 下游需要完整 PII 的服务（如 Notification 发送短信/邮件）应调用本端点并携带内部 Key。
    /// 用户不存在由 Service 层抛 <c>IdentityDomainException</c>，全局异常中间件映射为 404。
    /// </para>
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回完整联系方式。</response>
    /// <response code="403">未携带 X-Internal-Key 头，fail-closed 拒绝。</response>
    /// <response code="404">用户不存在。</response>
    [HttpGet("internal/v1/users/{userId:guid}/contacts/full")]
    [ProducesResponseType(typeof(ApiResponse<UserContactsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullContactsAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(InternalKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse.Fail(StatusCodes.Status403Forbidden, "完整 PII 查询需要 X-Internal-Key 鉴权"));
        }

        var result = await _userInternalAppService.GetFullContactsAsync(userId, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(result));
    }
}

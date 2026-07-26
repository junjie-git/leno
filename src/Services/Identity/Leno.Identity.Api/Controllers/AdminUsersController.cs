using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 用户管理后台控制器（Identity BC，Task A4 新建 5 端点）。
/// <para>
/// 提供用户分页查询、详情查询、角色分配与账户状态管理端点。
/// 仅 <c>Operator</c> 与 <c>Admin</c> 角色可访问；写操作经审计拦截器在事务内写入审计日志。
/// </para>
/// <para>
/// 统一使用 <see cref="ApiResponse{T}"/> 包装响应；POST 写操作返回 200 OK（不用 201/204）。
/// Identity 接口签名不传 currentUserId，roleIds 是 <see cref="List{Guid}"/>，与旧域 UserAuth 不同。
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserAdminAppService _userAdminAppService;

    public AdminUsersController(
        ICurrentUserContext currentUser,
        IUserAdminAppService userAdminAppService)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _userAdminAppService = userAdminAppService ?? throw new ArgumentNullException(nameof(userAdminAppService));
    }

    /// <summary>
    /// 分页查询用户列表。
    /// 查询参数（Keyword / Status / Page / PageSize）通过 [FromQuery] 绑定到 <see cref="AdminUserQueryDto"/>。
    /// </summary>
    /// <param name="query">查询参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回分页结果。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="403">角色无权访问。</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryUsersAsync([FromQuery] AdminUserQueryDto query, CancellationToken ct)
    {
        var result = await _userAdminAppService
            .QueryUsersAsync(query, query.Page, query.PageSize, ct)
            .ConfigureAwait(false);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询用户详情。</summary>
    /// <param name="id">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回用户详情。</response>
    /// <response code="404">用户不存在。</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAsync([FromRoute] Guid id, CancellationToken ct)
    {
        var user = await _userAdminAppService.GetUserAsync(id, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(user));
    }

    /// <summary>
    /// 为用户分配角色（幂等，覆盖式分配）。
    /// 角色变更后由 Service 层撤销该用户所有刷新令牌，使变更立即生效。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="request">角色分配请求，含 RoleIds 列表。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">角色已分配。</response>
    /// <response code="404">用户不存在。</response>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRolesAsync(
        [FromRoute] Guid id,
        [FromBody] AssignRolesRequest request,
        CancellationToken ct)
    {
        await _userAdminAppService.AssignRolesAsync(id, request.RoleIds, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("角色已分配"));
    }

    /// <summary>锁定用户账户。</summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="dto">锁定请求（含原因与时长）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">账户已锁定。</response>
    /// <response code="404">用户不存在。</response>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendAsync(
        [FromRoute] Guid id,
        [FromBody] SuspendUserDto dto,
        CancellationToken ct)
    {
        await _userAdminAppService.SuspendAsync(id, dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("账户已锁定"));
    }

    /// <summary>解锁或恢复用户账户为 Active。</summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">账户已恢复。</response>
    /// <response code="404">用户不存在。</response>
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeAsync([FromRoute] Guid id, CancellationToken ct)
    {
        await _userAdminAppService.ResumeAsync(id, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("账户已恢复"));
    }
}

/// <summary>
/// 角色分配请求 DTO（Identity BC，Task A4）。
/// Identity 域未在 Application 层定义独立的 AssignRolesDto，Controller 层内联声明以匹配请求体结构。
/// </summary>
public sealed class AssignRolesRequest
{
    /// <summary>角色标识列表（覆盖式分配）。</summary>
    public List<Guid> RoleIds { get; init; } = new();
}

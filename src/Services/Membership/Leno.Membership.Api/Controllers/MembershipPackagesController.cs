using Leno.Infrastructure.Auth;
using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员套餐控制器（买家端）。
/// 路由 /api/membership-packages/*，需 Buyer 角色。
/// 对应 design-prompts operations/08-membership-ops/membership-packages.md 的 2 个买家端端点：
/// 查询可购买套餐列表、订阅套餐（新建）。
/// </summary>
[ApiController]
[Route("api/membership-packages")]
[Authorize(Roles = "Buyer")]
public sealed class MembershipPackagesController : ControllerBase
{
    private readonly IMembershipPackageAppService _packageAppService;
    private readonly ICurrentUserContext _currentUser;

    public MembershipPackagesController(
        IMembershipPackageAppService packageAppService,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(packageAppService);
        ArgumentNullException.ThrowIfNull(currentUser);
        _packageAppService = packageAppService;
        _currentUser = currentUser;
    }

    /// <summary>查询全部已启用的会员套餐，供买家购买页展示。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<MembershipPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var packages = await _packageAppService.GetPackagesAsync(ct);
        return Ok(ApiResponse.Success(packages));
    }

    /// <summary>买家订阅会员套餐，生成待支付订阅意图，实际订单创建转发至订单域。</summary>
    /// <param name="id">套餐标识，由路由参数传入。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpPost("{id:guid}/subscribe")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubscribeAsync([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _packageAppService.SubscribeAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success(result));
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return _currentUser.UserId.Value;
    }
}

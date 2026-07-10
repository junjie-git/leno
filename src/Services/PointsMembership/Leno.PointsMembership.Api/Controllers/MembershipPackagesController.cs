using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 会员套餐控制器。
/// 买家端（/api/membership-packages）：套餐列表与订阅，需 Buyer 角色。
/// 运营端（/api/admin/membership-packages）：套餐 CRUD 与启停，需 Operator/Admin 角色。
/// </summary>
[ApiController]
public sealed class MembershipPackagesController : PointsMembershipControllerBase
{
    private readonly IMembershipPackageAppService _packageAppService;

    public MembershipPackagesController(
        ICurrentUserContext currentUser,
        IMembershipPackageAppService packageAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(packageAppService);
        _packageAppService = packageAppService;
    }

    // ========== 买家端 ==========

    /// <summary>查询可购买的会员套餐列表。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/membership-packages")]
    [ProducesResponseType(typeof(ApiResponse<List<MembershipPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackagesAsync(CancellationToken ct)
    {
        var packages = await _packageAppService.GetPackagesAsync(ct);
        return Ok(ApiResponse.Success(packages));
    }

    /// <summary>订阅会员套餐，创建待支付的用户会员权益记录。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/membership-packages/{packageId:guid}/subscribe")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubscribeAsync(Guid packageId, CancellationToken ct)
    {
        await _packageAppService.SubscribeAsync(GetCurrentUserId(), packageId, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 运营端 ==========

    /// <summary>创建会员套餐。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/membership-packages")]
    [ProducesResponseType(typeof(ApiResponse<MembershipPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePackageAsync([FromBody] CreateMembershipPackageDto dto, CancellationToken ct)
    {
        var package = await _packageAppService.CreatePackageAsync(dto, ct);
        return Ok(ApiResponse.Success(package));
    }

    /// <summary>更新会员套餐（名称、价格、时长、权益）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/membership-packages/{packageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MembershipPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePackageAsync(Guid packageId, [FromBody] UpdateMembershipPackageDto dto, CancellationToken ct)
    {
        var package = await _packageAppService.UpdatePackageAsync(packageId, dto, ct);
        return Ok(ApiResponse.Success(package));
    }

    /// <summary>启用会员套餐。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/membership-packages/{packageId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnablePackageAsync(Guid packageId, CancellationToken ct)
    {
        await _packageAppService.EnablePackageAsync(packageId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用会员套餐。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/membership-packages/{packageId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisablePackageAsync(Guid packageId, CancellationToken ct)
    {
        await _packageAppService.DisablePackageAsync(packageId, ct);
        return Ok(ApiResponse.Success());
    }
}

using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员套餐运营控制器（运营端）。
/// 路由 /api/admin/membership-packages/*，需 Operator/Admin 角色。
/// 对应 design-prompts operations/08-membership-ops/membership-packages.md 的 4 个运营端端点：
/// 创建套餐、更新套餐、启用套餐、停用套餐。
/// 返工：路径从 api/membershippackages 改连字符 + api/admin/ 前缀，鉴权从 Policy AdminOnly 改角色 RBAC，
/// 响应统一 ApiResponse 包装，创建/启停返回 200 OK（不用 201 CreatedAtAction、不用 204 NoContent）。
/// </summary>
[ApiController]
[Route("api/admin/membership-packages")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminMembershipPackagesController : ControllerBase
{
    private readonly IMembershipPackageAppService _packageAppService;

    public AdminMembershipPackagesController(IMembershipPackageAppService packageAppService)
    {
        ArgumentNullException.ThrowIfNull(packageAppService);
        _packageAppService = packageAppService;
    }

    /// <summary>创建会员套餐。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MembershipPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMembershipPackageDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var package = await _packageAppService.CreatePackageAsync(dto, ct);
        return Ok(ApiResponse.Success(package));
    }

    /// <summary>更新会员套餐（名称、价格、时长、权益，等级编号不可改）。</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MembershipPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateMembershipPackageDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var package = await _packageAppService.UpdatePackageAsync(id, dto, ct);
        return Ok(ApiResponse.Success(package));
    }

    /// <summary>启用会员套餐，已启用返回 409。</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync([FromRoute] Guid id, CancellationToken ct)
    {
        await _packageAppService.EnablePackageAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用会员套餐，停用后不可购买，已停用返回 409。</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync([FromRoute] Guid id, CancellationToken ct)
    {
        await _packageAppService.DisablePackageAsync(id, ct);
        return Ok(ApiResponse.Success());
    }
}

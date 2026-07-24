using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员套餐管理接口（运营端 CRUD + 买家查询）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class MembershipPackagesController : ControllerBase
{
    private readonly IMembershipPackageAppService _packageAppService;

    public MembershipPackagesController(IMembershipPackageAppService packageAppService)
    {
        _packageAppService = packageAppService;
    }

    /// <summary>
    /// 获取全部已启用的会员套餐，供买家购买页展示。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MembershipPackageDto>>> GetPackages(CancellationToken ct)
        => Ok(await _packageAppService.GetPackagesAsync(ct));

    /// <summary>
    /// 创建会员套餐（运营端）。
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MembershipPackageDto>> CreatePackage(
        [FromBody] CreateMembershipPackageDto dto, CancellationToken ct)
    {
        var package = await _packageAppService.CreatePackageAsync(dto, ct);
        return CreatedAtAction(nameof(GetPackages), new { }, package);
    }

    /// <summary>
    /// 更新会员套餐（运营端，等级编号不可改）。
    /// </summary>
    [HttpPut("{packageId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MembershipPackageDto>> UpdatePackage(
        Guid packageId, [FromBody] UpdateMembershipPackageDto dto, CancellationToken ct)
        => Ok(await _packageAppService.UpdatePackageAsync(packageId, dto, ct));

    /// <summary>
    /// 启用套餐（运营端）。
    /// </summary>
    [HttpPost("{packageId:guid}/enable")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> EnablePackage(Guid packageId, CancellationToken ct)
    {
        await _packageAppService.EnablePackageAsync(packageId, ct);
        return NoContent();
    }

    /// <summary>
    /// 停用套餐（运营端）。
    /// </summary>
    [HttpPost("{packageId:guid}/disable")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DisablePackage(Guid packageId, CancellationToken ct)
    {
        await _packageAppService.DisablePackageAsync(packageId, ct);
        return NoContent();
    }
}

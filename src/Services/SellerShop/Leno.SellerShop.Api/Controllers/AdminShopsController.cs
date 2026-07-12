using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 运营端店铺管理控制器，提供店铺分页查询、审核、状态管理及资质管理端点。
/// 仅 Admin/Operator 角色可访问。
/// </summary>
[Authorize(Roles = "Admin,Operator")]
[ApiController]
[Route("api/admin/shops")]
public sealed class AdminShopsController : SellerShopControllerBase
{
    private readonly IShopAppService _shopAppService;

    public AdminShopsController(ICurrentUserContext currentUser, IShopAppService shopAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(shopAppService);
        _shopAppService = shopAppService;
    }

    /// <summary>分页查询店铺列表，支持按状态过滤与关键词模糊匹配。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ShopDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryShopsAsync([FromQuery] AdminShopQueryDto query, CancellationToken ct)
    {
        var result = await _shopAppService.QueryShopsAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询店铺详情。</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ShopDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShopAsync(Guid id, CancellationToken ct)
    {
        var shop = await _shopAppService.GetShopInfoAsync(id, ct);
        return Ok(ApiResponse.Success(shop));
    }

    /// <summary>审核通过店铺入驻申请。</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken ct)
    {
        await _shopAppService.ApproveShopApplicationAsync(id, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("店铺已审核通过"));
    }

    /// <summary>驳回店铺入驻申请。</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(Guid id, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _shopAppService.RejectShopApplicationAsync(id, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success("店铺已驳回"));
    }

    /// <summary>暂停店铺营业。</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuspendAsync(Guid id, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _shopAppService.SuspendShopAsync(id, dto, ct);
        return Ok(ApiResponse.Success("店铺已暂停"));
    }

    /// <summary>恢复店铺营业。</summary>
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeAsync(Guid id, CancellationToken ct)
    {
        await _shopAppService.ResumeShopAsync(id, ct);
        return Ok(ApiResponse.Success("店铺已恢复"));
    }

    /// <summary>关闭店铺（终态）。</summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseAsync(Guid id, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _shopAppService.CloseShopAsync(id, dto, ct);
        return Ok(ApiResponse.Success("店铺已关闭"));
    }

    /// <summary>查询店铺资质列表。</summary>
    [HttpGet("{id:guid}/qualifications")]
    [ProducesResponseType(typeof(ApiResponse<List<QualificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQualificationsAsync(Guid id, CancellationToken ct)
    {
        var qualifications = await _shopAppService.GetQualificationsAsync(id, ct);
        return Ok(ApiResponse.Success(qualifications));
    }

    /// <summary>审核通过资质。</summary>
    [HttpPost("{id:guid}/qualifications/{qualId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveQualificationAsync(Guid id, Guid qualId, CancellationToken ct)
    {
        await _shopAppService.ApproveQualificationAsync(id, qualId, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("资质已审核通过"));
    }

    /// <summary>驳回资质。</summary>
    [HttpPost("{id:guid}/qualifications/{qualId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectQualificationAsync(Guid id, Guid qualId, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _shopAppService.RejectQualificationAsync(id, qualId, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success("资质已驳回"));
    }
}
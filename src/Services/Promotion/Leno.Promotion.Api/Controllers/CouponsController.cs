using Leno.Infrastructure.Auth;
using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Promotion.Api.Controllers;

/// <summary>
/// 优惠券控制器。
/// 运营端（/api/admin/coupons）：券模板 CRUD、启停、发放，需 Operator/Admin 角色。
/// 买家端（/api/coupons）：可领券列表、领券、我的优惠券，需 Buyer 角色。
/// </summary>
[ApiController]
public sealed class CouponsController : PromotionControllerBase
{
    private readonly ICouponAppService _couponAppService;

    public CouponsController(ICurrentUserContext currentUser, ICouponAppService couponAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(couponAppService);
        _couponAppService = couponAppService;
    }

    // ========== 运营端 ==========

    /// <summary>创建优惠券模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/coupons")]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCouponDto dto, CancellationToken ct)
    {
        var coupon = await _couponAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(coupon));
    }

    /// <summary>更新优惠券模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/coupons/{couponId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid couponId, [FromBody] UpdateCouponDto dto, CancellationToken ct)
    {
        var coupon = await _couponAppService.UpdateAsync(couponId, dto, ct);
        return Ok(ApiResponse.Success(coupon));
    }

    /// <summary>启用优惠券模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/coupons/{couponId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid couponId, CancellationToken ct)
    {
        await _couponAppService.EnableAsync(couponId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用优惠券模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/coupons/{couponId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid couponId, CancellationToken ct)
    {
        await _couponAppService.DisableAsync(couponId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>批量发放优惠券（增加发放量）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/coupons/{couponId:guid}/issue")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueAsync(Guid couponId, [FromQuery] int quantity, CancellationToken ct)
    {
        await _couponAppService.IssueAsync(couponId, quantity, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// 分页查询优惠券模板，支持按名称模糊、类型精确、状态精确可选过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词。</param>
    /// <param name="type">券类型精确匹配。</param>
    /// <param name="status">券模板状态精确匹配。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页券模板列表与总记录数。</returns>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/coupons")]
    [ProducesResponseType(typeof(ApiResponse<CouponListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? name,
        [FromQuery] CouponType? type,
        [FromQuery] CouponTemplateStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _couponAppService.QueryAsync(name, type, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 买家端 ==========

    /// <summary>查询可领取的优惠券列表。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/coupons/available")]
    [ProducesResponseType(typeof(ApiResponse<List<CouponDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableAsync(CancellationToken ct)
    {
        var coupons = await _couponAppService.GetReceivableAsync(ct);
        return Ok(ApiResponse.Success(coupons));
    }

    /// <summary>领取优惠券。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/coupons/{couponId:guid}/receive")]
    [ProducesResponseType(typeof(ApiResponse<UserCouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveAsync(Guid couponId, [FromQuery] string source = "Manual", CancellationToken ct = default)
    {
        var userCoupon = await _couponAppService.ReceiveAsync(GetCurrentUserId(), couponId, source, ct);
        return Ok(ApiResponse.Success(userCoupon));
    }

    /// <summary>查询我的优惠券（按状态可选过滤）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/coupons/mine")]
    [ProducesResponseType(typeof(ApiResponse<List<UserCouponDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCouponsAsync([FromQuery] CouponStatus? status, CancellationToken ct = default)
    {
        var userCoupons = await _couponAppService.GetMyCouponsAsync(GetCurrentUserId(), status, ct);
        return Ok(ApiResponse.Success(userCoupons));
    }
}

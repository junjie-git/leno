using Leno.AfterSales.Application;
using Leno.AfterSales.Application.DTOs;
using Leno.AfterSales.Domain.ValueObjects;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.AfterSales.Api.Controllers;

/// <summary>
/// 管理员售后控制器（售后 BC 独立维护）。
/// 端点：分页查询全平台售后单、审核通过、驳回。
/// 全部端点需 Operator 或 Admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminAfterSalesController : AfterSalesControllerBase
{
    private readonly IAfterSalesAppService _afterSalesAppService;

    public AdminAfterSalesController(
        ICurrentUserContext currentUser,
        IAfterSalesAppService afterSalesAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(afterSalesAppService);
        _afterSalesAppService = afterSalesAppService;
    }

    /// <summary>运营分页查询全平台售后单。</summary>
    [HttpGet("api/admin/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAfterSalesAsync(
        [FromQuery] Guid? orderId,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? sellerId,
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _afterSalesAppService.QueryAsync(orderId, userId, sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>运营审核通过售后。</summary>
    [HttpPost("api/admin/after-sales/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminApproveAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.AdminApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营驳回售后。</summary>
    [HttpPost("api/admin/after-sales/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminRejectAfterSalesAsync(Guid id, [FromBody] RejectAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.AdminRejectAfterSalesAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }
}

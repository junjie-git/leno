using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 对账管理控制器。
/// GET /api/admin/reconciliation/diffs：分页查询对账差异
/// POST /api/admin/reconciliation/trigger：手动触发对账
/// 需 Admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reconciliation")]
public sealed class ReconciliationController : PaymentControllerBase
{
    private readonly IReconciliationAppService _reconciliationAppService;

    public ReconciliationController(
        ICurrentUserContext currentUser,
        IReconciliationAppService reconciliationAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reconciliationAppService);
        _reconciliationAppService = reconciliationAppService;
    }

    /// <summary>分页查询对账差异列表。</summary>
    [HttpGet("diffs")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationDiffListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryDiffsAsync(
        [FromQuery] DateTime? billDate,
        [FromQuery] PaymentChannel? channel,
        [FromQuery] ReconciliationDiffType? diffType,
        [FromQuery] ReconciliationDiffStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _reconciliationAppService.QueryDiffsAsync(
            billDate, channel, diffType, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>手动触发对账（指定日期）。</summary>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerReconciliationAsync(
        [FromQuery] DateTime? billDate,
        CancellationToken ct)
    {
        var date = billDate ?? DateTime.UtcNow.Date.AddDays(-1);
        await _reconciliationAppService.TriggerReconciliationAsync(date, ct);
        return Ok(ApiResponse.Success());
    }
}
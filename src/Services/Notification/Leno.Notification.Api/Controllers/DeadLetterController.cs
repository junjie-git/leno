using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 死信管理控制器（运营端：列表查询、批量重发、批量丢弃）。
/// </summary>
[ApiController]
public sealed class DeadLetterController : NotificationControllerBase
{
    private readonly IDeadLetterAppService _deadLetterAppService;

    public DeadLetterController(ICurrentUserContext currentUser, IDeadLetterAppService deadLetterAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(deadLetterAppService);
        _deadLetterAppService = deadLetterAppService;
    }

    /// <summary>分页查询死信列表。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/dead-letters")]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeadLettersAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _deadLetterAppService.GetDeadLettersAsync(page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>批量重发死信通知。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dead-letters/batch-resend")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchResendAsync([FromBody] BatchDeadLetterRequestDto request, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        var result = await _deadLetterAppService.BatchResendAsync(operatorId, request, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>批量丢弃死信通知。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/dead-letters/batch-discard")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchDiscardAsync([FromBody] BatchDeadLetterRequestDto request, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        var result = await _deadLetterAppService.BatchDiscardAsync(operatorId, request, ct);
        return Ok(ApiResponse.Success(result));
    }
}
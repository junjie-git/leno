using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 死信消息管理控制器（死信队列查看、重投与丢弃）。
/// </summary>
[ApiController]
[Route("api/admin/dead-letters")]
[Authorize(Roles = "Operator,Admin")]
public sealed class DeadLetterController : SystemAdminControllerBase
{
    private readonly IDeadLetterAppService _deadLetterAppService;

    public DeadLetterController(ICurrentUserContext currentUser, IDeadLetterAppService deadLetterAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(deadLetterAppService);
        _deadLetterAppService = deadLetterAppService;
    }

    /// <summary>分页查询死信消息，支持来源上下文与状态过滤。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? sourceContext,
        [FromQuery] DeadLetterStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _deadLetterAppService.QueryAsync(sourceContext, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取死信消息详情。</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _deadLetterAppService.GetByIdAsync(id, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(404, "死信消息不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>重投指定死信消息（幂等：已重投返回当前状态）。</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryAsync(Guid id, CancellationToken ct)
    {
        var operatorId = GetCurrentOperatorId().ToString();
        await _deadLetterAppService.RetryAsync(id, operatorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>丢弃指定死信消息（reason 必填）。</summary>
    [HttpPost("{id:guid}/discard")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DiscardAsync(Guid id, [FromBody] DiscardDeadLetterDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var operatorId = GetCurrentOperatorId().ToString();
        await _deadLetterAppService.DiscardAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>批量重投死信消息。</summary>
    [HttpPost("batch-retry")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchRetryAsync([FromBody] BatchOperationDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var operatorId = GetCurrentOperatorId().ToString();
        var result = await _deadLetterAppService.BatchRetryAsync(dto.MessageIds, operatorId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>批量丢弃死信消息。</summary>
    [HttpPost("batch-discard")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchDiscardAsync([FromBody] BatchDiscardDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var operatorId = GetCurrentOperatorId().ToString();
        var result = await _deadLetterAppService.BatchDiscardAsync(dto.MessageIds, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success(result));
    }
}

/// <summary>
/// 批量丢弃 DTO（含丢弃原因）。
/// </summary>
public sealed class BatchDiscardDto
{
    public List<Guid> MessageIds { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}
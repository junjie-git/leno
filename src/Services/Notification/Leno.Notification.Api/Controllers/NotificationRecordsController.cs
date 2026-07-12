using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知记录查询与投递统计控制器（管理员端）。
/// </summary>
[ApiController]
public sealed class NotificationRecordsController : NotificationControllerBase
{
    private readonly INotificationRecordAppService _recordAppService;
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationRecordsController> _logger;

    public NotificationRecordsController(
        ICurrentUserContext currentUser,
        INotificationRecordAppService recordAppService,
        INotificationRecordRepository recordRepository,
        IEnumerable<INotificationChannel> channels,
        IUnitOfWork unitOfWork,
        ILogger<NotificationRecordsController> logger)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(recordAppService);
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordAppService = recordAppService;
        _recordRepository = recordRepository;
        _channels = channels;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>多维度分页查询通知记录。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/notifications/records")]
    [ProducesResponseType(typeof(ApiResponse<NotificationRecordListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryRecordsAsync(
        [FromQuery] Guid? userId,
        [FromQuery] NotificationChannel? channel,
        [FromQuery] NotificationStatus? status,
        [FromQuery] string? templateCode,
        [FromQuery] string? businessRef,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _recordAppService.QueryRecordsAsync(
            userId, channel, status, templateCode, businessRef, from, to, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取通知记录详情。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/notifications/records/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NotificationRecordDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecordByIdAsync(Guid id, CancellationToken ct)
    {
        var record = await _recordAppService.GetRecordByIdAsync(id, ct);
        if (record is null)
        {
            return NotFound(ApiResponse.Fail(404, $"通知记录 {id} 不存在"));
        }

        return Ok(ApiResponse.Success(record));
    }

    /// <summary>按业务引用标识查询通知记录。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/notifications/records/by-business/{businessRef}")]
    [ProducesResponseType(typeof(ApiResponse<List<NotificationRecordListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBusinessRefAsync(string businessRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(businessRef))
        {
            return BadRequest(ApiResponse.Fail(400, "业务引用标识不可为空"));
        }

        var records = await _recordAppService.GetRecordsByBusinessRefAsync(businessRef, ct);
        return Ok(ApiResponse.Success(records));
    }

    /// <summary>手工重发死信通知记录。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notifications/records/{id:guid}/resend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendRecordAsync(Guid id, CancellationToken ct)
    {
        var record = await _recordRepository.GetByIdAsync(id, ct);
        if (record is null)
        {
            return NotFound(ApiResponse.Fail(404, $"通知记录 {id} 不存在"));
        }

        if (record.Status != NotificationStatus.DeadLettered)
        {
            return BadRequest(ApiResponse.Fail(400, $"记录 {id} 非死信状态，无法重发"));
        }

        var channel = _channels.FirstOrDefault(c => c.Channel == record.Channel);
        if (channel is null)
        {
            return BadRequest(ApiResponse.Fail(400, $"找不到渠道 {record.Channel} 的实现"));
        }

        // 重发
        record.MarkResend();
        await _recordRepository.UpdateAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var operatorId = GetCurrentUserId();
        _logger.LogInformation("操作员 {OperatorId} 手工重发死信 RecordId={RecordId}", operatorId, id);

        return Ok(ApiResponse.Success("死信已重新发送"));
    }

    /// <summary>获取送达率统计。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/notifications/statistics")]
    [ProducesResponseType(typeof(ApiResponse<DeliveryStatisticsListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryStatisticsAsync(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _recordAppService.GetDeliveryStatisticsAsync(from, to, ct);
        return Ok(ApiResponse.Success(result));
    }
}
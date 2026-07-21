using System.Security.Cryptography;
using System.Text;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 渠道回执回调控制器，接收邮件/短信渠道的送达回执。
/// 通过签名验证防止伪造，匹配 ChannelMessageId 更新通知记录状态。
/// </summary>
[ApiController]
public sealed class NotificationCallbacksController : ControllerBase
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationCallbacksController> _logger;

    public NotificationCallbacksController(
        INotificationRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<NotificationCallbacksController> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 邮件渠道回执回调。
    /// </summary>
    [HttpPost("api/notifications/callbacks/email")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EmailReceiptAsync([FromBody] EmailReceiptDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            return BadRequest(ApiResponse.Fail(400, "回执数据不可为空"));
        }

        // 验证签名
        if (!VerifySignature(dto.ChannelMessageId, dto.Succeeded.ToString(), dto.Timestamp.ToString(), dto.Signature))
        {
            _logger.LogWarning("邮件回执签名验证失败 ChannelMessageId={Id}", dto.ChannelMessageId);
            return Unauthorized(ApiResponse.Fail(401, "签名验证失败"));
        }

        return await ProcessReceiptAsync(dto.ChannelMessageId, dto.Succeeded, dto.RawPayload, "Email", ct);
    }

    /// <summary>
    /// 短信渠道回执回调。
    /// </summary>
    [HttpPost("api/notifications/callbacks/sms")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SmsReceiptAsync([FromBody] SmsReceiptDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            return BadRequest(ApiResponse.Fail(400, "回执数据不可为空"));
        }

        // 验证签名
        if (!VerifySignature(dto.ChannelMessageId, dto.Succeeded.ToString(), dto.Timestamp.ToString(), dto.Signature))
        {
            _logger.LogWarning("短信回执签名验证失败 ChannelMessageId={Id}", dto.ChannelMessageId);
            return Unauthorized(ApiResponse.Fail(401, "签名验证失败"));
        }

        return await ProcessReceiptAsync(dto.ChannelMessageId, dto.Succeeded, dto.RawPayload, "Sms", ct);
    }

    private async Task<IActionResult> ProcessReceiptAsync(
        string channelMessageId, bool succeeded, string? rawPayload, string channelName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelMessageId))
        {
            return BadRequest(ApiResponse.Fail(400, "渠道消息标识不可为空"));
        }

        var record = await _recordRepository.GetByChannelMessageIdAsync(channelMessageId, ct);
        if (record is null)
        {
            _logger.LogWarning("{Channel}回执未找到匹配记录 ChannelMessageId={Id}", channelName, channelMessageId);
            return NotFound(ApiResponse.Fail(404, $"未找到匹配的通知记录 ChannelMessageId={channelMessageId}"));
        }

        var applied = record.ApplyReceipt(channelMessageId, succeeded, rawPayload);
        if (!applied)
        {
            _logger.LogInformation("{Channel}回执幂等跳过 RecordId={RecordId} ChannelMessageId={Id}",
                channelName, record.Id, channelMessageId);
            return Ok(ApiResponse.Success("幂等跳过，记录已处理"));
        }

        await _recordRepository.UpdateAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("{Channel}回执已处理 RecordId={RecordId} Succeeded={Succeeded}",
            channelName, record.Id, succeeded);

        return Ok(ApiResponse.Success("回执已处理"));
    }

    private bool VerifySignature(string channelMessageId, string succeeded, string timestamp, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var secret = _configuration["Notification:CallbackSecret"] ?? "LenoNotificationCallbackSecret2024";
        var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{secret}";
        var computed = ComputeHmacSha256(raw, secret);

        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexStringLower(hash);
    }
}
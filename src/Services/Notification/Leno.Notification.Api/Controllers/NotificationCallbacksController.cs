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
    private readonly string _callbackSecret;
    private readonly ILogger<NotificationCallbacksController> _logger;

    /// <summary>
    /// 回执时间戳允许的最大时钟偏移（分钟），超出窗口视为重放攻击。
    /// </summary>
    private const double MaxTimestampSkewMinutes = 5;

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

        // 启动时校验密钥必须配置，拒绝回退到硬编码默认值
        var secret = configuration["Notification:CallbackSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Notification:CallbackSecret 未配置，拒绝启动回执端点");
        }
        _callbackSecret = secret;
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

        // 时间戳新鲜度校验：±5 分钟，防止重放攻击
        if (!long.TryParse(timestamp, out var ts))
        {
            _logger.LogWarning("回执时间戳解析失败 ChannelMessageId={Id} Timestamp={Ts}", channelMessageId, timestamp);
            return false;
        }
        var callbackTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        var skew = Math.Abs((DateTimeOffset.UtcNow - callbackTime).TotalMinutes);
        if (skew > MaxTimestampSkewMinutes)
        {
            _logger.LogWarning("回执时间戳超出窗口 Skew={Skew}min ChannelMessageId={Id}", skew, channelMessageId);
            return false;
        }

        var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{_callbackSecret}";
        var computed = ComputeHmacSha256(raw, _callbackSecret);

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
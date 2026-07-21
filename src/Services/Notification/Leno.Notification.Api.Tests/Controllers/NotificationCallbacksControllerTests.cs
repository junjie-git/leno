using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Leno.Notification.Api.Controllers;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Api.Tests.Controllers;

/// <summary>
/// P0-5 修复验证：NotificationCallbacksController 回执处理需要持久化。
/// 修复前：UpdateAsync 后无 SaveChangesAsync，EF Core ChangeTracker 在请求结束时丢弃变更，
/// 渠道回执永远不写库导致记录滞留 Sending 状态。
/// 修复后：注入 IUnitOfWork，在 UpdateAsync 后调用 SaveChangesAsync。
/// </summary>
public class NotificationCallbacksControllerTests
{
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<NotificationCallbacksController>> _loggerMock;
    private readonly NotificationCallbacksController _sut;
    private const string TestSecret = "test-secret-key";

    public NotificationCallbacksControllerTests()
    {
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        // IUnitOfWork 继承 IDisposable，使用 Loose 避免 Dispose 意外调用时抛异常
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Notification:CallbackSecret"]).Returns(TestSecret);
        _loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        _sut = new NotificationCallbacksController(
            _recordRepoMock.Object,
            _uowMock.Object,
            _configMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SmsReceiptAsync_ValidSignature_ShouldPersistChangesViaSaveChangesAsync()
    {
        // Arrange — 构造一个 Sending 状态且 ChannelMessageId 已设置的记录。
        // NotificationRecord.ChannelMessageId 仅通过 MarkSucceeded 设置（同时置 Succeeded），
        // 为测试 ApplyReceipt 返回 true 的路径（非幂等跳过），用反射在 Sending 状态下注入 ChannelMessageId。
        var channelMessageId = "msg-123";
        var record = NotificationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "sms_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        SetChannelMessageIdViaReflection(record, channelMessageId);

        _recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto(channelMessageId, succeeded: true, timestamp);

        // Act
        var result = await _sut.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert — 修复后：必须调用 SaveChangesAsync 持久化回执状态变更
        Assert.IsType<OkObjectResult>(result);
        _recordRepoMock.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailReceiptAsync_ValidSignature_ShouldPersistChangesViaSaveChangesAsync()
    {
        // Arrange
        var channelMessageId = "email-msg-456";
        var record = NotificationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "email_code", null,
            NotificationChannel.Email, "Title", "Content");
        record.MarkSending();
        SetChannelMessageIdViaReflection(record, channelMessageId);

        _recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dto = BuildEmailReceiptDto(channelMessageId, succeeded: true, timestamp);

        // Act
        var result = await _sut.EmailReceiptAsync(dto, CancellationToken.None);

        // Assert — 修复后：邮件回执也必须调用 SaveChangesAsync
        Assert.IsType<OkObjectResult>(result);
        _recordRepoMock.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SmsReceiptAsync_IdempotentSkip_ShouldNotCallSaveChangesAsync()
    {
        // Arrange — 已 Succeeded 的记录，ApplyReceipt 返回 false（幂等跳过），
        // 此时不应调用 UpdateAsync 和 SaveChangesAsync。
        var channelMessageId = "msg-idem";
        var record = NotificationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "sms_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        record.MarkSucceeded(channelMessageId);

        _recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto(channelMessageId, succeeded: true, timestamp);

        // Act
        var result = await _sut.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert — 幂等跳过路径不应调用 UpdateAsync 和 SaveChangesAsync
        Assert.IsType<OkObjectResult>(result);
        _recordRepoMock.Verify(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SmsReceiptAsync_RecordNotFound_ShouldReturn404AndNotCallSaveChangesAsync()
    {
        // Arrange
        var channelMessageId = "msg-not-found";
        _recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationRecord?)null);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto(channelMessageId, succeeded: true, timestamp);

        // Act
        var result = await _sut.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
        _recordRepoMock.Verify(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private SmsReceiptDto BuildSmsReceiptDto(string channelMessageId, bool succeeded, long timestamp)
    {
        var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{TestSecret}";
        var signature = ComputeHmacSha256(raw, TestSecret);
        return new SmsReceiptDto
        {
            ChannelMessageId = channelMessageId,
            Succeeded = succeeded,
            Timestamp = timestamp,
            Signature = signature,
            RawPayload = "{}"
        };
    }

    private EmailReceiptDto BuildEmailReceiptDto(string channelMessageId, bool succeeded, long timestamp)
    {
        var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{TestSecret}";
        var signature = ComputeHmacSha256(raw, TestSecret);
        return new EmailReceiptDto
        {
            ChannelMessageId = channelMessageId,
            Succeeded = succeeded,
            Timestamp = timestamp,
            Signature = signature,
            RawPayload = "{}"
        };
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 通过反射设置 NotificationRecord.ChannelMessageId（private set），
    /// 以便在 Sending 状态下测试 ApplyReceipt 返回 true 的路径。
    /// </summary>
    private static void SetChannelMessageIdViaReflection(NotificationRecord record, string channelMessageId)
    {
        var property = typeof(NotificationRecord).GetProperty(nameof(NotificationRecord.ChannelMessageId))
            ?? throw new InvalidOperationException("NotificationRecord.ChannelMessageId 属性未找到");
        property.SetValue(record, channelMessageId);
    }
}

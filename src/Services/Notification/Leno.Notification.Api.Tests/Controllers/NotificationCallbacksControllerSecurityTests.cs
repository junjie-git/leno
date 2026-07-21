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
/// P0-6 修复验证：NotificationCallbacksController 安全加固。
/// 修复前：① 配置缺失时回退到源码可见的硬编码密钥 "LenoNotificationCallbackSecret2024"；
///         ② 时间戳无新鲜度校验，可无限重放。
/// 修复后：① 缺失密钥时构造函数抛 InvalidOperationException 拒绝启动；
///         ② VerifySignature 增加 ±5 分钟时间戳新鲜度校验。
/// </summary>
public class NotificationCallbacksControllerSecurityTests
{
    private const string RealSecret = "real-secret-value-2026";

    [Fact]
    public void Constructor_MissingCallbackSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange — 配置中不设置 CallbackSecret（返回 null）
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns((string?)null);

        // Act & Assert — 修复后：缺失密钥应抛异常而非回退硬编码默认值
        Assert.Throws<InvalidOperationException>(() =>
            new NotificationCallbacksController(
                new Mock<INotificationRecordRepository>().Object,
                new Mock<IUnitOfWork>().Object,
                configMock.Object,
                new Mock<ILogger<NotificationCallbacksController>>().Object));
    }

    [Fact]
    public void Constructor_EmptyCallbackSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange — 配置中 CallbackSecret 为空字符串
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns("");

        // Act & Assert — 空字符串也应拒绝
        Assert.Throws<InvalidOperationException>(() =>
            new NotificationCallbacksController(
                new Mock<INotificationRecordRepository>().Object,
                new Mock<IUnitOfWork>().Object,
                configMock.Object,
                new Mock<ILogger<NotificationCallbacksController>>().Object));
    }

    [Fact]
    public void Constructor_WhitespaceCallbackSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange — 配置中 CallbackSecret 为空白字符串
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns("   ");

        // Act & Assert — 空白字符串也应拒绝
        Assert.Throws<InvalidOperationException>(() =>
            new NotificationCallbacksController(
                new Mock<INotificationRecordRepository>().Object,
                new Mock<IUnitOfWork>().Object,
                configMock.Object,
                new Mock<ILogger<NotificationCallbacksController>>().Object));
    }

    [Fact]
    public async Task SmsReceiptAsync_ReplayedTimestamp_ShouldReturn401()
    {
        // Arrange — 时间戳超出 5 分钟窗口（10 分钟前），签名本身有效
        // 修复前：无时间戳校验，旧签名可无限重放；
        // 修复后：时间戳超出窗口应返回 401。
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns(RealSecret);

        // 使用 Strict 仓储 mock 但不设任何方法——若 VerifySignature 正确拒绝，
        // ProcessReceiptAsync 不应被调用，仓储方法也不应被调用
        var recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        var uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        var loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        var controller = new NotificationCallbacksController(
            recordRepoMock.Object, uowMock.Object, configMock.Object, loggerMock.Object);

        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto("msg-123", succeeded: true, oldTimestamp);

        // Act
        var result = await controller.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert — 修复后：重放时间戳应返回 401
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SmsReceiptAsync_FutureTimestamp_ShouldReturn401()
    {
        // Arrange — 时间戳超出 5 分钟窗口（10 分钟后），防止未来时间戳注入
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns(RealSecret);

        var recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        var uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        var loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        var controller = new NotificationCallbacksController(
            recordRepoMock.Object, uowMock.Object, configMock.Object, loggerMock.Object);

        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto("msg-future", succeeded: true, futureTimestamp);

        // Act
        var result = await controller.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert — 未来时间戳也应拒绝
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SmsReceiptAsync_InvalidTimestampFormat_ShouldReturn401()
    {
        // Arrange — 时间戳非数字，无法解析
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns(RealSecret);

        var recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        var uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        var loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        var controller = new NotificationCallbacksController(
            recordRepoMock.Object, uowMock.Object, configMock.Object, loggerMock.Object);

        // 构造非数字时间戳的 DTO（绕过 long 类型，直接构造签名）
        var raw = $"msg-bad|True|not-a-number|{RealSecret}";
        var signature = ComputeHmacSha256(raw, RealSecret);
        var dto = new SmsReceiptDto
        {
            ChannelMessageId = "msg-bad",
            Succeeded = true,
            Timestamp = 0, // 占位，实际签名用 "not-a-number"
            Signature = signature
        };

        // Act — 由于 Timestamp 是 long 类型，DTO 的 Timestamp=0 但签名用 "not-a-number"，
        // 签名不匹配也会返回 401；这里主要验证不抛异常
        var result = await controller.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SmsReceiptAsync_ValidTimestampWithinWindow_ShouldProcessNormally()
    {
        // Arrange — 时间戳在 5 分钟窗口内，应正常处理
        var channelMessageId = "msg-valid-ts";
        var record = NotificationRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "sms_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        SetChannelMessageIdViaReflection(record, channelMessageId);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Notification:CallbackSecret"]).Returns(RealSecret);

        var recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        recordRepoMock
            .Setup(r => r.GetByChannelMessageIdAsync(channelMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var loggerMock = new Mock<ILogger<NotificationCallbacksController>>();

        var controller = new NotificationCallbacksController(
            recordRepoMock.Object, uowMock.Object, configMock.Object, loggerMock.Object);

        // 时间戳在 1 分钟前（窗口内）
        var validTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var dto = BuildSmsReceiptDto(channelMessageId, succeeded: true, validTimestamp);

        // Act
        var result = await controller.SmsReceiptAsync(dto, CancellationToken.None);

        // Assert — 窗口内时间戳应正常处理
        Assert.IsType<OkObjectResult>(result);
        recordRepoMock.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 使用真实密钥构造 SmsReceiptDto，签名基于 RealSecret 计算。
    /// </summary>
    private SmsReceiptDto BuildSmsReceiptDto(string channelMessageId, bool succeeded, long timestamp)
    {
        var raw = $"{channelMessageId}|{succeeded}|{timestamp}|{RealSecret}";
        var signature = ComputeHmacSha256(raw, RealSecret);
        return new SmsReceiptDto
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

    private static void SetChannelMessageIdViaReflection(NotificationRecord record, string channelMessageId)
    {
        var property = typeof(NotificationRecord).GetProperty(nameof(NotificationRecord.ChannelMessageId))
            ?? throw new InvalidOperationException("NotificationRecord.ChannelMessageId 属性未找到");
        property.SetValue(record, channelMessageId);
    }
}

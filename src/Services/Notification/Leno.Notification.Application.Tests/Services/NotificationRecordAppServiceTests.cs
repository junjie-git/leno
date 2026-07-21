using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests.Services;

/// <summary>
/// P0-7 修复验证：死信重发应将记录状态置为 Pending 让 NotificationDispatchJob 接管实际发送，
/// 而非原实现调用 MarkResend() 置为 Sending 导致记录永久卡死（无 Job 拾取 Sending 状态记录）。
/// </summary>
public class NotificationRecordAppServiceTests
{
    private readonly Mock<INotificationRecordRepository> _recordRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationRecordAppService>> _loggerMock;
    private readonly NotificationRecordAppService _sut;

    public NotificationRecordAppServiceTests()
    {
        _recordRepoMock = new Mock<INotificationRecordRepository>(MockBehavior.Strict);
        // IUnitOfWork 继承 IDisposable，使用 Loose 避免 Dispose 未设置抛异常
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        _loggerMock = new Mock<ILogger<NotificationRecordAppService>>();
        _sut = new NotificationRecordAppService(
            _recordRepoMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// 构造一个已进入 DeadLettered 状态的通知记录：
    /// Pending → Sending → Failed → Retried → DeadLettered。
    /// </summary>
    private static NotificationRecord BuildDeadLetteredRecord(Guid recordId)
    {
        var record = NotificationRecord.Create(
            recordId, Guid.NewGuid(), "test_code", null,
            NotificationChannel.Sms, "Title", "Content");
        record.MarkSending();
        record.MarkFailed("err", "ERR");
        record.ScheduleRetry(DateTime.UtcNow.AddSeconds(-1));
        record.MoveToDeadLetter("max retries");
        return record;
    }

    [Fact]
    public async Task ResendRecordAsync_DeadLetteredRecord_ShouldMoveToPendingNotSending()
    {
        // Arrange — 死信记录重发后应进入 Pending（可被 DispatchJob 拾取），
        // 而非 Sending（无 Job 拾取导致永久卡死）。
        var recordId = Guid.NewGuid();
        var record = BuildDeadLetteredRecord(recordId);

        _recordRepoMock
            .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _recordRepoMock
            .Setup(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _sut.ResendRecordAsync(recordId, Guid.NewGuid(), CancellationToken.None);

        // Assert — 修复前：状态为 Sending（卡死）；修复后：状态为 Pending（可被 DispatchJob 拾取）
        record.Status.Should().Be(NotificationStatus.Pending);
        record.Status.Should().NotBe(NotificationStatus.Sending);
        record.RetryCount.Should().Be(0);
        record.ErrorMessage.Should().BeNull();
        record.ErrorCode.Should().BeNull();
        record.FailedAt.Should().BeNull();
        record.NextRetryAt.Should().BeNull();
        _recordRepoMock.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendRecordAsync_NonDeadLetteredRecord_ShouldThrowInvalidOperationException()
    {
        // Arrange — 非 DeadLettered 状态的记录不可重发，应抛 InvalidOperationException。
        // 此时不应调用 UpdateAsync 或 SaveChangesAsync。
        var recordId = Guid.NewGuid();
        var record = NotificationRecord.Create(
            recordId, Guid.NewGuid(), "test_code", null,
            NotificationChannel.Sms, "Title", "Content");
        // 记录仍为 Pending 状态，非 DeadLettered

        _recordRepoMock
            .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        // Act
        var act = () => _sut.ResendRecordAsync(recordId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{recordId}*非死信状态*");

        record.Status.Should().Be(NotificationStatus.Pending);
        _recordRepoMock.Verify(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendRecordAsync_NonExistentRecord_ShouldThrowArgumentException()
    {
        // Arrange — 记录不存在时应抛 ArgumentException（ParamName=recordId）。
        var recordId = Guid.NewGuid();

        _recordRepoMock
            .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationRecord?)null);

        // Act
        var act = () => _sut.ResendRecordAsync(recordId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be("recordId");

        _recordRepoMock.Verify(r => r.UpdateAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendRecordAsync_EmptyRecordId_ShouldThrowArgumentException()
    {
        // Arrange — 空标识应直接抛 ArgumentException，不应查询仓储。
        // 无需设置任何 mock，因为 GetByIdAsync 不应被调用。

        // Act
        var act = () => _sut.ResendRecordAsync(Guid.Empty, Guid.NewGuid(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be("recordId");

        _recordRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

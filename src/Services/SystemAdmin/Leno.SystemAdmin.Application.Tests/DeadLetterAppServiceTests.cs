using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 死信消息管理应用服务单元测试，覆盖重投、丢弃与批量操作用例。
/// </summary>
public class DeadLetterAppServiceTests
{
    private readonly Mock<IDeadLetterMessageRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<DeadLetterAppService>> _loggerMock = new();
    private readonly DeadLetterAppService _sut;

    private static readonly Guid MessageId = Guid.NewGuid();
    private const string OperatorId = "op-001";

    public DeadLetterAppServiceTests()
    {
        _sut = new DeadLetterAppService(
            _repoMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResult()
    {
        var msg = CreateDeadLetter();
        _repoMock
            .Setup(r => r.QueryAsync("Order", DeadLetterStatus.Pending, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeadLetterMessage> { msg });
        _repoMock
            .Setup(r => r.CountAsync("Order", DeadLetterStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAsync("Order", DeadLetterStatus.Pending, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].SourceContext.Should().Be("Order");
        result.Items[0].Status.Should().Be(DeadLetterStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnDto()
    {
        var msg = CreateDeadLetter(MessageId);
        _repoMock
            .Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(msg);

        var result = await _sut.GetByIdAsync(MessageId);

        result.Should().NotBeNull();
        result!.MessageId.Should().Be(MessageId);
        result.SourceContext.Should().Be("Order");
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ShouldReturnNull()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeadLetterMessage?)null);

        var result = await _sut.GetByIdAsync(MessageId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RetryAsync_Pending_ShouldMarkRetriedAndSave()
    {
        var msg = CreateDeadLetter();
        _repoMock
            .Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(msg);

        await _sut.RetryAsync(MessageId, OperatorId);

        msg.Status.Should().Be(DeadLetterStatus.Retried);
        msg.OperatorId.Should().Be(OperatorId);
        msg.ProcessedAt.Should().NotBeNull();
        _repoMock.Verify(r => r.UpdateAsync(msg, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeadLetterMessage?)null);

        var act = () => _sut.RetryAsync(MessageId, OperatorId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*死信消息*不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiscardAsync_Pending_ShouldMarkDiscardedWithReason()
    {
        var msg = CreateDeadLetter();
        _repoMock
            .Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(msg);

        await _sut.DiscardAsync(MessageId, OperatorId, "无法处理的消息");

        msg.Status.Should().Be(DeadLetterStatus.Discarded);
        msg.OperatorId.Should().Be(OperatorId);
        msg.DiscardReason.Should().Be("无法处理的消息");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchRetryAsync_MixedSuccessAndFailure_ShouldAggregateResult()
    {
        var successMsg1 = CreateDeadLetter();
        var successMsg2 = CreateDeadLetter();
        var successId1 = successMsg1.Id;
        var successId2 = successMsg2.Id;
        var failedId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(successId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(successMsg1);
        _repoMock
            .Setup(r => r.GetByIdAsync(successId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(successMsg2);
        _repoMock
            .Setup(r => r.GetByIdAsync(failedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeadLetterMessage?)null);

        var result = await _sut.BatchRetryAsync(new List<Guid> { successId1, successId2, failedId }, OperatorId);

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].MessageId.Should().Be(failedId);
    }

    [Fact]
    public async Task BatchDiscardAsync_AllSuccess_ShouldReturnZeroFailures()
    {
        var msg1 = CreateDeadLetter();
        var msg2 = CreateDeadLetter();
        _repoMock
            .Setup(r => r.GetByIdAsync(msg1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(msg1);
        _repoMock
            .Setup(r => r.GetByIdAsync(msg2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(msg2);

        var result = await _sut.BatchDiscardAsync(new List<Guid> { msg1.Id, msg2.Id }, OperatorId, "批量丢弃");

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        msg1.Status.Should().Be(DeadLetterStatus.Discarded);
        msg2.Status.Should().Be(DeadLetterStatus.Discarded);
    }

    [Fact]
    public async Task BatchRetryAsync_NullMessageIds_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.BatchRetryAsync(null!, OperatorId);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static DeadLetterMessage CreateDeadLetter(Guid? id = null) =>
        DeadLetterMessage.Create(
            id ?? Guid.NewGuid(),
            "msg-" + Guid.NewGuid(),
            "Order",
            "order.events",
            "{\"orderId\":\"" + Guid.NewGuid() + "\"}",
            "{}",
            "处理超时");
}

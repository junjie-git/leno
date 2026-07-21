using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// 验证 <see cref="DeadLetterAppService.BatchRetryAsync"/> 与 <see cref="DeadLetterAppService.BatchDiscardAsync"/>
/// 合并为单次 <c>SaveEntitiesAsync</c> 调用（M-05 修复）。
/// </summary>
public sealed class DeadLetterBatchSaveEntitiesTests
{
    private readonly Mock<IDeadLetterMessageRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeadLetterAppService _service;

    public DeadLetterBatchSaveEntitiesTests()
    {
        _service = new DeadLetterAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<DeadLetterAppService>.Instance);
    }

    [Fact]
    public async Task BatchRetryAsync_Should_Call_SaveEntitiesAsync_Only_Once_For_Multiple_Messages()
    {
        var msg1 = CreateMessage();
        var msg2 = CreateMessage();
        var msg3 = CreateMessage();
        var ids = new List<Guid> { msg1.Id, msg2.Id, msg3.Id };

        _repoMock.Setup(r => r.GetByIdAsync(msg1.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg1);
        _repoMock.Setup(r => r.GetByIdAsync(msg2.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg2);
        _repoMock.Setup(r => r.GetByIdAsync(msg3.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg3);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.BatchRetryAsync(ids, "op-001", CancellationToken.None);

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task BatchDiscardAsync_Should_Call_SaveEntitiesAsync_Only_Once_For_Multiple_Messages()
    {
        var msg1 = CreateMessage();
        var msg2 = CreateMessage();
        var ids = new List<Guid> { msg1.Id, msg2.Id };

        _repoMock.Setup(r => r.GetByIdAsync(msg1.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg1);
        _repoMock.Setup(r => r.GetByIdAsync(msg2.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg2);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.BatchDiscardAsync(ids, "op-001", "批量清理", CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchRetryAsync_With_All_NotFound_Should_Not_Call_SaveEntitiesAsync()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeadLetterMessage?)null);

        var result = await _service.BatchRetryAsync(ids, "op-001", CancellationToken.None);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BatchRetryAsync_Partial_Failure_Should_Still_Save_Successful_Part()
    {
        var msg1 = CreateMessage();
        var msg2 = CreateMessage();
        // 让 msg2 已经是 Retried 状态触发聚合不变量异常
        msg2.Retry("previous-op");

        var ids = new List<Guid> { msg1.Id, msg2.Id };
        _repoMock.Setup(r => r.GetByIdAsync(msg1.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg1);
        _repoMock.Setup(r => r.GetByIdAsync(msg2.Id, It.IsAny<CancellationToken>())).ReturnsAsync(msg2);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.BatchRetryAsync(ids, "op-001", CancellationToken.None);

        // msg1 成功，msg2 因状态不变量失败但不应中断事务
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Single(result.Errors);
        Assert.Equal(msg2.Id, result.Errors[0].MessageId);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DeadLetterMessage CreateMessage()
        => DeadLetterMessage.Create(
            Guid.NewGuid(),
            "orig-" + Guid.NewGuid(),
            "Order",
            "order.events",
            "{\"k\":\"v\"}",
            "{}",
            "test error");
}

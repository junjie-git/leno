using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// <see cref="IndexRebuildOrchestrator"/> 单元测试。
/// 验证：
/// - TriggerAsync 合并 Create+Start 为单次 SaveEntitiesAsync（避免多步状态变更无事务）
/// - 触发失败时标记任务为 Failed 并持久化
/// - RetryAsync 重试前重新检查并发运行任务，避免竞态
/// </summary>
public sealed class IndexRebuildOrchestratorTests
{
    private readonly Mock<IIndexRebuildTaskRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIndexRebuildTrigger> _triggerMock = new();
    private readonly IndexRebuildOrchestrator _orchestrator;

    public IndexRebuildOrchestratorTests()
    {
        _orchestrator = new IndexRebuildOrchestrator(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _triggerMock.Object,
            NullLogger<IndexRebuildOrchestrator>.Instance);
    }

    [Fact]
    public async Task TriggerAsync_Should_Call_SaveEntitiesAsync_Once_Not_Three_Times()
    {
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Product", "products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(It.IsAny<Guid>(), "Product", "products", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var task = await _orchestrator.TriggerAsync("Product", "products", "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Running, task.Status);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerAsync_Should_Mark_Failed_When_Trigger_StartAsync_Throws()
    {
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Order", "orders", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(It.IsAny<Guid>(), "Order", "orders", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ES 不可用"));

        var task = await _orchestrator.TriggerAsync("Order", "orders", "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Failed, task.Status);
        Assert.Contains("ES 不可用", task.ErrorMessage);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RetryAsync_Should_Throw_When_Concurrent_Running_Task_Exists()
    {
        var taskId = Guid.NewGuid();
        var existingTask = IndexRebuildTask.Create(taskId, "Product", "products", "admin");
        existingTask.Start();
        _repoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        // 模拟并发：存在另一个运行中的任务
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Product", "products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexRebuildTask.Create(Guid.NewGuid(), "Product", "products", "other"));

        // 先 Fail existingTask 使其可重试
        existingTask.Fail("之前的错误");

        await Assert.ThrowsAsync<SystemAdminDomainException>(
            () => _orchestrator.RetryAsync(taskId, "admin", CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_Should_Call_SaveEntitiesAsync_Once_When_No_Concurrent_Task()
    {
        var taskId = Guid.NewGuid();
        var existingTask = IndexRebuildTask.Create(taskId, "Product", "products", "admin");
        existingTask.Start();
        existingTask.Fail("之前的错误");

        _repoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Product", "products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(taskId, "Product", "products", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var task = await _orchestrator.RetryAsync(taskId, "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Running, task.Status);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_Should_Mark_Failed_When_Trigger_StartAsync_Throws()
    {
        var taskId = Guid.NewGuid();
        var existingTask = IndexRebuildTask.Create(taskId, "Order", "orders", "admin");
        existingTask.Start();
        existingTask.Fail("之前的错误");

        _repoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);
        _repoMock.Setup(r => r.GetRunningByIndexAsync("Order", "orders", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexRebuildTask?)null);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _triggerMock.Setup(t => t.StartAsync(taskId, "Order", "orders", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ES 重试不可用"));

        var task = await _orchestrator.RetryAsync(taskId, "admin", CancellationToken.None);

        Assert.Equal(RebuildTaskStatus.Failed, task.Status);
        Assert.Contains("ES 重试不可用", task.ErrorMessage);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

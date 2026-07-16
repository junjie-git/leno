using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 定时任务管理应用服务单元测试，覆盖创建、启用、停用、立即触发与查询用例。
/// </summary>
public class ScheduledTaskAppServiceTests
{
    private readonly Mock<IScheduledTaskRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IScheduledTaskExecutor> _executorMock = new();
    private readonly Mock<ILogger<ScheduledTaskAppService>> _loggerMock = new();
    private readonly ScheduledTaskAppService _sut;

    private static readonly Guid TaskId = Guid.NewGuid();

    public ScheduledTaskAppServiceTests()
    {
        _sut = new ScheduledTaskAppService(
            _repoMock.Object,
            _uowMock.Object,
            _executorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_Valid_ShouldCreateDisabledTask()
    {
        var dto = new SaveScheduledTaskDto
        {
            Name = "订单超时取消",
            JobType = "Leno.Order.Jobs.OrderTimeoutJob",
            CronExpression = "*/5 * * * *",
            Parameters = "{\"timeoutMinutes\":30}"
        };

        var result = await _sut.CreateAsync(dto);

        result.TaskId.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("订单超时取消");
        result.Status.Should().Be(ScheduledTaskStatus.Disabled);
        result.LastRunStatus.Should().Be(TaskRunStatus.Never);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<ScheduledTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NullDto_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<ScheduledTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Existing_ShouldUpdateFields()
    {
        var task = ScheduledTask.Create(TaskId, "原任务", "Leno.X.Job", "0 * * * *", null);
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var result = await _sut.UpdateAsync(TaskId, new UpdateScheduledTaskDto
        {
            Name = "新任务名",
            CronExpression = "*/10 * * * *",
            Parameters = "{\"k\":\"v\"}"
        });

        result.Name.Should().Be("新任务名");
        result.CronExpression.Should().Be("*/10 * * * *");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_Existing_ShouldEnableAndSchedule()
    {
        var task = ScheduledTask.Create(TaskId, "任务", "Leno.X.Job", "0 * * * *", null);
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        await _sut.EnableAsync(TaskId);

        task.Status.Should().Be(ScheduledTaskStatus.Enabled);
        task.NextRunAt.Should().NotBeNull();
        _executorMock.Verify(e => e.ScheduleAsync(TaskId, "Leno.X.Job", "0 * * * *", null, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Existing_ShouldDisableAndUnschedule()
    {
        var task = ScheduledTask.Create(TaskId, "任务", "Leno.X.Job", "0 * * * *", null);
        task.Enable(DateTime.UtcNow.AddHours(1));
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        await _sut.DisableAsync(TaskId);

        task.Status.Should().Be(ScheduledTaskStatus.Disabled);
        _executorMock.Verify(e => e.UnscheduleAsync(TaskId, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunNowAsync_Existing_ShouldRunAndInvokeExecutor()
    {
        var task = ScheduledTask.Create(TaskId, "任务", "Leno.X.Job", "0 * * * *", null);
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        await _sut.RunNowAsync(TaskId);

        task.LastRunStatus.Should().Be(TaskRunStatus.Running);
        task.LastRunAt.Should().NotBeNull();
        _executorMock.Verify(e => e.RunNowAsync(TaskId, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledTask?)null);

        var act = () => _sut.EnableAsync(TaskId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*定时任务*不存在*");
        _executorMock.Verify(e => e.ScheduleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnDto()
    {
        var task = ScheduledTask.Create(TaskId, "任务", "Leno.X.Job", "0 * * * *", null);
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var result = await _sut.GetByIdAsync(TaskId);

        result.Should().NotBeNull();
        result!.TaskId.Should().Be(TaskId);
        result.Name.Should().Be("任务");
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ShouldReturnNull()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledTask?)null);

        var result = await _sut.GetByIdAsync(TaskId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResult()
    {
        var task = ScheduledTask.Create(TaskId, "任务", "Leno.X.Job", "0 * * * *", null);
        _repoMock
            .Setup(r => r.QueryAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduledTask> { task });
        _repoMock
            .Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }
}

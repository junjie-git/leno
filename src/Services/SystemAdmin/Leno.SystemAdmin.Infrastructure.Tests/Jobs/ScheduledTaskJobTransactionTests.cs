using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Jobs;

/// <summary>
/// 验证 <see cref="ScheduledTaskJob"/> 使用 <see cref="ScheduledTask.RunAndRecord"/> 单次事务提交执行结果（M-06 修复），
/// 不再分两次 SaveEntitiesAsync。
/// </summary>
public sealed class ScheduledTaskJobTransactionTests
{
    private readonly Mock<IScheduledTaskRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ScheduledTaskJob>> _loggerMock = new();
    private readonly ServiceProvider _serviceProvider;

    public ScheduledTaskJobTransactionTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_repoMock.Object);
        services.AddSingleton(_unitOfWorkMock.Object);
        services.AddSingleton<ILogger<ScheduledTaskJob>>(_loggerMock.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Execute_Success_Path_Should_Call_SaveEntitiesAsync_Only_Once()
    {
        var task = ScheduledTask.Create(
            Guid.NewGuid(), "测试任务", "TestJob, Assembly", "0 * * * * ?", null);
        var context = CreateJobExecutionContext(task.TaskId.ToString());
        _repoMock.Setup(r => r.GetByIdAsync(task.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var job = new ScheduledTaskJob(_serviceProvider);
        await job.Execute(context);

        Assert.Equal(TaskRunStatus.Success, task.LastRunStatus);
        Assert.NotNull(task.LastRunAt);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_Failure_Path_Should_Call_RunAndRecord_Failed_With_Single_Save()
    {
        var task = ScheduledTask.Create(
            Guid.NewGuid(), "测试任务", "TestJob, Assembly", "0 * * * * ?", null);
        var context = CreateJobExecutionContext(task.TaskId.ToString());
        _repoMock.Setup(r => r.GetByIdAsync(task.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        // 模拟 SaveEntitiesAsync 抛异常
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB 不可用"));

        var job = new ScheduledTaskJob(_serviceProvider);
        await job.Execute(context);

        // 失败分支记录 Failed 状态并再次 SaveEntitiesAsync，单次提交
        Assert.Equal(TaskRunStatus.Failed, task.LastRunStatus);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_When_Task_NotFound_Should_LogWarning_And_Return()
    {
        var taskId = Guid.NewGuid();
        var context = CreateJobExecutionContext(taskId.ToString());
        _repoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledTask?)null);

        var job = new ScheduledTaskJob(_serviceProvider);
        await job.Execute(context);

        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("定时任务不存在", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static IJobExecutionContext CreateJobExecutionContext(string taskId)
    {
        var mock = new Mock<IJobExecutionContext>();
        var jobDataMap = new JobDataMap
        {
            ["taskId"] = taskId
        };
        mock.SetupGet(c => c.MergedJobDataMap).Returns(jobDataMap);
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}

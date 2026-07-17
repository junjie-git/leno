using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 任务中心应用服务单元测试，覆盖任务列表查询、任务完成奖励、防重复完成与每日重置场景。
/// </summary>
public class TaskAppServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepoMock = new();
    private readonly Mock<IUserTaskRepository> _userTaskRepoMock = new();
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly TaskAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();

    public TaskAppServiceTests()
    {
        _sut = new TaskAppService(
            _taskRepoMock.Object,
            _userTaskRepoMock.Object,
            _accountRepoMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task GetTasksAsync_NoTasks_ShouldReturnEmptyList()
    {
        _taskRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskDefinition>());
        _userTaskRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserTask>());

        var result = await _sut.GetTasksAsync(UserId);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTasksAsync_WithTasks_ShouldReturnTaskDtos()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.CompleteProfile, "完善资料", "完善个人资料", 50,
            "全部字段填写完成", isDaily: false, isOneTime: true);
        _taskRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskDefinition> { task });
        _userTaskRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserTask>());

        var result = await _sut.GetTasksAsync(UserId);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(TaskId);
        result[0].Name.Should().Be("完善资料");
        result[0].RewardPoints.Should().Be(50);
        result[0].UserStatus.Should().BeNull();
    }

    [Fact]
    public async Task CompleteTaskAsync_TaskNotFound_ShouldThrow()
    {
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskDefinition?)null);

        var act = () => _sut.CompleteTaskAsync(UserId, TaskId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*任务*不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTaskAsync_TaskDisabled_ShouldThrow()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.ShareProduct, "分享商品", "分享商品", 5,
            "分享任意商品", isDaily: true, isOneTime: false);
        task.Disable();
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var act = () => _sut.CompleteTaskAsync(UserId, TaskId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*任务已停用*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTaskAsync_OneTimeAlreadyDone_ShouldThrow()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.CompleteProfile, "完善资料", "完善个人资料", 50,
            "全部字段填写完成", isDaily: false, isOneTime: true);
        var existingUserTask = UserTask.Create(Guid.NewGuid(), UserId, TaskId);
        existingUserTask.Complete();
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _userTaskRepoMock.Setup(r => r.GetByUserIdAndTaskIdAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUserTask);

        var act = () => _sut.CompleteTaskAsync(UserId, TaskId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*一次性任务已完成，不可重复完成*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTaskAsync_FirstTime_ShouldAwardPoints()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.CompleteProfile, "完善资料", "完善个人资料", 50,
            "全部字段填写完成", isDaily: false, isOneTime: true);
        var account = PointsAccount.Create(AccountId, UserId);
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _userTaskRepoMock.Setup(r => r.GetByUserIdAndTaskIdAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTask?)null);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.CompleteTaskAsync(UserId, TaskId);

        result.Should().NotBeNull();
        result.TaskId.Should().Be(TaskId);
        result.UserId.Should().Be(UserId);
        result.PointsAwarded.Should().Be(50);
        account.Balance.Should().Be(50);
        _userTaskRepoMock.Verify(r => r.AddAsync(It.IsAny<UserTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteTaskAsync_AccountNotFound_ShouldThrow()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.FirstOrder, "首单任务", "首次下单", 200,
            "完成第一笔订单", isDaily: false, isOneTime: true);
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _userTaskRepoMock.Setup(r => r.GetByUserIdAndTaskIdAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTask?)null);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.CompleteTaskAsync(UserId, TaskId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*积分账户不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTaskAsync_DailyTaskCompletedToday_ShouldThrow()
    {
        var task = TaskDefinition.Create(
            TaskId, TaskType.ShareProduct, "分享商品", "分享商品", 5,
            "分享任意商品", isDaily: true, isOneTime: false);
        var existingUserTask = UserTask.Create(Guid.NewGuid(), UserId, TaskId);
        existingUserTask.Complete();
        _taskRepoMock.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _userTaskRepoMock.Setup(r => r.GetByUserIdAndTaskIdAsync(UserId, TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUserTask);

        var act = () => _sut.CompleteTaskAsync(UserId, TaskId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*今日已完成该任务*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

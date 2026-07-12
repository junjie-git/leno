using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Domain.Tests;

public class TaskDefinitionTests
{
    private static readonly Guid TaskId = Guid.NewGuid();

    [Fact]
    public void Create_OneTimeTask_ShouldInitializeCorrectly()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "填写个人资料信息", 50, "完成资料填写", false, true);

        task.Id.Should().Be(TaskId);
        task.Type.Should().Be(TaskType.CompleteProfile);
        task.Name.Should().Be("完善资料");
        task.Description.Should().Be("填写个人资料信息");
        task.RewardPoints.Should().Be(50);
        task.CompletionCondition.Should().Be("完成资料填写");
        task.IsDaily.Should().BeFalse();
        task.IsOneTime.Should().BeTrue();
        task.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_DailyTask_ShouldInitializeCorrectly()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.ShareProduct, "分享商品",
            "分享商品到社交平台", 5, "每日分享一次", true, false);

        task.IsDaily.Should().BeTrue();
        task.IsOneTime.Should().BeFalse();
        task.RewardPoints.Should().Be(5);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowException()
    {
        var act = () => TaskDefinition.Create(Guid.Empty, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", false, true);

        act.Should().Throw<PointsDomainException>().WithMessage("*TaskId*");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "",
            "描述", 50, "条件", false, true);

        act.Should().Throw<PointsDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_NegativeRewardPoints_ShouldThrowException()
    {
        var act = () => TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", -1, "条件", false, true);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可为负*");
    }

    [Fact]
    public void Create_BothDailyAndOneTime_ShouldThrowException()
    {
        var act = () => TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", true, true);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可同时*");
    }

    [Fact]
    public void Create_ZeroRewardPoints_ShouldSucceed()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 0, "条件", false, true);

        task.RewardPoints.Should().Be(0);
    }

    [Fact]
    public void Enable_Disabled_ShouldBecomeEnabled()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", false, true);
        task.Disable();

        task.Enable();

        task.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_AlreadyEnabled_ShouldThrowException()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", false, true);

        var act = () => task.Enable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已启用*");
    }

    [Fact]
    public void Disable_Enabled_ShouldBecomeDisabled()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", false, true);

        task.Disable();

        task.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_AlreadyDisabled_ShouldThrowException()
    {
        var task = TaskDefinition.Create(TaskId, TaskType.CompleteProfile, "完善资料",
            "描述", 50, "条件", false, true);
        task.Disable();

        var act = () => task.Disable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已停用*");
    }
}

public class UserTaskTests
{
    private static readonly Guid UserTaskId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeAsPending()
    {
        var userTask = UserTask.Create(UserTaskId, UserId, TaskId);

        userTask.Id.Should().Be(UserTaskId);
        userTask.UserId.Should().Be(UserId);
        userTask.TaskId.Should().Be(TaskId);
        userTask.Status.Should().Be(UserTaskStatus.Pending);
        userTask.CompletedAt.Should().BeNull();
        userTask.CompletedDate.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyUserTaskId_ShouldThrowException()
    {
        var act = () => UserTask.Create(Guid.Empty, UserId, TaskId);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserTaskId*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => UserTask.Create(UserTaskId, Guid.Empty, TaskId);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowException()
    {
        var act = () => UserTask.Create(UserTaskId, UserId, Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*TaskId*");
    }

    [Fact]
    public void Complete_Pending_ShouldSetCompleted()
    {
        var userTask = UserTask.Create(UserTaskId, UserId, TaskId);

        userTask.Complete();

        userTask.Status.Should().Be(UserTaskStatus.Completed);
        userTask.CompletedAt.Should().NotBeNull();
        userTask.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userTask.CompletedDate.Should().NotBeNull();
    }

    [Fact]
    public void Complete_AlreadyCompleted_ShouldThrowException()
    {
        var userTask = UserTask.Create(UserTaskId, UserId, TaskId);
        userTask.Complete();

        var act = () => userTask.Complete();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可完成*");
    }

    [Fact]
    public void Reset_Completed_ShouldBecomePending()
    {
        var userTask = UserTask.Create(UserTaskId, UserId, TaskId);
        userTask.Complete();

        userTask.Reset();

        userTask.Status.Should().Be(UserTaskStatus.Pending);
        userTask.CompletedAt.Should().BeNull();
        userTask.CompletedDate.Should().BeNull();
    }

    [Fact]
    public void Reset_Pending_ShouldThrowException()
    {
        var userTask = UserTask.Create(UserTaskId, UserId, TaskId);

        var act = () => userTask.Reset();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可重置*");
    }
}
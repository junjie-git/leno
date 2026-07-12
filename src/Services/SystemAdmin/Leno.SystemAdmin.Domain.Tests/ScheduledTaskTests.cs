using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class ScheduledTaskTests
{
    private static readonly Guid ValidTaskId = Guid.NewGuid();
    private const string ValidName = "CleanupJob";
    private const string ValidJobType = "Leno.Jobs.CleanupJob, Leno.Jobs";
    private const string ValidCron = "0 0 * * *";
    private const string ValidParameters = "{\"retention\":30}";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var task = ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, ValidCron, ValidParameters);

        task.TaskId.Should().Be(ValidTaskId);
        task.Id.Should().Be(ValidTaskId);
        task.Name.Should().Be(ValidName);
        task.JobType.Should().Be(ValidJobType);
        task.CronExpression.Should().Be(ValidCron);
        task.Parameters.Should().Be(ValidParameters);
        task.Status.Should().Be(ScheduledTaskStatus.Disabled);
        task.LastRunStatus.Should().Be(TaskRunStatus.Never);
        task.LastRunAt.Should().BeNull();
        task.NextRunAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullParameters_ShouldSetNull()
    {
        var task = ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, ValidCron, parameters: null);

        task.Parameters.Should().BeNull();
    }

    [Fact]
    public void Create_WithWhitespaceParameters_ShouldNormalizeToNull()
    {
        var task = ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, ValidCron, "   ");

        task.Parameters.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimNameJobTypeAndCron()
    {
        var task = ScheduledTask.Create(ValidTaskId, "  CleanupJob  ", "  Leno.Jobs.CleanupJob  ", "  0 0 * * *  ", ValidParameters);

        task.Name.Should().Be("CleanupJob");
        task.JobType.Should().Be("Leno.Jobs.CleanupJob");
        task.CronExpression.Should().Be("0 0 * * *");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyTaskId_ShouldThrowTaskIdEmpty()
    {
        var act = () => ScheduledTask.Create(Guid.Empty, ValidName, ValidJobType, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowTaskNameEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, null!, ValidJobType, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowTaskNameEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, "", ValidJobType, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldThrowTaskNameLength()
    {
        var longName = new string('n', 129);

        var act = () => ScheduledTask.Create(ValidTaskId, longName, ValidJobType, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 128);

        var task = ScheduledTask.Create(ValidTaskId, name, ValidJobType, ValidCron, ValidParameters);

        task.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithNullJobType_ShouldThrowTaskJobTypeEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, null!, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_JOB_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyJobType_ShouldThrowTaskJobTypeEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, "", ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_JOB_TYPE_EMPTY");
    }

    [Fact]
    public void Create_WithJobTypeTooLong_ShouldThrowTaskJobTypeLength()
    {
        var longJobType = new string('j', 257);

        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, longJobType, ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_JOB_TYPE_LENGTH");
    }

    [Fact]
    public void Create_WithJobTypeAtMaxLength_ShouldSucceed()
    {
        var jobType = new string('j', 256);

        var task = ScheduledTask.Create(ValidTaskId, ValidName, jobType, ValidCron, ValidParameters);

        task.JobType.Should().Be(jobType);
    }

    [Fact]
    public void Create_WithNullCron_ShouldThrowTaskCronEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, null!, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_CRON_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyCron_ShouldThrowTaskCronEmpty()
    {
        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, "", ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_CRON_EMPTY");
    }

    [Fact]
    public void Create_WithCronTooLong_ShouldThrowTaskCronLength()
    {
        var longCron = new string('c', 129);

        var act = () => ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, longCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_CRON_LENGTH");
    }

    [Fact]
    public void Create_WithCronAtMaxLength_ShouldSucceed()
    {
        var cron = new string('c', 128);

        var task = ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, cron, ValidParameters);

        task.CronExpression.Should().Be(cron);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProperties()
    {
        var task = CreateTask();

        task.Update("NewName", "0 12 * * *", "{\"retention\":60}");

        task.Name.Should().Be("NewName");
        task.CronExpression.Should().Be("0 12 * * *");
        task.Parameters.Should().Be("{\"retention\":60}");
    }

    [Fact]
    public void Update_WithNullParameters_ShouldSetNull()
    {
        var task = CreateTask();

        task.Update("NewName", "0 12 * * *", null);

        task.Parameters.Should().BeNull();
    }

    [Fact]
    public void Update_WithEmptyName_ShouldThrowTaskNameEmpty()
    {
        var task = CreateTask();

        var act = () => task.Update("", ValidCron, ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_NAME_EMPTY");
    }

    [Fact]
    public void Update_WithEmptyCron_ShouldThrowTaskCronEmpty()
    {
        var task = CreateTask();

        var act = () => task.Update(ValidName, "", ValidParameters);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_CRON_EMPTY");
    }

    #endregion

    #region Enable

    [Fact]
    public void Enable_WithValidNextRunAt_ShouldSetEnabledAndNextRunAt()
    {
        var task = CreateTask();
        var nextRunAt = DateTime.UtcNow.AddHours(1);

        task.Enable(nextRunAt);

        task.Status.Should().Be(ScheduledTaskStatus.Enabled);
        task.NextRunAt.Should().Be(nextRunAt);
    }

    [Fact]
    public void Enable_WithDefaultDateTime_ShouldThrowTaskNextRunAtEmpty()
    {
        var task = CreateTask();

        var act = () => task.Enable(default);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_NEXT_RUN_AT_EMPTY");
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_ShouldSetStatusToDisabled()
    {
        var task = CreateTask();
        task.Enable(DateTime.UtcNow.AddHours(1));

        task.Disable();

        task.Status.Should().Be(ScheduledTaskStatus.Disabled);
    }

    #endregion

    #region RunNow

    [Fact]
    public void RunNow_ShouldSetLastRunStatusToRunningAndSetLastRunAt()
    {
        var task = CreateTask();
        var before = DateTime.UtcNow;

        task.RunNow();

        task.LastRunStatus.Should().Be(TaskRunStatus.Running);
        task.LastRunAt.Should().NotBeNull();
        task.LastRunAt!.Value.Should().BeCloseTo(before, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RunNow_ShouldNotChangeStatus()
    {
        var task = CreateTask();

        task.RunNow();

        task.Status.Should().Be(ScheduledTaskStatus.Disabled);
    }

    #endregion

    #region RecordExecution

    [Fact]
    public void RecordExecution_WithValidParameters_ShouldUpdateLastRunStatusAndTime()
    {
        var task = CreateTask();
        var runAt = DateTime.UtcNow.AddMinutes(-5);

        task.RecordExecution(TaskRunStatus.Success, runAt, "Completed successfully");

        task.LastRunStatus.Should().Be(TaskRunStatus.Success);
        task.LastRunAt.Should().Be(runAt);
    }

    [Fact]
    public void RecordExecution_WithFailedStatus_ShouldUpdateCorrectly()
    {
        var task = CreateTask();
        var runAt = DateTime.UtcNow.AddMinutes(-5);

        task.RecordExecution(TaskRunStatus.Failed, runAt, "Error: timeout");

        task.LastRunStatus.Should().Be(TaskRunStatus.Failed);
        task.LastRunAt.Should().Be(runAt);
    }

    [Fact]
    public void RecordExecution_WithInvalidStatus_ShouldThrowTaskRunStatusInvalid()
    {
        var task = CreateTask();
        var invalidStatus = (TaskRunStatus)999;

        var act = () => task.RecordExecution(invalidStatus, DateTime.UtcNow, "result");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_RUN_STATUS_INVALID");
    }

    [Fact]
    public void RecordExecution_WithDefaultRunAt_ShouldThrowTaskRunAtEmpty()
    {
        var task = CreateTask();

        var act = () => task.RecordExecution(TaskRunStatus.Success, default, "result");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("TASK_RUN_AT_EMPTY");
    }

    [Fact]
    public void RecordExecution_WithNullResult_ShouldNotThrow()
    {
        var task = CreateTask();

        var act = () => task.RecordExecution(TaskRunStatus.Success, DateTime.UtcNow, null);

        act.Should().NotThrow();
    }

    #endregion

    private static ScheduledTask CreateTask()
    {
        return ScheduledTask.Create(ValidTaskId, ValidName, ValidJobType, ValidCron, ValidParameters);
    }
}
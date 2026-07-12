using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class IndexRebuildTaskTests
{
    private static readonly Guid ValidTaskId = Guid.NewGuid();
    private const string ValidTargetContext = "Product";
    private const string ValidIndexName = "products";
    private const string ValidTriggeredBy = "operator-001";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var task = IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, ValidTriggeredBy);

        task.TaskId.Should().Be(ValidTaskId);
        task.Id.Should().Be(ValidTaskId);
        task.TargetContext.Should().Be(ValidTargetContext);
        task.IndexName.Should().Be(ValidIndexName);
        task.TriggeredBy.Should().Be(ValidTriggeredBy);
        task.Status.Should().Be(RebuildTaskStatus.Created);
        task.Progress.Should().Be(0);
        task.RetryCount.Should().Be(0);
        task.ErrorMessage.Should().BeNull();
        task.StartedAt.Should().BeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimStrings()
    {
        var task = IndexRebuildTask.Create(ValidTaskId, "  Product  ", "  products  ", "  operator-001  ");

        task.TargetContext.Should().Be("Product");
        task.IndexName.Should().Be("products");
        task.TriggeredBy.Should().Be("operator-001");
    }

    [Fact]
    public void Create_ShouldSetCreatedAt()
    {
        var before = DateTime.UtcNow;

        var task = IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, ValidTriggeredBy);

        task.CreatedAt.Should().BeCloseTo(before, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyTaskId_ShouldThrowTaskIdEmpty()
    {
        var act = () => IndexRebuildTask.Create(Guid.Empty, ValidTargetContext, ValidIndexName, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullTargetContext_ShouldThrowTargetContextEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, null!, ValidIndexName, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TARGET_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyTargetContext_ShouldThrowTargetContextEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, "", ValidIndexName, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TARGET_CONTEXT_EMPTY");
    }

    [Fact]
    public void Create_WithTargetContextTooLong_ShouldThrowTargetContextLength()
    {
        var longContext = new string('c', 129);

        var act = () => IndexRebuildTask.Create(ValidTaskId, longContext, ValidIndexName, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TARGET_CONTEXT_LENGTH");
    }

    [Fact]
    public void Create_WithTargetContextAtMaxLength_ShouldSucceed()
    {
        var context = new string('c', 128);

        var task = IndexRebuildTask.Create(ValidTaskId, context, ValidIndexName, ValidTriggeredBy);

        task.TargetContext.Should().Be(context);
    }

    [Fact]
    public void Create_WithNullIndexName_ShouldThrowIndexNameEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, null!, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_INDEX_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyIndexName_ShouldThrowIndexNameEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, "", ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_INDEX_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithIndexNameTooLong_ShouldThrowIndexNameLength()
    {
        var longName = new string('n', 257);

        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, longName, ValidTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_INDEX_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithIndexNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 256);

        var task = IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, name, ValidTriggeredBy);

        task.IndexName.Should().Be(name);
    }

    [Fact]
    public void Create_WithNullTriggeredBy_ShouldThrowTriggeredByEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TRIGGERED_BY_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyTriggeredBy_ShouldThrowTriggeredByEmpty()
    {
        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TRIGGERED_BY_EMPTY");
    }

    [Fact]
    public void Create_WithTriggeredByTooLong_ShouldThrowTriggeredByLength()
    {
        var longTriggeredBy = new string('t', 65);

        var act = () => IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, longTriggeredBy);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TRIGGERED_BY_LENGTH");
    }

    [Fact]
    public void Create_WithTriggeredByAtMaxLength_ShouldSucceed()
    {
        var triggeredBy = new string('t', 64);

        var task = IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, triggeredBy);

        task.TriggeredBy.Should().Be(triggeredBy);
    }

    #endregion

    #region Start

    [Fact]
    public void Start_FromCreated_ShouldTransitionToRunning()
    {
        var task = CreateTask();

        task.Start();

        task.Status.Should().Be(RebuildTaskStatus.Running);
        task.StartedAt.Should().NotBeNull();
        task.StartedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Start_FromRunning_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();
        task.Start();

        var act = () => task.Start();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_START_INVALID_STATUS");
    }

    [Fact]
    public void Start_FromCompleted_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        var act = () => task.Start();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_START_INVALID_STATUS");
    }

    [Fact]
    public void Start_FromFailed_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();
        task.Start();
        task.Fail("test error");

        var act = () => task.Start();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_START_INVALID_STATUS");
    }

    #endregion

    #region ReportProgress

    [Fact]
    public void ReportProgress_WithValidProgress_ShouldUpdateProgress()
    {
        var task = CreateTask();
        task.Start();

        task.ReportProgress(50);

        task.Progress.Should().Be(50);
    }

    [Fact]
    public void ReportProgress_WithZero_ShouldSetProgressToZero()
    {
        var task = CreateTask();
        task.Start();

        task.ReportProgress(0);

        task.Progress.Should().Be(0);
    }

    [Fact]
    public void ReportProgress_WithHundred_ShouldSetProgressToHundred()
    {
        var task = CreateTask();
        task.Start();

        task.ReportProgress(100);

        task.Progress.Should().Be(100);
    }

    [Fact]
    public void ReportProgress_WithNegative_ShouldThrowOutOfRange()
    {
        var task = CreateTask();
        task.Start();

        var act = () => task.ReportProgress(-1);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_PROGRESS_OUT_OF_RANGE");
    }

    [Fact]
    public void ReportProgress_WithOverHundred_ShouldThrowOutOfRange()
    {
        var task = CreateTask();
        task.Start();

        var act = () => task.ReportProgress(101);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_PROGRESS_OUT_OF_RANGE");
    }

    [Fact]
    public void ReportProgress_WhenNotRunning_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();

        var act = () => task.ReportProgress(50);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_PROGRESS_INVALID_STATUS");
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_FromRunning_ShouldTransitionToCompleted()
    {
        var task = CreateTask();
        task.Start();

        task.Complete();

        task.Status.Should().Be(RebuildTaskStatus.Completed);
        task.Progress.Should().Be(100);
        task.CompletedAt.Should().NotBeNull();
        task.CompletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Complete_WhenNotRunning_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();

        var act = () => task.Complete();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_COMPLETE_INVALID_STATUS");
    }

    [Fact]
    public void Complete_FromCompleted_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        var act = () => task.Complete();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_COMPLETE_INVALID_STATUS");
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_FromRunning_ShouldTransitionToFailed()
    {
        var task = CreateTask();
        task.Start();

        task.Fail("Connection timeout");

        task.Status.Should().Be(RebuildTaskStatus.Failed);
        task.ErrorMessage.Should().Be("Connection timeout");
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_WhenNotRunning_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();

        var act = () => task.Fail("error");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_FAIL_INVALID_STATUS");
    }

    [Fact]
    public void Fail_WithNullErrorMessage_ShouldThrowErrorMessageEmpty()
    {
        var task = CreateTask();
        task.Start();

        var act = () => task.Fail(null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_ERROR_MESSAGE_EMPTY");
    }

    [Fact]
    public void Fail_WithEmptyErrorMessage_ShouldThrowErrorMessageEmpty()
    {
        var task = CreateTask();
        task.Start();

        var act = () => task.Fail("");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_ERROR_MESSAGE_EMPTY");
    }

    [Fact]
    public void Fail_WithErrorMessageTooLong_ShouldThrowErrorMessageLength()
    {
        var task = CreateTask();
        task.Start();
        var longMessage = new string('e', 2001);

        var act = () => task.Fail(longMessage);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_ERROR_MESSAGE_LENGTH");
    }

    [Fact]
    public void Fail_WithErrorMessageAtMaxLength_ShouldSucceed()
    {
        var task = CreateTask();
        task.Start();
        var message = new string('e', 2000);

        task.Fail(message);

        task.ErrorMessage.Should().Be(message);
    }

    [Fact]
    public void Fail_ShouldTrimErrorMessage()
    {
        var task = CreateTask();
        task.Start();

        task.Fail("  Connection timeout  ");

        task.ErrorMessage.Should().Be("Connection timeout");
    }

    #endregion

    #region Retry

    [Fact]
    public void Retry_FromFailed_ShouldResetToCreatedAndIncrementRetryCount()
    {
        var task = CreateTask();
        task.Start();
        task.Fail("Connection timeout");

        task.Retry("operator-002");

        task.Status.Should().Be(RebuildTaskStatus.Created);
        task.TriggeredBy.Should().Be("operator-002");
        task.Progress.Should().Be(0);
        task.ErrorMessage.Should().BeNull();
        task.RetryCount.Should().Be(1);
        task.StartedAt.Should().BeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Retry_WhenNotFailed_ShouldThrowInvalidStatus()
    {
        var task = CreateTask();

        var act = () => task.Retry("operator-002");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_RETRY_INVALID_STATUS");
    }

    [Fact]
    public void Retry_WhenMaxRetriesExceeded_ShouldThrowMaxExceeded()
    {
        // Retry 3 times (max = 3), so the 4th attempt should fail
        var task = CreateTask();
        task.Start();
        task.Fail("error");
        task.Retry("operator-002");
        task.Start();
        task.Fail("error");
        task.Retry("operator-003");
        task.Start();
        task.Fail("error");
        task.Retry("operator-004");

        // Now retry count is 3, can't retry again
        task.Start();
        task.Fail("error");

        var act = () => task.Retry("operator-005");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_RETRY_MAX_EXCEEDED");
    }

    [Fact]
    public void Retry_WithNullTriggeredBy_ShouldThrowTriggeredByEmpty()
    {
        var task = CreateTask();
        task.Start();
        task.Fail("error");

        var act = () => task.Retry(null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REBUILD_TASK_TRIGGERED_BY_EMPTY");
    }

    #endregion

    #region CanRetry

    [Fact]
    public void CanRetry_WhenFailedAndRetryCountBelowMax_ShouldReturnTrue()
    {
        var task = CreateTask();
        task.Start();
        task.Fail("error");

        task.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenCreated_ShouldReturnFalse()
    {
        var task = CreateTask();

        task.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenRunning_ShouldReturnFalse()
    {
        var task = CreateTask();
        task.Start();

        task.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenCompleted_ShouldReturnFalse()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        task.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenRetryCountEqualsMax_ShouldReturnFalse()
    {
        var task = CreateTask();
        // Retry max count times (each retry: Start -> Fail -> Retry)
        for (int i = 0; i < IndexRebuildTask.MaxRetryCount; i++)
        {
            task.Start();
            task.Fail($"error {i}");
            task.Retry($"operator-{i + 2:000}");
        }

        // After all retries, RetryCount == MaxRetryCount, status is Created
        // Start and fail one more time to get to Failed state with RetryCount == MaxRetryCount
        task.Start();
        task.Fail("final error");

        task.CanRetry.Should().BeFalse();
    }

    #endregion

    #region State Machine

    [Fact]
    public void FullStateMachine_CreatedToRunningToCompleted()
    {
        var task = CreateTask();

        task.Status.Should().Be(RebuildTaskStatus.Created);

        task.Start();
        task.Status.Should().Be(RebuildTaskStatus.Running);

        task.Complete();
        task.Status.Should().Be(RebuildTaskStatus.Completed);
    }

    [Fact]
    public void FullStateMachine_CreatedToRunningToFailed()
    {
        var task = CreateTask();

        task.Start();
        task.Fail("error");

        task.Status.Should().Be(RebuildTaskStatus.Failed);
    }

    [Fact]
    public void FullStateMachine_CreatedToRunningToFailedToRetryToRunning()
    {
        var task = CreateTask();

        task.Start();
        task.Fail("error");
        task.Retry("operator-002");
        task.Start();

        task.Status.Should().Be(RebuildTaskStatus.Running);
        task.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Retry_ShouldIncrementRetryCountEachTime()
    {
        var task = CreateTask();

        task.Start();
        task.Fail("error 1");
        task.Retry("operator-002");
        task.RetryCount.Should().Be(1);

        task.Start();
        task.Fail("error 2");
        task.Retry("operator-003");
        task.RetryCount.Should().Be(2);

        task.Start();
        task.Fail("error 3");
        task.Retry("operator-004");
        task.RetryCount.Should().Be(3);
    }

    #endregion

    private static IndexRebuildTask CreateTask()
    {
        return IndexRebuildTask.Create(ValidTaskId, ValidTargetContext, ValidIndexName, ValidTriggeredBy);
    }
}
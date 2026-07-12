using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 索引重建任务聚合根，封装重建目标、进度与状态机转换不变量。
/// 同一索引（target_context + index_name）同时只允许一个运行中的任务。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>TaskId</c>。
/// 状态机：Created → Running → Completed/Failed → Retry → Running。
/// </summary>
public sealed class IndexRebuildTask : AggregateRoot
{
    private const int MaxTargetContextLength = 128;
    private const int MaxIndexNameLength = 256;
    private const int MaxTriggeredByLength = 64;
    private const int MaxErrorMessageLength = 2000;
    public const int MaxRetryCount = 3;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid TaskId => Id;

    /// <summary>目标上下文，如 "Product"、"Order"，≤128 字。</summary>
    public string TargetContext { get; private set; } = string.Empty;

    /// <summary>索引名称，如 "products"、"orders"，≤256 字。</summary>
    public string IndexName { get; private set; } = string.Empty;

    /// <summary>重建状态。</summary>
    public RebuildTaskStatus Status { get; private set; }

    /// <summary>触发操作者标识。</summary>
    public string TriggeredBy { get; private set; } = string.Empty;

    /// <summary>重建进度，0-100。</summary>
    public int Progress { get; private set; }

    /// <summary>错误信息，≤2000 字，可空。</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>已重试次数。</summary>
    public int RetryCount { get; private set; }

    /// <summary>创建时间（UTC）。</summary>
    public new DateTime CreatedAt { get; private set; }

    /// <summary>开始执行时间（UTC），可空。</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>完成时间（UTC），可空。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>是否允许重试。</summary>
    public bool CanRetry => Status == RebuildTaskStatus.Failed && RetryCount < MaxRetryCount;

    /// <summary>EF Core 无参构造。</summary>
    private IndexRebuildTask() { }

    private IndexRebuildTask(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验参数并创建任务，初始状态为 Created，进度为 0。
    /// </summary>
    /// <param name="taskId">任务标识，由应用层生成。</param>
    /// <param name="targetContext">目标上下文。</param>
    /// <param name="indexName">索引名称。</param>
    /// <param name="triggeredBy">触发操作者标识。</param>
    public static IndexRebuildTask Create(Guid taskId, string targetContext, string indexName, string triggeredBy)
    {
        if (taskId == Guid.Empty)
        {
            throw new SystemAdminDomainException("任务标识不可为空", "REBUILD_TASK_ID_EMPTY");
        }

        ValidateTargetContext(targetContext);
        ValidateIndexName(indexName);
        ValidateTriggeredBy(triggeredBy);

        return new IndexRebuildTask(taskId)
        {
            TargetContext = targetContext.Trim(),
            IndexName = indexName.Trim(),
            TriggeredBy = triggeredBy.Trim(),
            Status = RebuildTaskStatus.Created,
            Progress = 0,
            RetryCount = 0,
            ErrorMessage = null,
            CreatedAt = DateTime.UtcNow,
            StartedAt = null,
            CompletedAt = null
        };
    }

    /// <summary>
    /// 开始执行重建，状态从 Created 转为 Running。
    /// </summary>
    public void Start()
    {
        if (Status != RebuildTaskStatus.Created)
        {
            throw new SystemAdminDomainException("只有新建状态的任务可以开始执行", "REBUILD_TASK_START_INVALID_STATUS");
        }

        Status = RebuildTaskStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 报告重建进度，须在 Running 状态下，进度值须在 0-100 之间。
    /// </summary>
    /// <param name="progress">进度值，0-100。</param>
    public void ReportProgress(int progress)
    {
        if (Status != RebuildTaskStatus.Running)
        {
            throw new SystemAdminDomainException("只有运行中的任务可以报告进度", "REBUILD_TASK_PROGRESS_INVALID_STATUS");
        }

        if (progress < 0 || progress > 100)
        {
            throw new SystemAdminDomainException("进度值须在 0 到 100 之间", "REBUILD_TASK_PROGRESS_OUT_OF_RANGE");
        }

        Progress = progress;
    }

    /// <summary>
    /// 完成任务，状态从 Running 转为 Completed，进度置为 100。
    /// </summary>
    public void Complete()
    {
        if (Status != RebuildTaskStatus.Running)
        {
            throw new SystemAdminDomainException("只有运行中的任务可以完成", "REBUILD_TASK_COMPLETE_INVALID_STATUS");
        }

        Status = RebuildTaskStatus.Completed;
        Progress = 100;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 标记任务失败，状态从 Running 转为 Failed。
    /// </summary>
    /// <param name="errorMessage">错误信息。</param>
    public void Fail(string errorMessage)
    {
        if (Status != RebuildTaskStatus.Running)
        {
            throw new SystemAdminDomainException("只有运行中的任务可以标记失败", "REBUILD_TASK_FAIL_INVALID_STATUS");
        }

        ValidateErrorMessage(errorMessage);

        Status = RebuildTaskStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 重试失败任务，状态从 Failed 转为 Created（重置后重新开始），RetryCount 加 1。
    /// </summary>
    /// <param name="triggeredBy">触发操作者标识。</param>
    public void Retry(string triggeredBy)
    {
        if (!CanRetry)
        {
            if (Status != RebuildTaskStatus.Failed)
            {
                throw new SystemAdminDomainException("只有失败的任务可以重试", "REBUILD_TASK_RETRY_INVALID_STATUS");
            }

            throw new SystemAdminDomainException(
                $"重试次数已达上限 ({MaxRetryCount})，无法继续重试",
                "REBUILD_TASK_RETRY_MAX_EXCEEDED");
        }

        ValidateTriggeredBy(triggeredBy);

        Status = RebuildTaskStatus.Created;
        TriggeredBy = triggeredBy.Trim();
        Progress = 0;
        ErrorMessage = null;
        RetryCount++;
        StartedAt = null;
        CompletedAt = null;
    }

    private static void ValidateTargetContext(string targetContext)
    {
        if (string.IsNullOrWhiteSpace(targetContext))
        {
            throw new SystemAdminDomainException("目标上下文不可为空", "REBUILD_TASK_TARGET_CONTEXT_EMPTY");
        }

        if (targetContext.Trim().Length > MaxTargetContextLength)
        {
            throw new SystemAdminDomainException($"目标上下文长度不可超过 {MaxTargetContextLength} 字符", "REBUILD_TASK_TARGET_CONTEXT_LENGTH");
        }
    }

    private static void ValidateIndexName(string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new SystemAdminDomainException("索引名称不可为空", "REBUILD_TASK_INDEX_NAME_EMPTY");
        }

        if (indexName.Trim().Length > MaxIndexNameLength)
        {
            throw new SystemAdminDomainException($"索引名称长度不可超过 {MaxIndexNameLength} 字符", "REBUILD_TASK_INDEX_NAME_LENGTH");
        }
    }

    private static void ValidateTriggeredBy(string triggeredBy)
    {
        if (string.IsNullOrWhiteSpace(triggeredBy))
        {
            throw new SystemAdminDomainException("触发者标识不可为空", "REBUILD_TASK_TRIGGERED_BY_EMPTY");
        }

        if (triggeredBy.Trim().Length > MaxTriggeredByLength)
        {
            throw new SystemAdminDomainException($"触发者标识长度不可超过 {MaxTriggeredByLength} 字符", "REBUILD_TASK_TRIGGERED_BY_LENGTH");
        }
    }

    private static void ValidateErrorMessage(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new SystemAdminDomainException("错误信息不可为空", "REBUILD_TASK_ERROR_MESSAGE_EMPTY");
        }

        if (errorMessage.Trim().Length > MaxErrorMessageLength)
        {
            throw new SystemAdminDomainException($"错误信息长度不可超过 {MaxErrorMessageLength} 字符", "REBUILD_TASK_ERROR_MESSAGE_LENGTH");
        }
    }
}
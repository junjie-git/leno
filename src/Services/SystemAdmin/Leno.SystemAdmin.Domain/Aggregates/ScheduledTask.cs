using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 定时任务聚合根，封装任务元数据、Cron 表达式与运行状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>TaskId</c>。
/// </summary>
public sealed class ScheduledTask : AggregateRoot
{
    private const int MaxNameLength = 128;
    private const int MaxJobTypeLength = 256;
    private const int MaxCronLength = 128;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid TaskId => Id;

    /// <summary>任务名称，≤128 字。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>作业类型（程序集限定名），≤256 字。</summary>
    public string JobType { get; private set; } = string.Empty;

    /// <summary>Cron 表达式，≤128 字。</summary>
    public string CronExpression { get; private set; } = string.Empty;

    /// <summary>参数 JSON，可空。</summary>
    public string? Parameters { get; private set; }

    /// <summary>启停状态。</summary>
    public ScheduledTaskStatus Status { get; private set; }

    /// <summary>上次运行时间（UTC），可空。</summary>
    public DateTime? LastRunAt { get; private set; }

    /// <summary>上次运行状态。</summary>
    public TaskRunStatus LastRunStatus { get; private set; }

    /// <summary>下次运行时间（UTC），可空。</summary>
    public DateTime? NextRunAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ScheduledTask() { }

    private ScheduledTask(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验名称/作业类型/Cron，初始状态为 Disabled，LastRunStatus=Never。
    /// </summary>
    /// <param name="taskId">任务标识，由应用层生成。</param>
    /// <param name="name">任务名称。</param>
    /// <param name="jobType">作业类型（程序集限定名）。</param>
    /// <param name="cronExpression">Cron 表达式。</param>
    /// <param name="parameters">参数 JSON，可空。</param>
    public static ScheduledTask Create(Guid taskId, string name, string jobType, string cronExpression, string? parameters)
    {
        if (taskId == Guid.Empty)
        {
            throw new SystemAdminDomainException("任务标识不可为空", "TASK_ID_EMPTY");
        }

        ValidateName(name);
        ValidateJobType(jobType);
        ValidateCron(cronExpression);

        return new ScheduledTask(taskId)
        {
            Name = name.Trim(),
            JobType = jobType.Trim(),
            CronExpression = cronExpression.Trim(),
            Parameters = NormalizeNullable(parameters),
            Status = ScheduledTaskStatus.Disabled,
            LastRunStatus = TaskRunStatus.Never,
            LastRunAt = null,
            NextRunAt = null
        };
    }

    /// <summary>
    /// 更新名称、Cron 表达式与参数。
    /// </summary>
    /// <param name="name">任务名称。</param>
    /// <param name="cronExpression">Cron 表达式。</param>
    /// <param name="parameters">参数 JSON，可空。</param>
    public void Update(string name, string cronExpression, string? parameters)
    {
        ValidateName(name);
        ValidateCron(cronExpression);

        Name = name.Trim();
        CronExpression = cronExpression.Trim();
        Parameters = NormalizeNullable(parameters);
    }

    /// <summary>
    /// 启用任务并设置下次运行时间。
    /// </summary>
    /// <param name="nextRunAt">下次运行时间（UTC）。</param>
    public void Enable(DateTime nextRunAt)
    {
        if (nextRunAt == default)
        {
            throw new SystemAdminDomainException("下次运行时间不可为空", "TASK_NEXT_RUN_AT_EMPTY");
        }

        Status = ScheduledTaskStatus.Enabled;
        NextRunAt = nextRunAt;
    }

    /// <summary>停用任务。</summary>
    public void Disable()
    {
        Status = ScheduledTaskStatus.Disabled;
    }

    /// <summary>立即运行，置 LastRunStatus=Running 并记录当前时间为上次运行时间。</summary>
    public void RunNow()
    {
        LastRunStatus = TaskRunStatus.Running;
        LastRunAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 记录执行结果，更新 LastRunStatus 与 LastRunAt。结果详情不在领域层持久化。
    /// </summary>
    /// <param name="status">运行状态。</param>
    /// <param name="runAt">运行时间（UTC）。</param>
    /// <param name="result">运行结果摘要，领域层忽略（保留参数以兼容调用方）。</param>
    public void RecordExecution(TaskRunStatus status, DateTime runAt, string? result)
    {
        if (!Enum.IsDefined(status))
        {
            throw new SystemAdminDomainException("运行状态取值非法", "TASK_RUN_STATUS_INVALID");
        }

        if (runAt == default)
        {
            throw new SystemAdminDomainException("运行时间不可为空", "TASK_RUN_AT_EMPTY");
        }

        LastRunStatus = status;
        LastRunAt = runAt;
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("任务名称不可为空", "TASK_NAME_EMPTY");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"任务名称长度不可超过 {MaxNameLength} 字符", "TASK_NAME_LENGTH");
        }
    }

    private static void ValidateJobType(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
        {
            throw new SystemAdminDomainException("作业类型不可为空", "TASK_JOB_TYPE_EMPTY");
        }

        if (jobType.Trim().Length > MaxJobTypeLength)
        {
            throw new SystemAdminDomainException($"作业类型长度不可超过 {MaxJobTypeLength} 字符", "TASK_JOB_TYPE_LENGTH");
        }
    }

    private static void ValidateCron(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new SystemAdminDomainException("Cron 表达式不可为空", "TASK_CRON_EMPTY");
        }

        if (cronExpression.Trim().Length > MaxCronLength)
        {
            throw new SystemAdminDomainException($"Cron 表达式长度不可超过 {MaxCronLength} 字符", "TASK_CRON_LENGTH");
        }
    }
}

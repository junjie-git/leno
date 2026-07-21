using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 索引重建编排器实现，协调任务创建、执行、进度查询与重试。
/// 同一索引（targetContext + indexName）同时只允许一个运行中的任务。
/// </summary>
public sealed class IndexRebuildOrchestrator : IIndexRebuildOrchestrator
{
    private readonly IIndexRebuildTaskRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIndexRebuildTrigger _trigger;
    private readonly ILogger<IndexRebuildOrchestrator> _logger;

    public IndexRebuildOrchestrator(
        IIndexRebuildTaskRepository repository,
        IUnitOfWork unitOfWork,
        IIndexRebuildTrigger trigger,
        ILogger<IndexRebuildOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _trigger = trigger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTask> TriggerAsync(string targetContext, string indexName, string triggeredBy, CancellationToken ct)
    {
        // 检查同一索引是否已有运行中任务
        var existing = await _repository.GetRunningByIndexAsync(targetContext, indexName, ct);
        if (existing is not null)
        {
            throw new SystemAdminDomainException(
                $"索引 {targetContext}/{indexName} 已有运行中的重建任务（TaskId={existing.TaskId}），不可重复触发",
                "REBUILD_TASK_CONFLICT");
        }

        var taskId = Guid.NewGuid();
        var task = IndexRebuildTask.Create(taskId, targetContext, indexName, triggeredBy);
        task.Start();

        // 合并为单次事务：Create + Start + 持久化，避免中途失败导致状态不一致
        await _repository.AddAsync(task, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 触发底层索引重建操作；失败时标记任务为 Failed 并持久化
        try
        {
            await _trigger.StartAsync(taskId, targetContext, indexName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ES 索引重建触发失败，标记任务为 Failed TaskId={TaskId}", taskId);
            task.Fail(ex.Message);
            await _repository.UpdateAsync(task, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        _logger.LogInformation(
            "索引重建任务已创建并启动：TaskId={TaskId}, TargetContext={TargetContext}, IndexName={IndexName}",
            taskId, targetContext, indexName);

        return task;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTask> GetProgressAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct)
                   ?? throw new SystemAdminDomainException($"索引重建任务 {taskId} 不存在", "REBUILD_TASK_NOT_FOUND");

        // 如果任务正在运行，从底层触发器获取最新进度并更新
        if (task.Status == Domain.ValueObjects.RebuildTaskStatus.Running)
        {
            var progress = await _trigger.GetProgressAsync(taskId, ct);
            task.ReportProgress(progress);
            await _repository.UpdateAsync(task, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        return task;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTask> RetryAsync(Guid taskId, string triggeredBy, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct)
                   ?? throw new SystemAdminDomainException($"索引重建任务 {taskId} 不存在", "REBUILD_TASK_NOT_FOUND");

        // 重试前重新检查并发任务，避免与正在运行的任务竞争同一索引
        var concurrent = await _repository.GetRunningByIndexAsync(task.TargetContext, task.IndexName, ct);
        if (concurrent is not null && concurrent.TaskId != taskId)
        {
            throw new SystemAdminDomainException(
                $"索引 {task.TargetContext}/{task.IndexName} 已有运行中的重建任务（TaskId={concurrent.TaskId}），不可重试",
                "REBUILD_TASK_CONFLICT");
        }

        task.Retry(triggeredBy);
        task.Start();
        await _repository.UpdateAsync(task, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        try
        {
            await _trigger.StartAsync(taskId, task.TargetContext, task.IndexName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ES 索引重建重试触发失败，标记任务为 Failed TaskId={TaskId}", taskId);
            task.Fail(ex.Message);
            await _repository.UpdateAsync(task, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        _logger.LogInformation(
            "索引重建任务已重试：TaskId={TaskId}, RetryCount={RetryCount}, TargetContext={TargetContext}, IndexName={IndexName}",
            taskId, task.RetryCount, task.TargetContext, task.IndexName);

        return task;
    }
}
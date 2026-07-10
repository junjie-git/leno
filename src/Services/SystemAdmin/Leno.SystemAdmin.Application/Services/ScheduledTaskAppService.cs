using Leno.SystemAdmin.Application.Abstractions;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 定时任务管理应用服务实现。
/// 启用/停用/立即触发在聚合状态持久化后，委托 <see cref="IScheduledTaskExecutor"/> 同步底层调度器。
/// </summary>
public sealed class ScheduledTaskAppService : IScheduledTaskAppService
{
    private readonly IScheduledTaskRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScheduledTaskExecutor _executor;
    private readonly ILogger<ScheduledTaskAppService> _logger;

    public ScheduledTaskAppService(
        IScheduledTaskRepository repository,
        IUnitOfWork unitOfWork,
        IScheduledTaskExecutor executor,
        ILogger<ScheduledTaskAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _executor = executor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScheduledTaskDto> CreateAsync(SaveScheduledTaskDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var taskId = Guid.NewGuid();
        var entity = ScheduledTask.Create(taskId, dto.Name, dto.JobType, dto.CronExpression, dto.Parameters);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("定时任务已创建：{TaskId}（Name={Name}）", taskId, entity.Name);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ScheduledTaskDto> UpdateAsync(Guid taskId, UpdateScheduledTaskDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await RequireTaskAsync(taskId, ct);
        entity.Update(dto.Name, dto.CronExpression, dto.Parameters);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("定时任务已更新：{TaskId}", taskId);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid taskId, CancellationToken ct = default)
    {
        var entity = await RequireTaskAsync(taskId, ct);
        entity.Enable(DateTime.UtcNow);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        await _executor.ScheduleAsync(entity.Id, entity.JobType, entity.CronExpression, entity.Parameters, ct);

        _logger.LogInformation("定时任务已启用并注册调度：{TaskId}", taskId);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid taskId, CancellationToken ct = default)
    {
        var entity = await RequireTaskAsync(taskId, ct);
        entity.Disable();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        await _executor.UnscheduleAsync(entity.Id, ct);

        _logger.LogInformation("定时任务已停用并注销调度：{TaskId}", taskId);
    }

    /// <inheritdoc />
    public async Task RunNowAsync(Guid taskId, CancellationToken ct = default)
    {
        var entity = await RequireTaskAsync(taskId, ct);
        entity.RunNow();

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        await _executor.RunNowAsync(entity.Id, ct);

        _logger.LogInformation("定时任务已触发立即执行：{TaskId}", taskId);
    }

    /// <inheritdoc />
    public async Task<ScheduledTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(taskId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ScheduledTaskListResultDto> QueryAsync(string? name, ScheduledTaskStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(name, status, page, pageSize, ct);
        var total = await _repository.CountAsync(name, status, ct);

        return new ScheduledTaskListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<ScheduledTask> RequireTaskAsync(Guid taskId, CancellationToken ct)
        => await _repository.GetByIdAsync(taskId, ct)
           ?? throw new InvalidOperationException($"定时任务 {taskId} 不存在");

    private static ScheduledTaskDto ToDto(ScheduledTask entity)
        => new()
        {
            TaskId = entity.TaskId,
            Name = entity.Name,
            JobType = entity.JobType,
            CronExpression = entity.CronExpression,
            Parameters = entity.Parameters,
            Status = entity.Status,
            LastRunAt = entity.LastRunAt,
            LastRunStatus = entity.LastRunStatus,
            NextRunAt = entity.NextRunAt,
            UpdatedAt = entity.UpdatedAt
        };
}

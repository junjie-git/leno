using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 索引重建应用服务实现。
/// 委托编排器处理触发与重试，委托仓储处理查询。
/// </summary>
public sealed class IndexRebuildAppService : IIndexRebuildAppService
{
    private readonly IIndexRebuildOrchestrator _orchestrator;
    private readonly IIndexRebuildTaskRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IndexRebuildAppService> _logger;

    public IndexRebuildAppService(
        IIndexRebuildOrchestrator orchestrator,
        IIndexRebuildTaskRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<IndexRebuildAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _orchestrator = orchestrator;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTaskDto> TriggerAsync(TriggerIndexRebuildDto dto, string triggeredBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var task = await _orchestrator.TriggerAsync(dto.TargetContext, dto.IndexName, triggeredBy, ct);

        _logger.LogInformation("索引重建已触发：TaskId={TaskId}, TargetContext={TargetContext}, IndexName={IndexName}",
            task.TaskId, task.TargetContext, task.IndexName);

        return ToDto(task);
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _orchestrator.GetProgressAsync(taskId, ct);
        return task is null ? null : ToDto(task);
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTaskDto> RetryAsync(Guid taskId, string triggeredBy, CancellationToken ct = default)
    {
        var task = await _orchestrator.RetryAsync(taskId, triggeredBy, ct);

        _logger.LogInformation("索引重建已重试：TaskId={TaskId}, RetryCount={RetryCount}", taskId, task.RetryCount);

        return ToDto(task);
    }

    /// <inheritdoc />
    public async Task<IndexRebuildTaskListResultDto> QueryAsync(string? targetContext, RebuildTaskStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(targetContext, status, page, pageSize, ct);
        var total = await _repository.CountAsync(targetContext, status, ct);

        return new IndexRebuildTaskListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static IndexRebuildTaskDto ToDto(IndexRebuildTask entity)
        => new()
        {
            TaskId = entity.TaskId,
            TargetContext = entity.TargetContext,
            IndexName = entity.IndexName,
            Status = entity.Status,
            TriggeredBy = entity.TriggeredBy,
            Progress = entity.Progress,
            ErrorMessage = entity.ErrorMessage,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt
        };
}
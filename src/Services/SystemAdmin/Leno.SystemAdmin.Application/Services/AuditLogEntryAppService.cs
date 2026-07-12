using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 跨域审计日志条目查询应用服务实现。
/// 日志条目仅追加，本服务仅提供查询能力。
/// </summary>
public sealed class AuditLogEntryAppService : IAuditLogEntryAppService
{
    private readonly IAuditLogEntryRepository _repository;
    private readonly ILogger<AuditLogEntryAppService> _logger;

    public AuditLogEntryAppService(
        IAuditLogEntryRepository repository,
        ILogger<AuditLogEntryAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuditLogEntryListResultDto> QueryAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(moduleName, action, fromTime, toTime, operatorId, page, pageSize, ct);
        var total = await _repository.CountAsync(moduleName, action, fromTime, toTime, operatorId, ct);

        return new AuditLogEntryListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<AuditLogEntryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _repository.GetByIdAsync(id, ct);
        return entry is null ? null : ToDto(entry);
    }

    private static AuditLogEntryDto ToDto(AuditLogEntry entity)
        => new()
        {
            EntryId = entity.EntryId,
            EventId = entity.EventId,
            EventType = entity.EventType,
            AggregateId = entity.AggregateId,
            Module = entity.Module,
            Action = entity.Action,
            OperatorId = entity.OperatorId,
            OperatorName = entity.OperatorName,
            RequestSummary = entity.RequestSummary,
            Timestamp = entity.Timestamp,
            IpAddress = entity.IpAddress
        };
}
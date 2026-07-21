using System.Globalization;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Consumers;

/// <summary>
/// 售后事件消费者，将售后审核通过与退款完成事件落操作日志。
/// 审核通过事件以 SellerId 作为操作人；退款完成事件无操作人上下文，仅记录日志跳过。
/// </summary>
public sealed class AfterSalesEventConsumer :
    IConsumer<AfterSalesApprovedEvent>,
    IConsumer<RefundCompletedEvent>
{
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AfterSalesEventConsumer> _logger;

    public AfterSalesEventConsumer(
        IOperationLogRepository operationLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<AfterSalesEventConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(operationLogRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _operationLogRepository = operationLogRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        var evt = context.Message;

        if (evt.SellerId == Guid.Empty)
        {
            _logger.LogWarning("售后审核事件缺少操作人上下文，跳过操作日志记录 EventId={EventId}", evt.EventId);
            return;
        }

        // 幂等去重：按 EventId 检查是否已处理
        var existing = await _operationLogRepository.GetByEventIdAsync(evt.EventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("操作日志已存在，跳过 EventId={EventId}", evt.EventId);
            return;
        }

        var log = OperationLog.Create(
            Guid.NewGuid(),
            evt.SellerId,
            "Approve",
            "AfterSales",
            $"售后审核通过 AfterSalesId={evt.AfterSalesId}",
            null,
            $"{{\"afterSalesId\":\"{evt.AfterSalesId}\",\"amount\":{evt.ApprovedAmount.ToString(CultureInfo.InvariantCulture)}}}",
            null,
            evt.OccurredAt,
            evt.EventId);

        try
        {
            await _operationLogRepository.AddAsync(log, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex,
                "操作日志并发插入冲突，已按幂等处理 EventId={EventId}", evt.EventId);
            return;
        }

        _logger.LogInformation("操作日志已记录 EventId={EventId}", evt.EventId);
    }

    /// <summary>
    /// 判断 DbUpdateException 是否为唯一约束冲突（SQL Server 错误码 2601/2627），
    /// 兼容 PostgreSQL/MySQL 的错误消息。
    /// </summary>
    private static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_operation_logs_event_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task Consume(ConsumeContext<RefundCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;

        _logger.LogWarning("退款完成事件缺少操作人上下文，跳过操作日志记录 EventId={EventId} RefundId={RefundId}", evt.EventId, evt.RefundId);
        return Task.CompletedTask;
    }
}

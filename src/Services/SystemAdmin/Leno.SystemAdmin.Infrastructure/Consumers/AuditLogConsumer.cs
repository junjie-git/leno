using System.Globalization;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Consumers;

/// <summary>
/// 售后审核通过事件消费者，将审核操作落审计日志。
/// 事件缺少操作人（SellerId 为空）时跳过，避免 AuditLog.Create 抛出空操作人异常。
/// </summary>
public sealed class AuditLogConsumer : IConsumer<AfterSalesApprovedEvent>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditLogConsumer> _logger;

    public AuditLogConsumer(
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuditLogConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _auditLogRepository = auditLogRepository;
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
            _logger.LogWarning("售后审核事件缺少操作人上下文，跳过审计日志记录 EventId={EventId}", evt.EventId);
            return;
        }

        var log = AuditLog.Create(
            Guid.NewGuid(),
            evt.SellerId,
            "AfterSalesApprove",
            "AfterSales",
            evt.AfterSalesId.ToString(),
            $"售后审核通过 金额={evt.ApprovedAmount.ToString(CultureInfo.InvariantCulture)} {evt.Currency}",
            200,
            null,
            evt.EventId.ToString(),
            evt.OccurredAt);

        await _auditLogRepository.AddAsync(log, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("审计日志已记录 EventId={EventId}", evt.EventId);
    }
}

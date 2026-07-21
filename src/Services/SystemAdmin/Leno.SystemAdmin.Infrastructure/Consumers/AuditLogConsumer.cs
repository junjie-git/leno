using System.Globalization;
using System.Text.RegularExpressions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Consumers;

/// <summary>
/// 跨域审计日志消费者，消费各领域集成事件，生成 AuditLogEntry 审计日志条目。
/// 同时保留原有 AuditLog 操作审计日志写入。
/// 支持幂等去重：按 EventId 检查已存在的 AuditLogEntry。
/// </summary>
public sealed partial class AuditLogConsumer :
    IConsumer<AfterSalesApprovedEvent>,
    IConsumer<OrderCreatedEvent>,
    IConsumer<OrderPaidEvent>,
    IConsumer<OrderShippedEvent>,
    IConsumer<OrderCompletedEvent>,
    IConsumer<OrderCancelledEvent>,
    IConsumer<PaymentSucceededEvent>,
    IConsumer<PaymentFailedEvent>,
    IConsumer<RefundCompletedEvent>,
    IConsumer<UserRegisteredEvent>,
    IConsumer<SellerRegisteredEvent>,
    IConsumer<ShopApprovedEvent>,
    IConsumer<ShopSuspendedEvent>,
    IConsumer<ProductPublishedEvent>,
    IConsumer<ProductTakenDownEvent>,
    IConsumer<ReviewSubmittedEvent>,
    IConsumer<ReviewApprovedEvent>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAuditLogEntryRepository _auditLogEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditLogConsumer> _logger;

    public AuditLogConsumer(
        IAuditLogRepository auditLogRepository,
        IAuditLogEntryRepository auditLogEntryRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuditLogConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(auditLogEntryRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _auditLogRepository = auditLogRepository;
        _auditLogEntryRepository = auditLogEntryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region AfterSales

    public async Task Consume(ConsumeContext<AfterSalesApprovedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        if (evt.SellerId == Guid.Empty)
        {
            _logger.LogWarning("售后审核事件缺少操作人上下文，跳过审计日志记录 EventId={EventId}", evt.EventId);
            return;
        }

        var summary = MaskSensitiveData($"售后审核通过 金额={evt.ApprovedAmount.ToString(CultureInfo.InvariantCulture)} {evt.Currency}");
        await CreateAuditLogEntryAsync(evt.EventId, "AfterSalesApprovedEvent", evt.AggregateId, "AfterSales",
            "AfterSalesApprove", evt.SellerId, null, summary, evt.OccurredAt, null, ct);
    }

    #endregion

    #region Order

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "OrderCreatedEvent", evt.AggregateId, "Order",
            "OrderCreated", evt.BuyerId, null,
            MaskSensitiveData($"订单创建 金额={evt.TotalAmount} {evt.Currency}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "OrderPaidEvent", evt.AggregateId, "Order",
            "OrderPaid", evt.UserId, null,
            MaskSensitiveData($"订单支付成功 金额={evt.Amount} {evt.Currency} 渠道={evt.Channel}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "OrderShippedEvent", evt.AggregateId, "Order",
            "OrderShipped", evt.UserId, null,
            $"订单发货 物流单号={evt.LogisticsNo}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "OrderCompletedEvent", evt.AggregateId, "Order",
            "OrderCompleted", evt.UserId, null,
            MaskSensitiveData($"订单完成 金额={evt.TotalAmount} {evt.Currency}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "OrderCancelledEvent", evt.AggregateId, "Order",
            "OrderCancelled", Guid.Empty, null,
            $"订单取消 原因={evt.CancelReason} 取消方={evt.CancelledBy}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region Payment

    public async Task Consume(ConsumeContext<PaymentSucceededEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "PaymentSucceededEvent", evt.AggregateId, "Payment",
            "PaymentSucceeded", evt.UserId, null,
            MaskSensitiveData($"支付成功 金额={evt.Amount} {evt.Currency} 渠道={evt.Channel} 交易号={MaskTradeNo(evt.TradeNo)}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "PaymentFailedEvent", evt.AggregateId, "Payment",
            "PaymentFailed", evt.UserId, null,
            $"支付失败 原因={evt.Reason}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<RefundCompletedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "RefundCompletedEvent", evt.AggregateId, "Payment",
            "RefundCompleted", evt.UserId, null,
            MaskSensitiveData($"退款完成 金额={evt.RefundAmount} {evt.Currency}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region User

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "UserRegisteredEvent", evt.AggregateId, "User",
            "UserRegistered", evt.UserId, evt.Username,
            MaskSensitiveData($"用户注册 用户名={evt.Username}"),
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region Shop

    public async Task Consume(ConsumeContext<SellerRegisteredEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "SellerRegisteredEvent", evt.AggregateId, "Shop",
            "SellerRegistered", evt.SellerId, null,
            $"卖家入驻申请 店铺={evt.ShopName}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShopApprovedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ShopApprovedEvent", evt.AggregateId, "Shop",
            "ShopApproved", evt.SellerId, null,
            $"店铺审核通过 店铺={evt.ShopName}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShopSuspendedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ShopSuspendedEvent", evt.AggregateId, "Shop",
            "ShopSuspended", evt.SellerId, null,
            "店铺暂停运营",
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region Product

    public async Task Consume(ConsumeContext<ProductPublishedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ProductPublishedEvent", evt.AggregateId, "Product",
            "ProductPublished", evt.SellerId, null,
            "商品发布",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ProductTakenDownEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ProductTakenDownEvent", evt.AggregateId, "Product",
            "ProductTakenDown", evt.SellerId, null,
            "商品下架",
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region Review

    public async Task Consume(ConsumeContext<ReviewSubmittedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ReviewSubmittedEvent", evt.AggregateId, "Review",
            "ReviewSubmitted", evt.UserId, null,
            $"评价提交 评分={evt.Rating}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ReviewApprovedEvent> context)
    {
        var evt = context.Message;
        await CreateAuditLogEntryAsync(evt.EventId, "ReviewApprovedEvent", evt.AggregateId, "Review",
            "ReviewApproved", evt.UserId, null,
            $"评价审核通过 评分={evt.Rating}",
            evt.OccurredAt, null, context.CancellationToken);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 创建 AuditLogEntry 审计日志条目，支持幂等去重。
    /// 先按 EventId 查询快速路径跳过；并发插入时由 EventId 唯一索引兜底，
    /// 捕获 DbUpdateException 判定为唯一约束冲突则视为已存在并正常返回。
    /// </summary>
    private async Task CreateAuditLogEntryAsync(
        Guid eventId, string eventType, Guid aggregateId, string module,
        string action, Guid operatorId, string? operatorName,
        string? requestSummary, DateTime timestamp, string? ipAddress,
        CancellationToken ct)
    {
        // 幂等去重：按 EventId 检查是否已存在（快速路径）
        var existing = await _auditLogEntryRepository.GetByEventIdAsync(eventId, ct);
        if (existing is not null)
        {
            _logger.LogDebug("审计日志条目已存在，跳过 EventId={EventId}", eventId);
            return;
        }

        var entry = AuditLogEntry.Create(
            Guid.NewGuid(), eventId, eventType, aggregateId, module,
            action, operatorId, operatorName, requestSummary, timestamp, ipAddress);

        try
        {
            await _auditLogEntryRepository.AddAsync(entry, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("审计日志条目已记录 EventId={EventId} EventType={EventType}", eventId, eventType);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 并发插入导致唯一索引冲突，视为已存在，正常返回
            _logger.LogWarning(ex,
                "审计日志条目并发插入冲突，已按幂等处理 EventId={EventId}", eventId);
        }
    }

    /// <summary>
    /// 判断 DbUpdateException 是否为唯一约束冲突（SQL Server 错误码 2601/2627）。
    /// 同时匹配索引名 ix_audit_log_entries_event_id 与通用关键字作为兜底，
    /// 兼容 PostgreSQL/MySQL 等其他数据库的错误消息。
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        // SQL Server: 2601 (唯一键) / 2627 (违反约束)
        // PostgreSQL: duplicate key value violates unique constraint
        // MySQL: Duplicate entry
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("ix_audit_log_entries_event_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 脱敏敏感数据：密码、令牌、手机号等。
    /// </summary>
    internal static string MaskSensitiveData(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // 掩码密码字段
        input = PasswordPattern().Replace(input, "密码=******");
        // 掩码令牌字段
        input = TokenPattern().Replace(input, "token=******");
        // 掩码手机号（保留前3后4）
        input = PhonePattern().Replace(input, match =>
        {
            var phone = match.Value;
            return phone.Length >= 11 ? phone[..3] + "****" + phone[^4..] : "****";
        });

        return input;
    }

    /// <summary>
    /// 掩码交易号（保留前4后4）。
    /// </summary>
    internal static string MaskTradeNo(string tradeNo)
    {
        if (string.IsNullOrWhiteSpace(tradeNo) || tradeNo.Length <= 8) return "****";
        return tradeNo[..4] + "****" + tradeNo[^4..];
    }

    [GeneratedRegex(@"密码[=:]\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"token[=:]\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"1[3-9]\d{9}")]
    private static partial Regex PhonePattern();

    #endregion
}
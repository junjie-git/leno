using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Application.Services;

/// <summary>
/// 售后应用服务实现，编排售后申请、审核、撤销、退货、确认收货与查询用例。
/// 审核通过时经 <see cref="IEventBus"/> 发布 <see cref="RefundRequestedIntegrationEvent"/> 请求支付域退款。
/// 支付单标识与渠道通过 <see cref="IPaymentInfoQueryService"/> 防腐层查询。
/// </summary>
public sealed class AfterSalesAppService : IAfterSalesAppService
{
    private readonly IAfterSalesRepository _afterSalesRepository;
    private readonly IAfterSalesEligibilityChecker _eligibilityChecker;
    private readonly IPaymentInfoQueryService _paymentInfoQueryService;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AfterSalesAppService> _logger;

    public AfterSalesAppService(
        IAfterSalesRepository afterSalesRepository,
        IAfterSalesEligibilityChecker eligibilityChecker,
        IPaymentInfoQueryService paymentInfoQueryService,
        IEventBus eventBus,
        IUnitOfWork unitOfWork,
        ILogger<AfterSalesAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(afterSalesRepository);
        ArgumentNullException.ThrowIfNull(eligibilityChecker);
        ArgumentNullException.ThrowIfNull(paymentInfoQueryService);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _afterSalesRepository = afterSalesRepository;
        _eligibilityChecker = eligibilityChecker;
        _paymentInfoQueryService = paymentInfoQueryService;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AfterSalesDto> SubmitAfterSalesAsync(Guid userId, SubmitAfterSalesDto dto, CancellationToken ct = default)
    {
        await _eligibilityChecker.EnsureEligibleAsync(dto.OrderId, dto.OrderLineId, userId, dto.Type, ct);

        var afterSalesId = Guid.NewGuid();
        var afterSales = AfterSalesAggregate.Create(
            afterSalesId, dto.OrderId, dto.OrderLineId, userId, dto.SellerId,
            dto.Type, dto.ReasonCategory, dto.Reason, dto.Images,
            dto.RequestedAmount, dto.Currency);

        await _afterSalesRepository.AddAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("售后申请已提交 AfterSalesId={AfterSalesId} OrderId={OrderId} Type={Type}", afterSalesId, dto.OrderId, dto.Type);
        return ToDto(afterSales);
    }

    /// <inheritdoc />
    public async Task ApproveAfterSalesAsync(Guid afterSalesId, Guid operatorId, decimal approvedAmount, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        // 越权校验：仅归属卖家可审核
        RequireOwnedAfterSales(afterSales, operatorId);

        afterSales.Approve(operatorId, approvedAmount);

        // 仅退款类型直接进入退款流程，经发件箱模式发布退款请求集成事件
        if (afterSales.Type == AfterSalesType.RefundOnly)
        {
            afterSales.MarkRefunding();

            var paymentInfo = await _paymentInfoQueryService.GetByOrderIdAsync(afterSales.OrderId, ct)
                ?? throw new InvalidOperationException($"订单支付信息不存在 OrderId={afterSales.OrderId}");

            var refundId = Guid.NewGuid();
            afterSales.AddRefundRequestedEvent(
                refundId, paymentInfo.PaymentId, approvedAmount,
                paymentInfo.Channel, afterSales.Reason);

            _logger.LogInformation("卖家审核通过仅退款售后并已入发件箱 AfterSalesId={AfterSalesId} RefundId={RefundId}", afterSalesId, refundId);
        }
        else
        {
            _logger.LogInformation("售后审核通过（退货退款，等待买家退货） AfterSalesId={AfterSalesId} ApprovedAmount={ApprovedAmount}", afterSalesId, approvedAmount);
        }

        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        afterSales.Reject(operatorId, reason);
        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ConfirmReturnAsync(Guid afterSalesId, Guid operatorId, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        // 越权校验：仅归属卖家可确认退货
        RequireOwnedAfterSales(afterSales, operatorId);

        afterSales.ConfirmReturn();
        afterSales.MarkRefunding();

        // 查询支付单信息，经发件箱模式发布退款请求集成事件
        var paymentInfo = await _paymentInfoQueryService.GetByOrderIdAsync(afterSales.OrderId, ct)
            ?? throw new InvalidOperationException($"订单支付信息不存在 OrderId={afterSales.OrderId}");

        var refundId = Guid.NewGuid();
        var refundAmount = afterSales.ApprovedAmount ?? afterSales.RequestedAmount;
        afterSales.AddRefundRequestedEvent(
            refundId, paymentInfo.PaymentId, refundAmount,
            paymentInfo.Channel, afterSales.Reason);

        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("卖家确认收货并已入发件箱 AfterSalesId={AfterSalesId} RefundId={RefundId}", afterSalesId, refundId);
    }

    /// <inheritdoc />
    public async Task AdminApproveAfterSalesAsync(Guid afterSalesId, Guid operatorId, decimal approvedAmount, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        afterSales.Approve(operatorId, approvedAmount);

        // 仅退款类型直接进入退款流程，经发件箱模式发布退款请求集成事件
        if (afterSales.Type == AfterSalesType.RefundOnly)
        {
            afterSales.MarkRefunding();

            var paymentInfo = await _paymentInfoQueryService.GetByOrderIdAsync(afterSales.OrderId, ct)
                ?? throw new InvalidOperationException($"订单支付信息不存在 OrderId={afterSales.OrderId}");

            var refundId = Guid.NewGuid();
            afterSales.AddRefundRequestedEvent(
                refundId, paymentInfo.PaymentId, approvedAmount,
                paymentInfo.Channel, afterSales.Reason);
        }

        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("运营审核通过售后 AfterSalesId={AfterSalesId} ApprovedAmount={ApprovedAmount}", afterSalesId, approvedAmount);
    }

    /// <inheritdoc />
    public async Task AdminRejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        afterSales.Reject(operatorId, reason);
        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReturnGoodsAsync(Guid afterSalesId, Guid userId, string trackingNo, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        afterSales.ReturnGoods(trackingNo);
        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CancelAfterSalesAsync(Guid afterSalesId, Guid userId, string reason, CancellationToken ct = default)
    {
        var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
            ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

        afterSales.Cancel(userId, reason);
        await _afterSalesRepository.UpdateAsync(afterSales, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<AfterSalesDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var items = await _afterSalesRepository.GetByOrderIdAsync(orderId, ct);
        return items.ConvertAll(ToDto);
    }

    /// <inheritdoc />
    public async Task<AfterSalesListResultDto> GetByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _afterSalesRepository.QueryAsync(null, userId, null, null, page, pageSize, ct);
        var total = await _afterSalesRepository.CountAsync(null, userId, null, null, ct);
        return new AfterSalesListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task<AfterSalesListResultDto> GetBySellerAsync(Guid sellerId, AfterSalesStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _afterSalesRepository.QueryAsync(null, null, sellerId, status, page, pageSize, ct);
        var total = await _afterSalesRepository.CountAsync(null, null, sellerId, status, ct);
        return new AfterSalesListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task<AfterSalesListResultDto> QueryAsync(Guid? orderId, Guid? userId, Guid? sellerId, AfterSalesStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _afterSalesRepository.QueryAsync(orderId, userId, sellerId, status, page, pageSize, ct);
        var total = await _afterSalesRepository.CountAsync(orderId, userId, sellerId, status, ct);
        return new AfterSalesListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// 校验售后单归属卖家。非归属卖家抛领域异常，操作人标识为空抛领域异常。
    /// </summary>
    private static void RequireOwnedAfterSales(AfterSalesAggregate afterSales, Guid operatorId)
    {
        if (operatorId == Guid.Empty)
        {
            throw new ReviewDomainException("操作人标识不可为空", "OPERATOR_EMPTY");
        }
        if (afterSales.SellerId != operatorId)
        {
            throw new ReviewDomainException("无权操作此售后单", "AFTERSALES_NOT_OWNED");
        }
    }

    private static AfterSalesDto ToDto(AfterSalesAggregate afterSales)
    {
        return new AfterSalesDto
        {
            AfterSalesId = afterSales.Id,
            OrderId = afterSales.OrderId,
            OrderLineId = afterSales.OrderLineId,
            UserId = afterSales.UserId,
            SellerId = afterSales.SellerId,
            Type = afterSales.Type,
            ReasonCategory = afterSales.ReasonCategory,
            Reason = afterSales.Reason,
            Images = afterSales.Images,
            RequestedAmount = afterSales.RequestedAmount,
            Currency = afterSales.Currency,
            ApprovedAmount = afterSales.ApprovedAmount,
            RefundedAmount = afterSales.RefundedAmount,
            Status = afterSales.Status,
            AppliedAt = afterSales.AppliedAt,
            ApprovedAt = afterSales.ApprovedAt,
            ApproverId = afterSales.ApproverId,
            RefundedAt = afterSales.RefundedAt,
            ChannelRefundNo = afterSales.ChannelRefundNo,
            RejectReason = afterSales.RejectReason,
            FailReason = afterSales.FailReason,
            CancelledAt = afterSales.CancelledAt,
            CancelReason = afterSales.CancelReason
        };
    }
}

using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 退款应用服务实现，编排退款结果查询与运营管理用例。
/// </summary>
public sealed class RefundAppService : IRefundAppService
{
    private readonly IRefundOrderRepository _refundOrderRepository;

    public RefundAppService(IRefundOrderRepository refundOrderRepository)
    {
        ArgumentNullException.ThrowIfNull(refundOrderRepository);
        _refundOrderRepository = refundOrderRepository;
    }

    /// <inheritdoc />
    public async Task<RefundOrderDto?> GetRefundResultAsync(Guid afterSalesId, CancellationToken ct = default)
    {
        var refund = await _refundOrderRepository.GetByAfterSalesIdAsync(afterSalesId, ct);
        return refund is null ? null : ToDto(refund);
    }

    /// <inheritdoc />
    public async Task<RefundListResultDto> QueryRefundsAsync(
        Guid? orderId,
        RefundStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = await _refundOrderRepository.QueryAsync(orderId, status, page, pageSize, ct);
        var total = await _refundOrderRepository.CountAsync(orderId, status, ct);

        return new RefundListResultDto
        {
            Items = items.ConvertAll(ToDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static RefundOrderDto ToDto(Domain.Aggregates.RefundOrder refund)
    {
        return new RefundOrderDto
        {
            RefundId = refund.Id,
            OutRefundNo = refund.OutRefundNo,
            PaymentId = refund.PaymentId,
            OrderId = refund.OrderId,
            UserId = refund.UserId,
            AfterSalesId = refund.AfterSalesId,
            RefundAmount = refund.RefundAmount,
            Currency = refund.Currency,
            Channel = refund.Channel,
            ChannelRefundNo = refund.ChannelRefundNo,
            Status = refund.Status,
            RefundedAt = refund.RefundedAt,
            FailReason = refund.FailReason,
            CreatedAt = refund.CreatedAt,
            UpdatedAt = refund.UpdatedAt
        };
    }
}

using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 支付应用服务实现，编排支付结果查询、渠道状态主动查询与运营管理用例。
/// 主动查询渠道状态时通过 <see cref="IChannelStatusQueryService"/> 防腐层调用渠道适配器，
/// 若渠道返回已支付则补偿更新支付单聚合状态并经发件箱发布领域事件。
/// </summary>
public sealed class PaymentAppService : IPaymentAppService
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChannelStatusQueryService _channelStatusQueryService;
    private readonly ILogger<PaymentAppService> _logger;

    public PaymentAppService(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IChannelStatusQueryService channelStatusQueryService,
        ILogger<PaymentAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(paymentOrderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(channelStatusQueryService);
        ArgumentNullException.ThrowIfNull(logger);
        _paymentOrderRepository = paymentOrderRepository;
        _unitOfWork = unitOfWork;
        _channelStatusQueryService = channelStatusQueryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentOrderDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default)
    {
        var payment = await _paymentOrderRepository.GetByOrderIdAsync(orderId, ct);
        return payment is null ? null : ToDto(payment);
    }

    /// <inheritdoc />
    public async Task<ChannelStatusDto> QueryPaymentStatusAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _paymentOrderRepository.GetByIdAsync(paymentId, ct)
            ?? throw new InvalidOperationException($"支付单不存在 PaymentId={paymentId}");

        // 已支付或已关闭的支付单无需主动查询渠道
        if (payment.Status is PaymentStatus.Paid or PaymentStatus.Closed)
        {
            return new ChannelStatusDto
            {
                PaymentId = payment.Id,
                IsPaid = payment.Status == PaymentStatus.Paid,
                ChannelTradeNo = payment.ChannelTradeNo,
                PaidAt = payment.PaidAt
            };
        }

        var result = await _channelStatusQueryService.QueryPaymentStatusAsync(payment.Channel, payment.OutTradeNo, ct);

        // 补偿更新：渠道返回已支付但本地未更新
        if (result.IsPaid && payment.Status != PaymentStatus.Paid)
        {
            // 支付金额强校验：渠道查询实付金额必须与本地支付单金额一致。
            // 不一致视为风险事件，记录告警并进入人工对账队列，不调用 MarkSucceeded、不发布事件。
            if (!result.Amount.HasValue || result.Amount.Value != payment.Amount)
            {
                _logger.LogWarning("主动查询补偿金额不一致，进入人工对账队列 PaymentId={PaymentId} 期望金额={Expected} 实付金额={Actual}",
                    payment.Id, payment.Amount, result.Amount);
                return new ChannelStatusDto
                {
                    PaymentId = payment.Id,
                    IsPaid = false,
                    ChannelTradeNo = result.ChannelTradeNo,
                    PaidAt = result.PaidAt
                };
            }

            var tradeNo = result.ChannelTradeNo ?? payment.ChannelTradeNo ?? payment.OutTradeNo;
            payment.MarkSucceeded(tradeNo, result.Amount.Value, result.PaidAt ?? DateTime.UtcNow);
            await _paymentOrderRepository.UpdateAsync(payment, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
            _logger.LogInformation("主动查询补偿：支付单 {PaymentId} 已标记支付成功", payment.Id);
        }

        return new ChannelStatusDto
        {
            PaymentId = payment.Id,
            IsPaid = result.IsPaid,
            ChannelTradeNo = result.ChannelTradeNo,
            PaidAt = result.PaidAt
        };
    }

    /// <inheritdoc />
    public async Task<PaymentListResultDto> QueryPaymentsAsync(
        Guid? userId,
        PaymentChannel? channel,
        PaymentStatus? status,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = await _paymentOrderRepository.QueryAsync(userId, channel, status, startDate, endDate, page, pageSize, ct);
        var total = await _paymentOrderRepository.CountAsync(userId, channel, status, startDate, endDate, ct);

        return new PaymentListResultDto
        {
            Items = items.ConvertAll(ToDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static PaymentOrderDto ToDto(Domain.Aggregates.PaymentOrder payment)
    {
        return new PaymentOrderDto
        {
            PaymentId = payment.Id,
            OutTradeNo = payment.OutTradeNo,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Channel = payment.Channel,
            ChannelTradeNo = payment.ChannelTradeNo,
            Status = payment.Status,
            PrepayId = payment.PrepayId,
            CodeUrl = payment.CodeUrl,
            H5Url = payment.H5Url,
            ExpireAt = payment.ExpireAt,
            PaidAt = payment.PaidAt,
            FailReason = payment.FailReason,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };
    }
}

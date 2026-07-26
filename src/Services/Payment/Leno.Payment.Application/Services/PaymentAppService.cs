using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 支付应用服务实现，编排发起支付、支付结果查询、渠道状态主动查询与运营管理用例。
/// 主动查询渠道状态时通过 <see cref="IChannelStatusQueryService"/> 防腐层调用渠道适配器，
/// 若渠道返回已支付则补偿更新支付单聚合状态并经发件箱发布领域事件。
/// 同步发起支付时通过 <see cref="IPaymentOrderAntiCorruptionService"/> 防腐层校验订单，
/// 经 <see cref="IPaymentChannelFactory"/> 取得渠道适配器调用下单接口。
/// </summary>
public sealed class PaymentAppService : IPaymentAppService
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChannelStatusQueryService _channelStatusQueryService;
    private readonly IPaymentOrderAntiCorruptionService _orderAntiCorruptionService;
    private readonly IPaymentChannelFactory _channelFactory;
    private readonly ILogger<PaymentAppService> _logger;

    public PaymentAppService(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IChannelStatusQueryService channelStatusQueryService,
        IPaymentOrderAntiCorruptionService orderAntiCorruptionService,
        IPaymentChannelFactory channelFactory,
        ILogger<PaymentAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(paymentOrderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(channelStatusQueryService);
        ArgumentNullException.ThrowIfNull(orderAntiCorruptionService);
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _paymentOrderRepository = paymentOrderRepository;
        _unitOfWork = unitOfWork;
        _channelStatusQueryService = channelStatusQueryService;
        _orderAntiCorruptionService = orderAntiCorruptionService;
        _channelFactory = channelFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentInitiationResultDto> CreatePaymentAsync(
        Guid currentUserId,
        CreatePaymentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUserId == Guid.Empty)
        {
            throw new PaymentDomainException("当前用户未认证", "PAYMENT_USER_EMPTY");
        }

        if (request.OrderId == Guid.Empty)
        {
            throw new PaymentDomainException("OrderId 不可为空", "PAYMENT_ORDER_EMPTY");
        }

        // 1. 经防腐层校验订单存在性、归属、可支付状态与金额（INV-PAY-01）
        var orderContext = await _orderAntiCorruptionService.GetOrderPaymentContextAsync(request.OrderId, ct)
            ?? throw new PaymentDomainException(
                $"订单不存在 OrderId={request.OrderId}",
                "ORDER_NOT_FOUND");

        if (orderContext.UserId != currentUserId)
        {
            // 越权发起他人订单支付，按 spec AC-PAY-022 返回 403 语义（错误码 ORDER_FORBIDDEN 由全局异常中间件映射为 403）
            throw new PaymentDomainException(
                $"无权操作此订单 OrderId={request.OrderId}",
                "ORDER_FORBIDDEN");
        }

        if (!orderContext.IsPayable)
        {
            // 订单非待支付态（已支付/已取消/已完成等），拒绝发起支付返回 409 语义
            throw new PaymentDomainException(
                $"订单 {request.OrderId} 当前状态不可发起支付，仅待支付态可发起",
                "ORDER_NOT_PAYABLE");
        }

        if (orderContext.Amount <= 0)
        {
            throw new PaymentDomainException(
                $"订单 {request.OrderId} 应付金额非法 Amount={orderContext.Amount}",
                "PAYMENT_AMOUNT_INVALID");
        }

        // 2. 检查已有支付单，按状态分流（INV-PAY-04 单订单单活跃支付单）：
        //    - Paid：抛 PaymentAlreadySucceededException，订单域已通过本域发布的事件标记已支付
        //    - ChannelOrdered 且链接仍生效：幂等返回首次结果（链接已落库）
        //    - Pending/ChannelOrdered 已失效：MarkFailed 回收旧支付单，再创建新支付单
        //    - Failed/Closed：终态，直接创建新支付单
        var existing = await _paymentOrderRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.Paid)
            {
                _logger.LogWarning(
                    "发起支付：订单 {OrderId} 已由支付单 {PaymentId} 完成支付，拒绝重复发起",
                    request.OrderId, existing.Id);
                throw new PaymentAlreadySucceededException(existing.OrderId, existing.Id);
            }

            if (existing.HasActivePaymentLink())
            {
                _logger.LogInformation(
                    "发起支付：订单 {OrderId} 已存在生效支付单 PaymentId={PaymentId} Status={Status}，幂等返回首次结果",
                    request.OrderId, existing.Id, existing.Status);
                return ToInitiationResultDto(existing);
            }

            if (existing.Status == PaymentStatus.Pending || existing.Status == PaymentStatus.ChannelOrdered)
            {
                // 卡死 Pending 或 ChannelOrdered 已过期：MarkFailed 回收旧支付单，发布 PaymentFailedDomainEvent
                // 使订单保持待支付可重试，不触发 PaymentClosedDomainEvent 导致订单被取消
                existing.MarkFailed("用户重新发起支付，回收失效支付单");
                await _paymentOrderRepository.UpdateAsync(existing, ct);
                await _unitOfWork.SaveEntitiesAsync(ct);
                _logger.LogInformation(
                    "旧支付单已回收 PaymentId={PaymentId} 原 Status={Status}，准备创建新支付单 OrderId={OrderId}",
                    existing.Id, existing.Status, request.OrderId);
            }
            // Failed/Closed 终态：无需回收，直接创建新支付单
        }

        // 3. 解析支付渠道：未指定时按工厂首个启用渠道兜底（spec F-PAY-014）
        var channel = request.Channel ?? ResolveDefaultChannel();

        // 4. 创建支付单（Pending 态），金额取自订单域权威值（INV-PAY-01 金额一致）
        var scene = request.Scene ?? TradeType.Native;
        var paymentOrder = PaymentOrder.Create(
            Guid.NewGuid(),
            request.OrderId,
            currentUserId,
            orderContext.Amount,
            orderContext.Currency,
            channel,
            scene);

        // 5. 先持久化支付单（Pending 态），确保渠道下单成功后本地有记录可关联，
        //    避免渠道下单成功但本地保存失败时丢单（P0-6 修复，与 PaymentRequestedEventConsumer 一致）
        await _paymentOrderRepository.AddAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 6. 调用渠道下单获取预支付参数
        var adapter = _channelFactory.GetAdapter(channel);
        ChannelPaymentResult channelResult;
        try
        {
            channelResult = await adapter.CreatePaymentAsync(paymentOrder, ct);
        }
        catch (Exception ex)
        {
            // 渠道调用异常（网络超时、签名失败等）：标记支付单失败，发布 PaymentFailedDomainEvent 通知订单域保持待支付可重试
            _logger.LogError(ex,
                "渠道下单异常 PaymentId={PaymentId} OrderId={OrderId} Channel={Channel}",
                paymentOrder.Id, request.OrderId, channel);
            paymentOrder.MarkFailed($"渠道下单异常：{ex.Message}");
            await _paymentOrderRepository.UpdateAsync(paymentOrder, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
            return ToInitiationResultDto(paymentOrder);
        }

        // 7. 根据渠道返回更新支付单状态
        if (string.IsNullOrEmpty(channelResult.ChannelTradeNo))
        {
            paymentOrder.MarkFailed("渠道下单未返回交易号");
        }
        else
        {
            paymentOrder.MarkChannelOrdered(
                channelResult.ChannelTradeNo,
                channelResult.PrepayId,
                channelResult.CodeUrl,
                channelResult.H5Url);
        }

        await _paymentOrderRepository.UpdateAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "支付单已创建 OrderId={OrderId} PaymentId={PaymentId} OutTradeNo={OutTradeNo} Channel={Channel} Status={Status}",
            request.OrderId, paymentOrder.Id, paymentOrder.OutTradeNo, channel, paymentOrder.Status);

        return ToInitiationResultDto(paymentOrder);
    }

    /// <summary>
    /// 解析默认支付渠道：按工厂启用渠道列表的优先级取首个。
    /// 若无任何启用渠道，抛 <see cref="PaymentDomainException"/>（spec F-PAY-014 无可用渠道拒绝发起支付）。
    /// </summary>
    private PaymentChannel ResolveDefaultChannel()
    {
        var enabledChannels = _channelFactory.ListEnabledMetadata();
        if (enabledChannels.Count == 0)
        {
            throw new PaymentDomainException(
                "无可用支付渠道，请联系管理员启用至少一个渠道",
                "PAYMENT_CHANNEL_NOT_FOUND");
        }

        // 按工厂返回的优先级首个启用渠道的 ChannelKey 解析为 PaymentChannel 枚举
        var firstKey = enabledChannels[0].ChannelKey;
        if (Enum.TryParse<PaymentChannel>(firstKey, ignoreCase: true, out var channel))
        {
            return channel;
        }

        throw new PaymentDomainException(
            $"默认渠道 {firstKey} 无法映射为 PaymentChannel 枚举",
            "PAYMENT_CHANNEL_NOT_FOUND");
    }

    private static PaymentInitiationResultDto ToInitiationResultDto(PaymentOrder payment)
    {
        return new PaymentInitiationResultDto
        {
            PaymentOrderId = payment.Id,
            PaymentNo = payment.OutTradeNo,
            OrderId = payment.OrderId,
            Channel = payment.Channel,
            Status = payment.Status,
            PrepayId = payment.PrepayId,
            CodeUrl = payment.CodeUrl,
            H5Url = payment.H5Url,
            ExpireAt = payment.ExpireAt,
            FailReason = payment.FailReason
        };
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
                UserId = payment.UserId,
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
                    UserId = payment.UserId,
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
            UserId = payment.UserId,
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

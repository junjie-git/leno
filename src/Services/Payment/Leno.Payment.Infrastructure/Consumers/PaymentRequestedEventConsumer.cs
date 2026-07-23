using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Payment.Infrastructure.Consumers;

/// <summary>
/// 支付请求事件消费者，订单域在待支付订单发起支付时发布 <see cref="PaymentRequestedIntegrationEvent"/>。
/// 消费时创建支付单、先持久化支付单（Pending 态）、再调用渠道下单、最后更新状态并保存。
/// </summary>
/// <remarks>
/// P0-6 修复：原实现先调渠道下单再保存支付单，渠道下单成功但本地保存失败时支付单丢失，
/// 无法关联回调或对账，造成资金损失。正确顺序为先持久化支付单（Pending 态）再调渠道下单，
/// 即使后续保存失败，支付单已落库可由对账/关单补偿任务处理，且消息重试时被幂等检查跳过。
///
/// P1-4 修复：原幂等检查对任何已存在的支付单一律跳过，导致支付单卡在 Pending（渠道下单未完成）
/// 或 Failed/Closed 终态时用户无法重新发起支付。修复后按现有支付单状态分流：
/// <list type="bullet">
/// <item><see cref="PaymentStatus.Paid"/>：抛出 <see cref="PaymentAlreadySucceededException"/>，拒绝重复发起。</item>
/// <item><see cref="PaymentStatus.ChannelOrdered"/> 且支付链接仍生效：幂等跳过，复用现有支付单（链接已落库，查询侧可取）。</item>
/// <item><see cref="PaymentStatus.Pending"/>（卡死，渠道下单未完成）或 <see cref="PaymentStatus.ChannelOrdered"/> 已过期：
///   标记 <see cref="PaymentOrder.MarkFailed"/> 回收旧支付单（发布 PaymentFailedDomainEvent 使订单保持待支付可重试，
///   不触发 PaymentClosedDomainEvent 导致订单被取消），随后创建新支付单。</item>
/// <item><see cref="PaymentStatus.Failed"/>/<see cref="PaymentStatus.Closed"/>：终态，直接创建新支付单。</item>
/// <item>不存在：创建新支付单。</item>
/// </list>
/// 状态检查与创建新支付单在同一 <see cref="IUnitOfWork.SaveEntitiesAsync"/> 事务内完成，避免 TOCTOU。
/// </remarks>
public sealed class PaymentRequestedEventConsumer : IntegrationEventConsumerBase<PaymentRequestedIntegrationEvent>
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentChannelFactory _channelFactory;

    public PaymentRequestedEventConsumer(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IPaymentChannelFactory channelFactory,
        ILogger<PaymentRequestedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentRequestedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // P1-4：按现有支付单状态分流，避免卡死/终态支付单阻断重新发起
        var existing = await _paymentOrderRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (existing is not null)
        {
            // 已支付：拒绝重复发起，抛出业务异常暴露上游（订单域）对已支付订单重复发起的缺陷
            if (existing.Status == PaymentStatus.Paid)
            {
                Logger.LogWarning("支付请求事件：订单 {OrderId} 已由支付单 {PaymentId} 完成支付，拒绝重复发起",
                    integrationEvent.OrderId, existing.Id);
                throw new PaymentAlreadySucceededException(existing.OrderId, existing.Id);
            }

            // 渠道已下单且支付链接仍生效：幂等跳过，复用现有支付单（链接已落库，查询侧 GetPaymentResultAsync 可取）
            if (existing.HasActivePaymentLink())
            {
                Logger.LogInformation("支付请求事件：订单 {OrderId} 已存在生效支付单 PaymentId={PaymentId} Status={Status} Link={Link}，幂等跳过",
                    integrationEvent.OrderId, existing.Id, existing.Status, existing.GetActivePaymentLink());
                return;
            }

            // 中间态但已失效（卡死 Pending 渠道下单未完成，或 ChannelOrdered 链接已过期）：
            // 标记 MarkFailed 回收旧支付单。使用 MarkFailed 而非 MarkClosed，避免 PaymentClosedDomainEvent
            // 触发订单域取消订单；PaymentFailedDomainEvent 使订单保持待支付可重试，符合"重新发起支付"语义。
            if (existing.Status == PaymentStatus.Pending || existing.Status == PaymentStatus.ChannelOrdered)
            {
                existing.MarkFailed("用户重新发起支付，回收失效支付单");
                await _paymentOrderRepository.UpdateAsync(existing, ct);
                await _unitOfWork.SaveEntitiesAsync(ct);
                Logger.LogInformation("旧支付单已回收 PaymentId={PaymentId} 原 Status={Status}，准备创建新支付单 OrderId={OrderId}",
                    existing.Id, existing.Status, integrationEvent.OrderId);
            }
            // Failed/Closed 终态：无需回收，直接创建新支付单
        }

        if (!Enum.TryParse(integrationEvent.Channel, true, out PaymentChannel channel))
        {
            Logger.LogWarning("支付请求事件：不支持的支付渠道 Channel={Channel} OrderId={OrderId}，跳过",
                integrationEvent.Channel, integrationEvent.OrderId);
            return;
        }

        // 1. 创建支付单（Pending 态）
        var paymentOrder = PaymentOrder.Create(
            Guid.NewGuid(),
            integrationEvent.OrderId,
            integrationEvent.UserId,
            integrationEvent.Amount,
            integrationEvent.Currency,
            channel);

        // 2. 先持久化支付单（Pending 态），确保渠道下单成功后本地有记录可关联，
        //    避免渠道下单成功但本地保存失败时丢单。
        await _paymentOrderRepository.AddAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 3. 调用渠道下单
        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.CreatePaymentAsync(paymentOrder, ct);

        // 4. 根据渠道返回更新支付单状态
        if (string.IsNullOrEmpty(result.ChannelTradeNo))
        {
            paymentOrder.MarkFailed("渠道下单未返回交易号");
        }
        else
        {
            paymentOrder.MarkChannelOrdered(result.ChannelTradeNo, result.PrepayId, result.CodeUrl, result.H5Url);
        }

        // 5. 保存状态更新
        await _paymentOrderRepository.UpdateAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("支付单已创建 OrderId={OrderId} PaymentId={PaymentId} OutTradeNo={OutTradeNo} Channel={Channel}",
            integrationEvent.OrderId, paymentOrder.Id, paymentOrder.OutTradeNo, channel);
    }
}

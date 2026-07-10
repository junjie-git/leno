using Leno.Payment.Application.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;

namespace Leno.Payment.Infrastructure.Services;

/// <summary>
/// 渠道状态主动查询服务实现，通过 <see cref="PaymentChannelFactory"/> 获取对应渠道适配器，
/// 调用渠道查询接口返回支付/退款状态。作为防腐层桥接应用层与基础设施层。
/// </summary>
public sealed class ChannelStatusQueryService : IChannelStatusQueryService
{
    private readonly PaymentChannelFactory _channelFactory;

    public ChannelStatusQueryService(PaymentChannelFactory channelFactory)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        _channelFactory = channelFactory;
    }

    /// <inheritdoc />
    public async Task<ChannelStatusResult> QueryPaymentStatusAsync(PaymentChannel channel, string outTradeNo, CancellationToken ct = default)
    {
        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.QueryPaymentAsync(outTradeNo, ct);

        return new ChannelStatusResult
        {
            IsPaid = result.IsPaid,
            ChannelTradeNo = result.ChannelTradeNo,
            PaidAt = result.PaidAt
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundStatusResult> QueryRefundStatusAsync(PaymentChannel channel, string outRefundNo, CancellationToken ct = default)
    {
        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.QueryRefundAsync(outRefundNo, ct);

        return new ChannelRefundStatusResult
        {
            Succeeded = result.Succeeded,
            RefundedAt = result.RefundedAt
        };
    }
}

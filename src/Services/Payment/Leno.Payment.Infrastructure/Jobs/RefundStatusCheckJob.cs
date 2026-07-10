using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using RefundOrderAggregate = Leno.Payment.Domain.Aggregates.RefundOrder;

namespace Leno.Payment.Infrastructure.Jobs;

/// <summary>
/// 退款状态补偿任务，定期查询退款中态的退款单。
/// 主动调用渠道退款查询接口，若已到账则标记退款成功。
/// 由宿主（如 BackgroundService / Hangfire）定时调用 <see cref="ExecuteAsync"/>。
/// </summary>
public sealed class RefundStatusCheckJob
{
    private const int BatchSize = 100;

    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymentChannelFactory _channelFactory;
    private readonly ILogger<RefundStatusCheckJob> _logger;

    public RefundStatusCheckJob(
        IRefundOrderRepository refundOrderRepository,
        IUnitOfWork unitOfWork,
        PaymentChannelFactory channelFactory,
        ILogger<RefundStatusCheckJob> logger)
    {
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行一次退款状态补偿扫描。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var refundingOrders = await _refundOrderRepository.QueryAsync(
            null, RefundStatus.Refunding, 1, BatchSize, ct);

        _logger.LogInformation("退款状态补偿：待查退款单 {Count} 笔", refundingOrders.Count);

        foreach (var refund in refundingOrders)
        {
            await CheckAsync(refund, ct);
        }
    }

    private async Task CheckAsync(RefundOrderAggregate refund, CancellationToken ct)
    {
        try
        {
            var adapter = _channelFactory.GetAdapter(refund.Channel);
            var result = await adapter.QueryRefundAsync(refund.OutRefundNo, ct);

            if (!result.Succeeded)
            {
                return;
            }

            var channelRefundNo = !string.IsNullOrEmpty(refund.ChannelRefundNo)
                ? refund.ChannelRefundNo
                : refund.OutRefundNo;
            refund.MarkSucceeded(channelRefundNo, result.RefundedAt ?? DateTime.UtcNow);
            await _refundOrderRepository.UpdateAsync(refund, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("退款状态补偿：退款单已标记成功 OutRefundNo={OutRefundNo} RefundId={RefundId}",
                refund.OutRefundNo, refund.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退款状态补偿异常 OutRefundNo={OutRefundNo}", refund.OutRefundNo);
        }
    }
}

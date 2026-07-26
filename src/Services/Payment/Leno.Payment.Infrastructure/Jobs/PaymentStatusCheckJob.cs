using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Config;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentOrderAggregate = Leno.Payment.Domain.Aggregates.PaymentOrder;

namespace Leno.Payment.Infrastructure.Jobs;

/// <summary>
/// 支付状态补偿任务，定期查询长时间停留在待支付/渠道已下单态的支付单。
/// 超过 <see cref="PaymentJobOptions.ThresholdMinutes"/> 分钟仍未收到异步通知的支付单主动调用渠道查询接口，若已支付则标记成功。
/// 同时对 <see cref="PaymentOrderAggregate.ExpireAt"/> 已过期的支付单主动调用 <see cref="PaymentOrderAggregate.MarkClosed"/>
/// 关单，避免过期支付单堆积并被反复查询渠道。
/// 由宿主（如 BackgroundService / Hangfire）定时调用 <see cref="ExecuteAsync"/>。
/// 扫描阈值与批次大小通过 <see cref="PaymentJobOptions"/>（绑定 appsettings 中 <c>Payment:Jobs</c> 节）配置；
/// 未提供配置时回退到默认值（ThresholdMinutes=5，BatchSize=100），与原硬编码常量保持一致。
/// </summary>
public sealed class PaymentStatusCheckJob
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentChannelFactory _channelFactory;
    private readonly ILogger<PaymentStatusCheckJob> _logger;
    private readonly PaymentJobOptions _jobOptions;

    public PaymentStatusCheckJob(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IPaymentChannelFactory channelFactory,
        ILogger<PaymentStatusCheckJob> logger,
        IOptions<PaymentJobOptions>? options = null)
    {
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // DI 注册了 IOptions<PaymentJobOptions> 时注入配置值；直接构造（如单元测试）未传 options 时回退到默认值。
        _jobOptions = options?.Value ?? new PaymentJobOptions();
    }

    /// <summary>
    /// 执行一次支付状态补偿扫描。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_jobOptions.ThresholdMinutes);

        var pendingOrders = await _paymentOrderRepository.QueryAsync(
            null, null, PaymentStatus.Pending, null, threshold, 1, _jobOptions.BatchSize, ct);

        var channelOrderedOrders = await _paymentOrderRepository.QueryAsync(
            null, null, PaymentStatus.ChannelOrdered, null, threshold, 1, _jobOptions.BatchSize, ct);

        _logger.LogInformation("支付状态补偿：待查支付单 {PendingCount} 笔（待支付）+ {ChannelOrderedCount} 笔（渠道已下单）",
            pendingOrders.Count, channelOrderedOrders.Count);

        foreach (var order in pendingOrders)
        {
            await CheckAsync(order, ct);
        }

        foreach (var order in channelOrderedOrders)
        {
            await CheckAsync(order, ct);
        }

        // 过期关单：扫描 ExpireAt 已过期但仍处于 Pending/ChannelOrdered 态的支付单
        await CloseExpiredOrdersAsync(ct);
    }

    /// <summary>
    /// 扫描 ExpireAt 已过期的支付单并调用 <see cref="PaymentOrderAggregate.MarkClosed"/> 关单。
    /// 跳过渠道查询，直接关单以释放资源。
    /// </summary>
    private async Task CloseExpiredOrdersAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var page = 1;
        var closedCount = 0;

        while (true)
        {
            var expiredOrders = await _paymentOrderRepository.GetExpiredOrdersAsync(
                now, page, _jobOptions.BatchSize, ct);

            if (expiredOrders.Count == 0)
            {
                break;
            }

            foreach (var order in expiredOrders)
            {
                try
                {
                    order.MarkClosed("支付超时自动关闭");
                    await _paymentOrderRepository.UpdateAsync(order, ct);
                    closedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "支付状态补偿：过期关单异常 OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
                        order.OutTradeNo, order.Id);
                }
            }

            await _unitOfWork.SaveEntitiesAsync(ct);

            if (expiredOrders.Count < _jobOptions.BatchSize)
            {
                break;
            }

            page++;
        }

        if (closedCount > 0)
        {
            _logger.LogInformation("支付状态补偿：本次过期关单 {ClosedCount} 笔", closedCount);
        }
    }

    private async Task CheckAsync(PaymentOrderAggregate order, CancellationToken ct)
    {
        try
        {
            var adapter = _channelFactory.GetAdapter(order.Channel);
            var result = await adapter.QueryPaymentAsync(order.OutTradeNo, ct);

            if (!result.IsPaid)
            {
                return;
            }

            var channelTradeNo = !string.IsNullOrEmpty(result.ChannelTradeNo)
                ? result.ChannelTradeNo
                : order.ChannelTradeNo;
            if (string.IsNullOrEmpty(channelTradeNo))
            {
                _logger.LogWarning("支付状态补偿：缺少第三方交易号 OutTradeNo={OutTradeNo}", order.OutTradeNo);
                return;
            }

            // 支付金额强校验：渠道查询实付金额必须与本地支付单金额一致。
            // 不一致视为风险事件，记录告警并进入人工对账队列，不调用 MarkSucceeded。
            if (!result.Amount.HasValue || result.Amount.Value != order.Amount)
            {
                _logger.LogWarning("支付状态补偿金额不一致，进入人工对账队列 OutTradeNo={OutTradeNo} 期望金额={Expected} 实付金额={Actual}",
                    order.OutTradeNo, order.Amount, result.Amount);
                return;
            }

            order.MarkSucceeded(channelTradeNo, result.Amount.Value, result.PaidAt ?? DateTime.UtcNow);
            await _paymentOrderRepository.UpdateAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("支付状态补偿：支付单已标记成功 OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
                order.OutTradeNo, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "支付状态补偿异常 OutTradeNo={OutTradeNo}", order.OutTradeNo);
        }
    }
}

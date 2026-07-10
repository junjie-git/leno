using Leno.ReviewAfterSales.Application.Services;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 支付信息查询防腐层实现（占位）。
/// 实际实现应通过 HTTP 调用支付域 API 或直接查询支付域数据库获取支付单标识与渠道。
/// 当前为占位桩，返回 null（需按实际部署替换）。
/// </summary>
public sealed class PaymentInfoQueryService : IPaymentInfoQueryService
{
    private readonly ILogger<PaymentInfoQueryService> _logger;

    public PaymentInfoQueryService(ILogger<PaymentInfoQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PaymentInfoResult?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        _logger.LogWarning("支付信息查询为占位桩实现，返回 null OrderId={OrderId}", orderId);
        return Task.FromResult<PaymentInfoResult?>(null);
    }
}

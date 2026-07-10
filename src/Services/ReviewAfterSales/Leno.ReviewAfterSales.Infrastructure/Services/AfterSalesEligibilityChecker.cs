using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 售后资格校验器防腐层实现（占位）。
/// 实际实现应通过 HTTP 调用订单域 API 校验售后期限内、同订单行无进行中同类型售后单且申请人为订单买家。
/// 当前为占位桩，始终判定为可申请（不抛异常），仅记录警告日志。
/// </summary>
public sealed class AfterSalesEligibilityChecker : IAfterSalesEligibilityChecker
{
    private readonly ILogger<AfterSalesEligibilityChecker> _logger;

    public AfterSalesEligibilityChecker(ILogger<AfterSalesEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, AfterSalesType type, CancellationToken ct = default)
    {
        _logger.LogWarning("售后资格校验为占位桩实现，默认放行 OrderId={OrderId} OrderLineId={OrderLineId} UserId={UserId} Type={Type}",
            orderId, orderLineId, userId, type);

        return Task.CompletedTask;
    }
}

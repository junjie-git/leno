using Leno.Order.Application.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 商品域防腐层占位实现。
/// 实际部署中应通过 HTTP 调用商品域 API 或共享数据库查询 SKU 现价与可售状态，
/// 当前返回 null，下单时由应用层校验抛出异常。
/// </summary>
public sealed class ProductAntiCorruptionService : IProductAntiCorruptionService
{
    private readonly ILogger<ProductAntiCorruptionService> _logger;

    public ProductAntiCorruptionService(ILogger<ProductAntiCorruptionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
    {
        _logger.LogDebug("商品域防腐层占位查询 SkuId={SkuId}，返回 null", skuId);
        return Task.FromResult<SkuInfo?>(null);
    }
}

/// <summary>
/// 促销域防腐层占位实现。
/// 实际部署中应通过 HTTP 调用促销域 API 查询适用优惠并返回分摊结果，
/// 当前返回 0 优惠。
/// </summary>
public sealed class PromotionAntiCorruptionService : IPromotionAntiCorruptionService
{
    private readonly ILogger<PromotionAntiCorruptionService> _logger;

    public PromotionAntiCorruptionService(ILogger<PromotionAntiCorruptionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<decimal> CalculateDiscountAsync(
        Guid userId,
        List<(Guid SkuId, decimal Subtotal)> items,
        CancellationToken ct = default)
    {
        _logger.LogDebug("促销域防腐层占位查询 UserId={UserId}，返回 0 优惠", userId);
        return Task.FromResult(0m);
    }
}

/// <summary>
/// 积分域防腐层占位实现。
/// 实际部署中应通过 HTTP 调用积分域 API 试算/冻结/释放积分，
/// 当前 TryOffsetAsync 返回 0，Freeze/Release 不执行任何操作。
/// </summary>
public sealed class PointsAntiCorruptionService : IPointsAntiCorruptionService
{
    private readonly ILogger<PointsAntiCorruptionService> _logger;

    public PointsAntiCorruptionService(ILogger<PointsAntiCorruptionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
    {
        _logger.LogDebug("积分域防腐层占位试算 UserId={UserId} Points={Points}，返回 0", userId, pointsToUse);
        return Task.FromResult(0m);
    }

    /// <inheritdoc />
    public Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
    {
        _logger.LogDebug("积分域防腐层占位冻结 UserId={UserId} OrderId={OrderId} Points={Points}", userId, orderId, pointsToUse);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        _logger.LogDebug("积分域防腐层占位释放 OrderId={OrderId}", orderId);
        return Task.CompletedTask;
    }
}

using Leno.Cart.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车价格防腐层实现。
/// 当前为占位实现：商品域 API 未就绪时返回默认快照（Available=true、Price=0）。
/// 后续接入商品域 API/防腐层后替换为真实查询。
/// </summary>
public sealed class CartPriceService : ICartPriceService
{
    private readonly ILogger<CartPriceService> _logger;

    public CartPriceService(ILogger<CartPriceService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkuPriceSnapshot>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        var ids = skuIds.ToList();

        // 占位实现：商品域 API 就绪后替换为真实查询（HttpClient 调用商品域 IProductQueryService）
        _logger.LogDebug("购物车价格防腐层占位查询，SKU 数量={Count}", ids.Count);

        var snapshots = ids.Select(id => new SkuPriceSnapshot
        {
            SkuId = id,
            Price = 0,
            Currency = "CNY",
            Available = true,
            Title = string.Empty,
            MainImageUrl = string.Empty,
            SellerId = Guid.Empty
        }).ToList();

        return Task.FromResult<IReadOnlyList<SkuPriceSnapshot>>(snapshots);
    }
}

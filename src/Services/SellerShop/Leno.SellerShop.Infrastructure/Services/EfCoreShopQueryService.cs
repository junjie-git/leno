using Leno.SellerShop.Domain.Services;
using Leno.SellerShop.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Services;

/// <summary>
/// 店铺查询防腐层实现，供商品域等下游上下文查询店铺可售状态。
/// 直接读 Shop 表（只读，AsNoTracking），不暴露店铺聚合内部结构。
/// </summary>
public sealed class EfCoreShopQueryService : IShopQueryService
{
    private readonly SellerShopDbContext _context;

    public EfCoreShopQueryService(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ShopStatus?> GetShopStatusAsync(Guid shopId, CancellationToken ct = default)
    {
        var status = await _context.Shops
            .AsNoTracking()
            .Where(s => s.Id == shopId)
            .Select(s => (ShopStatus?)s.Status)
            .FirstOrDefaultAsync(ct);

        return status;
    }
}

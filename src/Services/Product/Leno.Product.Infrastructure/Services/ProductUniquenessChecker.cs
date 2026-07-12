using Leno.Product.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Services;

/// <summary>
/// 商品唯一性校验器 EF Core 实现，查询数据库校验 SKU 编码全局唯一与标题同店铺唯一。
/// </summary>
public sealed class ProductUniquenessChecker : IProductUniquenessChecker
{
    private readonly ProductDbContext _context;

    public ProductUniquenessChecker(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<bool> IsSkuCodeUniqueAsync(string skuCode, Guid? excludeProductId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(skuCode))
        {
            return false;
        }

        var query = _context.SPUs
            .AsNoTracking()
            .Where(s => s.SKUs.Any(sk => sk.SkuCode == skuCode.Trim()));

        if (excludeProductId.HasValue)
        {
            query = query.Where(s => s.Id != excludeProductId.Value);
        }

        return !await query.AnyAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsTitleUniqueInShopAsync(string title, Guid shopId, Guid? excludeProductId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var query = _context.SPUs
            .AsNoTracking()
            .Where(s => s.ShopId == shopId && s.Title == title.Trim());

        if (excludeProductId.HasValue)
        {
            query = query.Where(s => s.Id != excludeProductId.Value);
        }

        return !await query.AnyAsync(ct);
    }
}
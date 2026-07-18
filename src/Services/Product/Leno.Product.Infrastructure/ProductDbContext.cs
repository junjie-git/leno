using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure;

/// <summary>
/// 商品域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 SPU、Category、Brand、StockBaseline 聚合与 OutboxMessage 发件箱表的 DbSet。
/// SKU 作为 SPU 聚合内实体经 HasMany 映射，不单独暴露 DbSet。
/// </summary>
public sealed class ProductDbContext : BaseDbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
    {
    }

    /// <summary>商品 SPU 聚合根。</summary>
    public DbSet<SPU> SPUs => Set<SPU>();

    /// <summary>商品分类聚合根。</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>商品品牌聚合根。</summary>
    public DbSet<Brand> Brands => Set<Brand>();

    /// <summary>库存基线聚合根。</summary>
    public DbSet<StockBaseline> StockBaselines => Set<StockBaseline>();

    /// <summary>价格历史聚合根（从 SPU 拆分）。</summary>
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
}

using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Cart.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure;

/// <summary>
/// 购物车域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 Cart 聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class CartDbContext : BaseDbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options)
    {
    }

    /// <summary>购物车聚合根。</summary>
    public DbSet<CartAggregate> Carts => Set<CartAggregate>();
}

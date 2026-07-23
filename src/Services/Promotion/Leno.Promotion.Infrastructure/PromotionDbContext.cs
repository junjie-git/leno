using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Promotion.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Promotion.Infrastructure;

/// <summary>
/// 促销域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 PromotionActivity、Coupon、UserCoupon、SeckillActivity 聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public class PromotionDbContext : BaseDbContext
{
    public PromotionDbContext(DbContextOptions<PromotionDbContext> options) : base(options)
    {
    }

    /// <summary>满减/促销活动聚合根。</summary>
    public DbSet<PromotionActivity> PromotionActivities => Set<PromotionActivity>();

    /// <summary>优惠券模板聚合根。</summary>
    public DbSet<Coupon> Coupons => Set<Coupon>();

    /// <summary>用户优惠券聚合根。</summary>
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();

    /// <summary>秒杀活动聚合根。</summary>
    public DbSet<SeckillActivity> SeckillActivities => Set<SeckillActivity>();

    /// <summary>秒杀预占记录表，跟踪 Redis 预扣后的履约状态。</summary>
    public DbSet<SeckillPreOccupationRecord> SeckillPreOccupationRecords => Set<SeckillPreOccupationRecord>();

    /// <summary>
    /// 促销规则定义聚合根，存储"规则类型 + 优先级 + 叠加策略 + JSON 规则体"四要素。
    /// 由 <c>JsonRuleLoader</c> 加载并供 <see cref="Leno.Promotion.Domain.Rules.IRuleEngine"/> 编排时使用。
    /// </summary>
    public DbSet<PromotionRuleDefinition> PromotionRuleDefinitions => Set<PromotionRuleDefinition>();
}

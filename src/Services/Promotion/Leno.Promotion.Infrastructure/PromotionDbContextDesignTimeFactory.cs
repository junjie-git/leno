using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.Promotion.Infrastructure;

/// <summary>
/// Promotion BC 设计期 DbContext 工厂，从环境变量读取连接字符串。
/// 不再硬编码 SA 密码，消除源码泄露风险（ADR 与安全审计统一要求）。
/// </summary>
public sealed class PromotionDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<PromotionDbContext>
{
    public override PromotionDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoPromotion");
        return new PromotionDbContext(builder.Options);
    }
}

using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure;

/// <summary>
/// SellerShop BC 设计期 DbContext 工厂，从环境变量读取连接字符串。
/// 不再硬编码 SA 密码，消除源码泄露风险（ADR 与安全审计统一要求）。
/// </summary>
public sealed class SellerShopDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<SellerShopDbContext>
{
    public override SellerShopDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoSellerShop");
        return new SellerShopDbContext(builder.Options);
    }
}

using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure;

/// <summary>
/// Inventory BC 设计期 DbContext 工厂，从环境变量读取连接字符串。
/// 不再硬编码 SA 密码，消除源码泄露风险（与 Order/Product BC 设计期工厂保持一致）。
/// </summary>
public sealed class InventoryDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<InventoryDbContext>
{
    public override InventoryDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoInventory");
        return new InventoryDbContext(builder.Options);
    }
}

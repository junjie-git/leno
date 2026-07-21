using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure;

/// <summary>
/// UserAuth BC 设计期 DbContext 工厂，从环境变量读取连接字符串。
/// 不再硬编码 SA 密码，消除源码泄露风险（ADR 与安全审计统一要求）。
/// </summary>
public sealed class UserAuthDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<UserAuthDbContext>
{
    public override UserAuthDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoUserAuth");
        return new UserAuthDbContext(builder.Options);
    }
}

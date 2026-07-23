using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.AccessControl.Infrastructure;

/// <summary>
/// AccessControl BC 设计时 DbContext 工厂，从环境变量读取连接字符串。
/// 供 EF Core 迁移命令（dotnet ef migrations add）使用。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class AccessControlDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<AccessControlDbContext>
{
    public override AccessControlDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoAccessControl");
        return new AccessControlDbContext(builder.Options);
    }
}

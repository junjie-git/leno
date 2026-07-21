using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 设计期 DbContext 工厂基类，统一从环境变量读取连接字符串，消除各 BC 硬编码 SA 密码的安全风险。
/// 各 BC 的 XxxDbContextDesignTimeFactory 继承此类，仅需实现 CreateDbContext 与提供 DbContext 类型参数。
/// </summary>
/// <typeparam name="TContext">DbContext 派生类型。</typeparam>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    private const string ConnectionStringEnvVar = "LENO_DESIGNTIME_CONNECTION_STRING";

    /// <summary>
    /// 解析连接字符串，优先从 LENO_DESIGNTIME_CONNECTION_STRING 环境变量读取。
    /// 未配置时抛 <see cref="InvalidOperationException"/>，避免回退到硬编码密码。
    /// </summary>
    /// <param name="databaseName">数据库名（仅用于错误提示，不参与拼接）。</param>
    /// <returns>连接字符串。</returns>
    /// <exception cref="InvalidOperationException">环境变量未设置时抛出。</exception>
    public static string ResolveConnectionString(string databaseName)
    {
        var connStr = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                $"设计期工厂需要环境变量 {ConnectionStringEnvVar} 才能生成迁移。" +
                $"请在本地设置：export {ConnectionStringEnvVar}='Server=localhost,1433;Database={databaseName};User Id=sa;Password=<YOUR_PASSWORD>;TrustServerCertificate=True'" +
                $"。CI 流水线会自动注入该变量。详细说明见 docs/handbook/06-storage-and-cache.md。");
        }
        return connStr;
    }

    /// <summary>
    /// 创建设计期 DbContext 实例。
    /// 子类应覆盖此方法以指定 DbContextOptions 配置（如 UseSqlServer vs UseNpgsql）。
    /// </summary>
    public abstract TContext CreateDbContext(string[] args);

    /// <summary>
    /// 构建 DbContextOptions，使用从环境变量解析的连接字符串。
    /// 子类在 CreateDbContext 中调用此方法。
    /// </summary>
    protected DbContextOptionsBuilder<TContext> CreateOptionsBuilder(string databaseName)
    {
        var connStr = ResolveConnectionString(databaseName);
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseSqlServer(connStr, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
        });
        return builder;
    }
}

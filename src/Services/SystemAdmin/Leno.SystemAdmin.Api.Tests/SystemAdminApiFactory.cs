using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Api;
using Leno.SystemAdmin.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Api.Tests;

/// <summary>
/// SystemAdmin API 集成测试工厂（spec §6.9）。
/// 替换 DbContext 为 SQLite in-memory、IConnectionMultiplexer 为测试容器 Redis、
/// ICurrentUserContext 为测试用户（Admin 角色）。
/// </summary>
public sealed class SystemAdminApiFactory : WebApplicationFactory<Program>
{
    public Guid TestUserId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string TestRole { get; set; } = "Admin";
    public Guid? TestShopId { get; set; }
    public string? TestSessionId { get; set; } = "test-session-id-001";

    private SqliteConnection? _sqliteConnection;
    private string? _redisConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // 替换 DbContext 为 SQLite in-memory。
            // 必须保持 SqliteConnection 打开，否则 :memory: 数据库会在连接关闭时被销毁。
            // 旧选项（DbContextOptions<SystemAdminDbContext> / DbContextOptions / SystemAdminDbContext）需全部移除，
            // 否则 AddLenoApi 中 UseSqlServer 的旧注册会因 TryAdd 而阻止 SQLite 注册生效。
            services.RemoveAll<DbContextOptions<SystemAdminDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SystemAdminDbContext>();
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();
            services.AddSingleton(_sqliteConnection);
            services.AddDbContext<SystemAdminDbContext>(opt =>
                opt.UseSqlite(_sqliteConnection));

            // 替换 ICurrentUserContext 为测试用户
            services.RemoveAll<ICurrentUserContext>();
            services.AddScoped(_ => new TestCurrentUserContext
            {
                UserId = TestUserId,
                Role = TestRole,
                ShopId = TestShopId,
                SessionId = TestSessionId,
                IsAuthenticated = true
            });

            // 替换 IConnectionMultiplexer 为测试容器 Redis（由 OverrideRedis 设置；默认不替换，保留 AddLenoApi 中的真实注册或测试用例自定义）
            if (_redisConnectionString is not null)
            {
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_ =>
                    ConnectionMultiplexer.Connect(_redisConnectionString));
            }
        });
    }

    /// <summary>用指定 Redis 连接替换 IConnectionMultiplexer。必须在访问 Services/CreateClient 之前调用。</summary>
    public SystemAdminApiFactory OverrideRedis(string redisConnectionString)
    {
        _redisConnectionString = redisConnectionString;
        return this;
    }

    /// <summary>初始化数据库（创建表 + 可选种子数据）。需在测试用例构造工厂后、发送请求前调用一次。</summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SystemAdminDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sqliteConnection?.Dispose();
            _sqliteConnection = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>测试用 ICurrentUserContext 实现，按工厂配置返回固定身份信息。</summary>
public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public Guid? ShopId { get; set; }
    public string? SessionId { get; set; }
    public bool IsAuthenticated { get; set; }
}

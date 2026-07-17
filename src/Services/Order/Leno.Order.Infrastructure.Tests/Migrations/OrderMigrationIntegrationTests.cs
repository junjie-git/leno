using Leno.Infrastructure.Persistence;
using Leno.Order.Infrastructure;
using Leno.Testing.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Order.Infrastructure.Tests.Migrations;

public class OrderMigrationIntegrationTests : DatabaseMigrationTestBase<OrderDbContext>
{
    public OrderMigrationIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(sqlConnectionString));
    }

    [Fact]
    public async Task MigrateWithLockAsync_OnEmptyDatabase_CreatesAllTables()
    {
        // Arrange & Act：InitializeAsync 已执行 MigrateWithLockAsync<OrderDbContext>

        // Assert：查询 __EFMigrationsHistory 与 Orders 表存在
        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue("迁移后应能连接数据库");

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty("迁移后应无 pending migrations");

        // 验证关键表已创建
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'").ToListAsync();
        tables.Should().Contain(new[] { "Orders", "OrderItems", "OutboxMessages", "__EFMigrationsHistory" });
    }

    [Fact]
    public async Task MigrateWithLockAsync_Idempotent_RunTwiceNoError()
    {
        // 第二次调用应无 pending migrations，无错误
        await Provider.MigrateWithLockAsync<OrderDbContext>();

        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("重复执行迁移后仍无 pending");
    }
}

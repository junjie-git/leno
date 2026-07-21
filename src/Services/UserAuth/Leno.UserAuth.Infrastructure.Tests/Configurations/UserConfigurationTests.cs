using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Tests.Configurations;

/// <summary>
/// UserConfiguration 索引过滤器语法验证测试。
/// 验证 Email/Phone 唯一索引使用 SQL Server 方括号语法而非 PostgreSQL 双引号语法，
/// 避免在 UseSqlServer 配置下迁移失败或被忽略导致索引退化为非过滤唯一索引。
/// </summary>
public sealed class UserConfigurationTests
{
    [Fact]
    public void UserConfiguration_Email_Filter_Should_Use_SqlServer_Syntax()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);
        var emailIndex = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_users_email");
        Assert.NotNull(emailIndex);

        var filter = emailIndex.GetFilter();

        // Assert：应为 SQL Server 风格 [email] IS NOT NULL，不应包含 PostgreSQL 风格的双引号
        Assert.NotNull(filter);
        Assert.Contains("[email]", filter);
        Assert.DoesNotContain("\"email\"", filter);
    }

    [Fact]
    public void UserConfiguration_Phone_Filter_Should_Use_SqlServer_Syntax()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);
        var phoneIndex = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_users_phone_number");
        Assert.NotNull(phoneIndex);

        var filter = phoneIndex.GetFilter();

        // Assert：应为 SQL Server 风格 [phone_number] IS NOT NULL，不应包含 PostgreSQL 风格的双引号
        Assert.NotNull(filter);
        Assert.Contains("[phone_number]", filter);
        Assert.DoesNotContain("\"phone_number\"", filter);
    }
}

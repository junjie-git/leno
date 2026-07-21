using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Tests.Configurations;

/// <summary>
/// AddressConfiguration 默认地址索引验证测试。
/// 验证 (UserId, IsDefault) 复合索引带唯一约束与 is_default = 1 过滤条件，
/// 防止并发场景下出现多条默认地址破坏 User.DefaultAddressId 单一性语义。
/// </summary>
public sealed class AddressConfigurationTests
{
    [Fact]
    public void AddressConfiguration_Default_Index_Should_Be_Unique_With_Filter()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(Address));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_addresses_user_default");
        Assert.NotNull(index);

        // Assert：索引必须唯一，且过滤条件限定 is_default = 1
        Assert.True(index.IsUnique);

        var filter = index.GetFilter();
        Assert.NotNull(filter);
        Assert.Contains("is_default", filter);
    }
}

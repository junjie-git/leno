using Leno.Order.Domain.Aggregates;
using Leno.Order.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.Order.Infrastructure.Tests;

public class OrderConfigurationTests
{
    [Fact]
    public void OrderConfiguration_Should_Have_RowVersion_Concurrency_Token()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: "order_concurrency_test_" + Guid.NewGuid())
            .Options;
        using var context = new OrderDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(Order));

        // Assert：RowVersion 属性应被配置为并发令牌
        var rowVersionProperty = entityType!.GetProperties()
            .FirstOrDefault(p => p.Name == nameof(Order.RowVersion));
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty!.IsConcurrencyToken.Should().BeTrue();
        rowVersionProperty.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }
}

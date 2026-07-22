using System.Reflection;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Configurations;

/// <summary>
/// P1-13 测试：验证 PaymentOrder / RefundOrder 聚合根新增 RowVersion byte[] 属性，
/// 且 EF Core 配置通过 <see cref="PropertyBuilder.IsRowVersion"/> 标记为乐观并发令牌。
/// 根因：原配置缺失 IsRowVersion()，并发场景下异步通知与补偿任务同时更新同一支付单
/// 可能发生覆盖（后写覆盖先写）。修复后：并发更新抛出 DbUpdateConcurrencyException。
/// </summary>
/// <remarks>
/// 由于测试环境未引入 EF Core Sqlite/InMemory provider，无法直接验证并发冲突抛出异常。
/// 本测试通过反射验证：(1) 聚合根含 RowVersion byte[] 属性；(2) 配置类中存在
/// 对 RowVersion 的 IsRowVersion() 调用。完整并发测试需在集成测试环境使用 Sqlite in-memory。
/// </remarks>
public class RowVersionConfigurationTests
{
    [Fact]
    public void PaymentOrder_ShouldHaveRowVersionByteArrayProperty()
    {
        // 安排 / 行动
        var rowVersionProperty = typeof(PaymentOrder).GetProperty(
            "RowVersion", BindingFlags.Public | BindingFlags.Instance);

        // 断言：存在 RowVersion 属性，类型为 byte[]
        Assert.NotNull(rowVersionProperty);
        Assert.Equal(typeof(byte[]), rowVersionProperty!.PropertyType);
    }

    [Fact]
    public void RefundOrder_ShouldHaveRowVersionByteArrayProperty()
    {
        // 安排 / 行动
        var rowVersionProperty = typeof(RefundOrder).GetProperty(
            "RowVersion", BindingFlags.Public | BindingFlags.Instance);

        // 断言：存在 RowVersion 属性，类型为 byte[]
        Assert.NotNull(rowVersionProperty);
        Assert.Equal(typeof(byte[]), rowVersionProperty!.PropertyType);
    }

    [Fact]
    public void PaymentOrderConfiguration_Configure_ShouldCallIsRowVersionOnRowVersionProperty()
    {
        // 安排：使用 ModelBuilder 创建EntityTypeBuilder<PaymentOrder>，
        // 调用 Configuration.Configure 后检查 RowVersion 属性的 IsRowVersion 标记
        var modelBuilder = new ModelBuilder();
        var entityTypeBuilder = modelBuilder.Entity<PaymentOrder>();

        // 行动
        var configuration = new PaymentOrderConfiguration();
        configuration.Configure(entityTypeBuilder);

        // 断言：RowVersion 属性被标记为并发令牌（IsRowVersion=true）
        var entityType = modelBuilder.Model.FindEntityType(typeof(PaymentOrder));
        Assert.NotNull(entityType);
        var rowVersionProperty = entityType!.FindProperty("RowVersion");
        Assert.NotNull(rowVersionProperty);
        Assert.True(rowVersionProperty!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersionProperty.ValueGenerated);
    }

    [Fact]
    public void RefundOrderConfiguration_Configure_ShouldCallIsRowVersionOnRowVersionProperty()
    {
        // 安排
        var modelBuilder = new ModelBuilder();
        var entityTypeBuilder = modelBuilder.Entity<RefundOrder>();

        // 行动
        var configuration = new RefundOrderConfiguration();
        configuration.Configure(entityTypeBuilder);

        // 断言：RowVersion 属性被标记为并发令牌（IsRowVersion=true）
        var entityType = modelBuilder.Model.FindEntityType(typeof(RefundOrder));
        Assert.NotNull(entityType);
        var rowVersionProperty = entityType!.FindProperty("RowVersion");
        Assert.NotNull(rowVersionProperty);
        Assert.True(rowVersionProperty!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersionProperty.ValueGenerated);
    }
}

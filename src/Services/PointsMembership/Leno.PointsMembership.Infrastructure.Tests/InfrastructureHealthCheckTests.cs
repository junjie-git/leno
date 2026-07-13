using Leno.PointsMembership.Infrastructure.Consumers;

namespace Leno.PointsMembership.Infrastructure.Tests;

/// <summary>
/// 健康检查测试：验证 PointsMembership.Infrastructure 程序集可加载、关键 Consumer 类型可解析。
/// 注意：PointsMembership.Infrastructure 的 6 个 Consumer（UserRegistered、ReviewApproved、RefundCompleted、
/// CouponExchange、OrderPaid、OrderAfterSalesWindowClosed）的单元测试已在
/// Leno.PointsMembership.Domain.Tests 项目中覆盖（NewFeatureTests3-6、DomainTests.cs）。
/// 此处仅保留项目健康检查测试，避免重复。
/// </summary>
public class InfrastructureHealthCheckTests
{
    [Fact]
    public void Infrastructure_Assembly_ShouldLoadSuccessfully()
    {
        // Act
        var assembly = typeof(UserRegisteredEventConsumer).Assembly;

        // Assert
        assembly.GetName().Name.Should().Be("Leno.PointsMembership.Infrastructure");
    }

    [Theory]
    [InlineData(typeof(UserRegisteredEventConsumer))]
    [InlineData(typeof(ReviewApprovedEventConsumer))]
    [InlineData(typeof(RefundCompletedEventConsumer))]
    public void KeyConsumers_ShouldBeResolvable(Type consumerType)
    {
        // Assert - Consumer 类型可解析，证明 Infrastructure 程序集引用正常
        consumerType.Should().NotBeNull();
        consumerType.IsClass.Should().BeTrue();
        consumerType.IsSealed.Should().BeTrue("Consumer 应为 sealed 类");
    }
}

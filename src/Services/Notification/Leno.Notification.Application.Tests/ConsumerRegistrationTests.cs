using System.Reflection;
using System.Collections;
using Leno.Notification.Infrastructure.Consumers;
using Leno.Notification.Infrastructure.Dependencies;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Notification.Application.Tests;

/// <summary>
/// 验证 MassTransit 消费者注册：NotificationEventConsumer 不应被注册，
/// 否则与各专用 Consumer（Order/User/Payment 等）形成重复订阅，每条事件被消费两次。
/// </summary>
public class ConsumerRegistrationTests
{
    [Fact]
    public void AddNotificationConsumers_ShouldNotRegisterNotificationEventConsumer()
    {
        // Arrange — 使用真实 MassTransit 注册入口
        var services = new ServiceCollection();
        IBusRegistrationConfigurator configurator = services.AddMassTransit();

        // Act — 调用 AddNotificationConsumers 扩展方法
        configurator.AddNotificationConsumers();

        // Assert — 通过反射获取 MassTransit 内部 _registrations 字典，
        // 验证 NotificationEventConsumer 未被注册，专用 Consumer 仍然注册。
        var registrations = ExtractRegistrations(configurator);
        Assert.NotNull(registrations);

        var registeredNames = registrations.Keys.OfType<string>().ToList();

        // 修复前：NotificationEventConsumer 被注册，与专用 Consumer 重复订阅同一事件
        // 修复后：NotificationEventConsumer 不在注册列表中
        Assert.DoesNotContain(registeredNames, name => name.Contains("NotificationEventConsumer", StringComparison.Ordinal));

        // 各专用 Consumer 仍然注册
        Assert.Contains(registeredNames, name => name.Contains("OrderEventConsumer", StringComparison.Ordinal));
        Assert.Contains(registeredNames, name => name.Contains("UserEventConsumer", StringComparison.Ordinal));
        Assert.Contains(registeredNames, name => name.Contains("PaymentEventConsumer", StringComparison.Ordinal));
        Assert.Contains(registeredNames, name => name.Contains("PromotionEventConsumer", StringComparison.Ordinal));
        Assert.Contains(registeredNames, name => name.Contains("PointsEventConsumer", StringComparison.Ordinal));
        Assert.Contains(registeredNames, name => name.Contains("AfterSalesEventConsumer", StringComparison.Ordinal));
    }

    /// <summary>
    /// 通过反射获取 MassTransit RegistrationConfigurator 内部的 _registrations 字典。
    /// 该字典存储所有通过 AddConsumer&lt;T&gt; 注册的消费者类型。
    /// </summary>
    private static IDictionary? ExtractRegistrations(IBusRegistrationConfigurator configurator)
    {
        var type = configurator.GetType();
        while (type is not null)
        {
            var field = type.GetField("_registrations", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field is not null)
            {
                return field.GetValue(configurator) as IDictionary;
            }
            type = type.BaseType;
        }
        return null;
    }
}

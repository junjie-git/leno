using Leno.Notification.Infrastructure.Consumers;

namespace Leno.Notification.Application.Tests;

public class EventTemplateMappingTests
{
    [Fact]
    public void Mappings_ShouldHaveAll12Entries()
    {
        EventTemplateMapping.Mappings.Count.Should().Be(12);
    }

    [Theory]
    [InlineData("UserRegisteredEvent", "user_registered_welcome")]
    [InlineData("OrderCreatedEvent", "order_created")]
    [InlineData("OrderShippedEvent", "order_shipped")]
    [InlineData("OrderCompletedEvent", "order_completed")]
    [InlineData("PaymentSucceededEvent", "payment_succeeded")]
    [InlineData("PaymentFailedEvent", "payment_failed")]
    [InlineData("AfterSalesApprovedEvent", "after_sales_approved")]
    [InlineData("RefundCompletedEvent", "refund_completed")]
    [InlineData("SeckillOrderCreatedEvent", "seckill_order_created")]
    [InlineData("PointsEarnedEvent", "points_earned")]
    [InlineData("MemberLevelUpgradedEvent", "member_level_upgraded")]
    [InlineData("MembershipActivatedEvent", "membership_activated")]
    public void GetTemplateCode_ShouldReturnCorrectCode(string eventType, string expectedTemplateCode)
    {
        var result = EventTemplateMapping.GetTemplateCode(eventType);

        result.Should().Be(expectedTemplateCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UnknownEvent")]
    [InlineData("NonExistentEvent")]
    public void GetTemplateCode_UnknownEventType_ShouldReturnNull(string eventType)
    {
        var result = EventTemplateMapping.GetTemplateCode(eventType);

        result.Should().BeNull();
    }

    [Fact]
    public void GetTemplateCode_NullEventType_ShouldReturnNull()
    {
        var result = EventTemplateMapping.GetTemplateCode(null!);

        result.Should().BeNull();
    }
}
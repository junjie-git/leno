using Leno.Notification.Infrastructure.Consumers;

namespace Leno.Notification.Application.Tests;

public class EventTemplateMappingTests
{
    [Fact]
    public void Mappings_ShouldHaveAll13Entries()
    {
        EventTemplateMapping.Mappings.Count.Should().Be(13);
    }

    [Theory]
    [InlineData("UserRegisteredEvent", "user_registered_welcome")]
    [InlineData("OrderCreatedEvent", "order_created")]
    [InlineData("OrderShippedEvent", "order_shipped")]
    [InlineData("OrderCompletedEvent", "order_completed")]
    [InlineData("OrderCancelledEvent", "order_cancelled")]
    [InlineData("PaymentSucceededEvent", "payment_succeeded")]
    [InlineData("PaymentFailedEvent", "payment_failed")]
    [InlineData("AfterSalesApprovedEvent", "after_sales_approved")]
    [InlineData("RefundCompletedEvent", "refund_completed")]
    [InlineData("SeckillOrderCreatedIntegrationEvent", "seckill_order_created")]
    [InlineData("PointsEarnedIntegrationEvent", "points_earned")]
    [InlineData("MemberLevelChangedIntegrationEvent", "member_level_upgraded")]
    [InlineData("PaidMemberSubscribedIntegrationEvent", "membership_activated")]
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
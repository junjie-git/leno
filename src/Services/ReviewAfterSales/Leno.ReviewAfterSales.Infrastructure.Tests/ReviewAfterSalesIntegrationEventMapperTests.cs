using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Infrastructure.EventBus;
using Leno.SharedContracts.Events;

namespace Leno.ReviewAfterSales.Infrastructure.Tests;

/// <summary>
/// P0-2.11 解除 RefundCompleted 事件回环 - 集成事件映射器单元测试。
/// 验证：
/// - AfterSalesRefundCompletedDomainEvent 映射为独立的 AfterSalesRefundCompletedEvent（而非 RefundCompletedEvent）
/// - 映射后字段（含 ChannelRefundNo）完整透传
/// - 不再产生 RefundCompletedEvent，避免售后域消费自己发布的事件造成回环
/// </summary>
public sealed class ReviewAfterSalesIntegrationEventMapperTests
{
    [Fact]
    public void Mapper_Should_Not_Map_AfterSalesRefundCompletedDomainEvent_To_RefundCompletedEvent()
    {
        var mapper = new ReviewAfterSalesIntegrationEventMapper();
        var domainEvent = BuildDomainEvent(channelRefundNo: "WX-REFUND-001");

        var integrationEvent = mapper.Map(domainEvent);

        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().NotBeOfType<RefundCompletedEvent>();
    }

    [Fact]
    public void Mapper_Should_Map_AfterSalesRefundCompletedDomainEvent_To_AfterSalesRefundCompletedEvent()
    {
        var mapper = new ReviewAfterSalesIntegrationEventMapper();
        var domainEvent = BuildDomainEvent(channelRefundNo: "WX-REFUND-001");

        var integrationEvent = mapper.Map(domainEvent);

        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().BeOfType<AfterSalesRefundCompletedEvent>();
    }

    [Fact]
    public void Mapper_Should_Preserve_Payload_Including_ChannelRefundNo()
    {
        var afterSalesId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        const decimal refundAmount = 88.5m;
        const string currency = "CNY";
        var completedAt = DateTime.UtcNow;
        const string channelRefundNo = "ALIPAY-REFUND-2026";

        var mapper = new ReviewAfterSalesIntegrationEventMapper();
        var domainEvent = new AfterSalesRefundCompletedDomainEvent(
            orderId, userId, refundId, afterSalesId,
            refundAmount, currency, completedAt, channelRefundNo);

        var integrationEvent = mapper.Map(domainEvent) as AfterSalesRefundCompletedEvent;

        integrationEvent.Should().NotBeNull();
        integrationEvent!.AfterSalesId.Should().Be(afterSalesId);
        integrationEvent.OrderId.Should().Be(orderId);
        integrationEvent.UserId.Should().Be(userId);
        integrationEvent.RefundId.Should().Be(refundId);
        integrationEvent.RefundAmount.Should().Be(refundAmount);
        integrationEvent.Currency.Should().Be(currency);
        integrationEvent.CompletedAt.Should().Be(completedAt);
        integrationEvent.ChannelRefundNo.Should().Be(channelRefundNo);
        integrationEvent.AggregateId.Should().Be(afterSalesId);
    }

    [Fact]
    public void Mapper_Should_Handle_Null_ChannelRefundNo_As_Empty_String()
    {
        var mapper = new ReviewAfterSalesIntegrationEventMapper();
        var domainEvent = BuildDomainEvent(channelRefundNo: null);

        var integrationEvent = mapper.Map(domainEvent) as AfterSalesRefundCompletedEvent;

        integrationEvent.Should().NotBeNull();
        integrationEvent!.ChannelRefundNo.Should().BeEmpty();
    }

    private static AfterSalesRefundCompletedDomainEvent BuildDomainEvent(string? channelRefundNo)
    {
        return new AfterSalesRefundCompletedDomainEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            afterSalesId: Guid.NewGuid(),
            refundAmount: 10m,
            currency: "CNY",
            completedAt: DateTime.UtcNow,
            channelRefundNo: channelRefundNo);
    }
}

using Leno.Order.Domain.Events;
using Leno.Order.Infrastructure.EventBus;
using Leno.SharedContracts.Events;

namespace Leno.Order.Infrastructure.Tests;

public class OrderIntegrationEventMapperTests
{
    [Fact]
    public void Mapper_Should_Register_StockReservedEvent_Translation()
    {
        var mapper = new OrderIntegrationEventMapper();
        var stockReservedEvent = new StockReservedEvent(Guid.NewGuid(), Guid.NewGuid(), 10);

        var integrationEvent = mapper.Map(stockReservedEvent);

        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().BeOfType<StockReservedIntegrationEvent>();
    }

    [Fact]
    public void Mapper_Should_Register_StockConfirmedEvent_Translation()
    {
        var mapper = new OrderIntegrationEventMapper();
        var stockConfirmedEvent = new StockConfirmedEvent(Guid.NewGuid(), Guid.NewGuid(), 10);

        var integrationEvent = mapper.Map(stockConfirmedEvent);

        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().BeOfType<StockConfirmedIntegrationEvent>();
    }

    [Fact]
    public void Mapper_Should_Register_StockReleasedEvent_Translation()
    {
        var mapper = new OrderIntegrationEventMapper();
        var stockReleasedEvent = new StockReleasedEvent(Guid.NewGuid(), Guid.NewGuid(), 10);

        var integrationEvent = mapper.Map(stockReleasedEvent);

        integrationEvent.Should().NotBeNull();
        integrationEvent.Should().BeOfType<StockReleasedIntegrationEvent>();
    }

    [Fact]
    public void Mapper_Should_Preserve_StockReservedEvent_Payload()
    {
        var skuId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quantity = 25;
        var mapper = new OrderIntegrationEventMapper();
        var stockReservedEvent = new StockReservedEvent(skuId, orderId, quantity);

        var integrationEvent = mapper.Map(stockReservedEvent) as StockReservedIntegrationEvent;

        integrationEvent.Should().NotBeNull();
        integrationEvent!.SkuId.Should().Be(skuId);
        integrationEvent.OrderId.Should().Be(orderId);
        integrationEvent.Quantity.Should().Be(quantity);
        integrationEvent.AggregateId.Should().Be(skuId);
    }
}

using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.Tests.EventBus;

public class IntegrationEventMapperBaseTests
{
    [Fact]
    public void Map_RegisteredHandler_ShouldReturnTranslatedEvent()
    {
        var mapper = new TestMapper();
        var domainEvent = new TestDomainEvent(Guid.NewGuid());

        var result = mapper.Map(domainEvent);

        result.Should().NotBeNull();
        result.Should().BeOfType<TestIntegrationEvent>();
    }

    [Fact]
    public void Map_UnregisteredDomainEvent_ShouldReturnNull()
    {
        var mapper = new TestMapper();
        var unknownEvent = new UnknownDomainEvent(Guid.NewGuid());

        var result = mapper.Map(unknownEvent);

        result.Should().BeNull();
    }

    private class TestMapper : IntegrationEventMapperBase
    {
        public TestMapper()
        {
            RegisterHandler<TestDomainEvent, TestIntegrationEvent>(e => new TestIntegrationEvent(e.AggregateId));
        }
    }

    private class TestDomainEvent : DomainEventBase
    {
        public TestDomainEvent(Guid aggregateId) : base(aggregateId) { }
    }

    private class TestIntegrationEvent : IntegrationEventBase
    {
        public Guid AggregateId { get; }
        public TestIntegrationEvent(Guid aggregateId) { AggregateId = aggregateId; }
    }

    private class UnknownDomainEvent : DomainEventBase
    {
        public UnknownDomainEvent(Guid aggregateId) : base(aggregateId) { }
    }
}

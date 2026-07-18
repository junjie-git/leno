using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.ReadModel;

public class ReadModelSyncConsumerBaseDeleteTests
{
    public sealed class FakeDeleteEvent : IntegrationEventBase
    {
        public string ResourceId { get; init; } = string.Empty;
    }

    public sealed class FakeDeleteReadModel
    {
        public string Id { get; set; } = string.Empty;
    }

    public sealed class FakeDeleteConsumer : ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>
    {
        public bool DeleteActionInvoked { get; private set; }

        protected override Task<(string Id, string IndexName, FakeDeleteReadModel? ReadModel)> BuildReadModelAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
        {
            // 删除场景下不调用索引分支
            return Task.FromResult<(string, string, FakeDeleteReadModel?)>(("", "", null));
        }

        protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
        {
            DeleteActionInvoked = true;
            return Task.FromResult<(string Id, string IndexName)?>(
                (integrationEvent.ResourceId, "leno_fake"));
        }

        public FakeDeleteConsumer(IEsReadModelRepository<FakeDeleteReadModel> repository)
            : base(repository, NullLogger<ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>>.Instance)
        {
        }
    }

    [Fact]
    public async Task Consume_WhenBuildDeleteActionReturnsValue_ShouldCallDeleteByIdAsync()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        var consumer = new FakeDeleteConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-001" });

        // Act
        await consumer.Consume(context.Object);

        // Assert
        consumer.DeleteActionInvoked.Should().BeTrue();
        repoMock.Verify(r => r.DeleteByIdAsync("res-001", "leno_fake", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenDeleteThrows_ShouldPropagateException()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ES unavailable"));
        var consumer = new FakeDeleteConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-002" });

        // Act
        var act = async () => await consumer.Consume(context.Object);

        // Assert: 必须抛异常触发 MassTransit 重试与死信队列
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Consume_WhenBuildDeleteActionReturnsNull_ShouldSkipSilently()
    {
        // Arrange: 既不索引也不删除（事件不感兴趣）
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        var consumer = new SkipAllConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-003" });

        // Act
        await consumer.Consume(context.Object);

        // Assert
        repoMock.Verify(r => r.IndexAsync(It.IsAny<FakeDeleteReadModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public sealed class SkipAllConsumer : ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>
    {
        public SkipAllConsumer(IEsReadModelRepository<FakeDeleteReadModel> repository)
            : base(repository, NullLogger<ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>>.Instance)
        {
        }

        protected override Task<(string Id, string IndexName, FakeDeleteReadModel? ReadModel)> BuildReadModelAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
            => Task.FromResult<(string, string, FakeDeleteReadModel?)>(("", "", null));

        protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
            => Task.FromResult<(string, string)?>(null);
    }
}

using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests;

/// <summary>
/// P0-2.3 补充测试：覆盖 SeckillOrderCreationFailedEventConsumer 在预占记录已回退时的幂等跳过行为。
/// </summary>
public class SeckillOrderCreationFailedEventConsumerAdditionalTests
{
    private readonly Mock<ISeckillActivityRepository> _activityRepoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<ISeckillPreOccupationRecordRepository> _preOccupationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<SeckillOrderCreationFailedEventConsumer>> _loggerMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();

    public SeckillOrderCreationFailedEventConsumerAdditionalTests()
    {
        _idempotencyStoreMock.Setup(s => s.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _idempotencyStoreMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Consume_AlreadyRolledBack_ShouldSkipRestore()
    {
        // 业务场景：补偿服务已先行回退预占记录（IsRolledBack=true），
        // 失败事件消费者不应再次回退 Redis/DB 库存，否则双重复回退导致库存膨胀
        var activityId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var record = SeckillPreOccupationRecord.Create(activityId, skuId, Guid.NewGuid(), orderId, 5);
        record.MarkRolledBack(); // 模拟补偿服务已先行回退

        _preOccupationRepoMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var consumer = new SeckillOrderCreationFailedEventConsumer(
            _activityRepoMock.Object, _stockServiceMock.Object, _preOccupationRepoMock.Object,
            _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

        var evt = new SeckillOrderCreationFailedIntegrationEvent(activityId, skuId, Guid.NewGuid(), orderId, 5, "fail");
        await consumer.Consume(CreateConsumeContext(evt));

        _stockServiceMock.Verify(
            s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _activityRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}

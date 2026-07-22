using System.Reflection;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// 商品事件消费者冒烟测试：事件未携带 SkuIds（默认空集合）时，消费者应不调用仓储与工作单元。
/// 覆盖 P0-3 改造后三个消费者的空索引路径，验证不产生副作用。
/// </summary>
public class ProductEventConsumerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ProductTakenDown_ShouldCompleteWithoutError()
    {
        // Arrange：事件未携带 SkuIds，反向索引无命中
        var (consumer, mockUnitOfWork) = CreateTakenDownConsumer();

        var integrationEvent = new ProductTakenDownEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert：SkuIds 为空，不调用仓储与 UnitOfWork
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProductPublished_ShouldCompleteWithoutError()
    {
        // Arrange：事件未携带 SkuIds，反向索引无命中
        var (consumer, mockUnitOfWork) = CreatePublishedConsumer();

        var integrationEvent = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert：SkuIds 为空，不调用仓储与 UnitOfWork
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProductUpdated_ShouldCompleteWithoutError()
    {
        // Arrange：事件未携带 SkuIds，反向索引无命中
        var (consumer, mockUnitOfWork) = CreateUpdatedConsumer();

        var integrationEvent = new ProductUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SellerId = SellerId,
            Title = "更新后标题",
            MainImageUrl = "https://cdn.example.com/img.png"
        };

        // Act
        await InvokeHandleAsync(consumer, integrationEvent);

        // Assert：SkuIds 为空，不调用仓储与 UnitOfWork
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ProductTakenDownEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreateTakenDownConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockIndexService = new Mock<ICartSkuIndexService>();
        var mockLogger = new Mock<ILogger<ProductTakenDownEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductTakenDownEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockIndexService.Object,
            CreateDbContext(), mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    private static (ProductPublishedEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreatePublishedConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockIndexService = new Mock<ICartSkuIndexService>();
        var mockLogger = new Mock<ILogger<ProductPublishedEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductPublishedEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockIndexService.Object,
            CreateDbContext(), mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    private static (ProductUpdatedEventConsumer consumer, Mock<IUnitOfWork> unitOfWork) CreateUpdatedConsumer()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockIndexService = new Mock<ICartSkuIndexService>();
        var mockSnapshotAc = new Mock<IProductSnapshotAntiCorruption>();
        var mockLogger = new Mock<ILogger<ProductUpdatedEventConsumer>>();
        var mockIdempotencyStore = new Mock<IIdempotencyStore>();

        var consumer = new ProductUpdatedEventConsumer(
            mockCartRepo.Object, mockUnitOfWork.Object, mockIndexService.Object,
            mockSnapshotAc.Object, CreateDbContext(), mockLogger.Object, mockIdempotencyStore.Object);
        return (consumer, mockUnitOfWork);
    }

    /// <summary>
    /// 创建用于单元测试的 CartDbContext 实例（不连接真实数据库）。
    /// 消费者构造函数要求 CartDbContext 用于 ChangeTracker.Clear()，但本组测试使用空 SkuIds 提前返回，
    /// 不会执行数据库查询或 SaveChanges，因此使用 SQL Server 选项构造即可（惰性连接）。
    /// </summary>
    private static CartDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseSqlServer("Server=localhost;Database=CartUnitTest;User Id=sa;Password=Dummy;TrustServerCertificate=True")
            .Options;
        return new CartDbContext(options);
    }

    private static async Task InvokeHandleAsync<TConsumer, TEvent>(TConsumer consumer, TEvent integrationEvent)
        where TConsumer : class
        where TEvent : class
    {
        var handleMethod = typeof(TConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }
}

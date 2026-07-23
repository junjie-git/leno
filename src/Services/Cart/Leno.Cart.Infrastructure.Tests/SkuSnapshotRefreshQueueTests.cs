using System.Runtime.CompilerServices;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Abstractions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// <see cref="SkuSnapshotRefreshQueue"/> 后台 SKU 快照刷新队列单元测试（阶段三 3.11）。
/// 覆盖：
/// - EnqueueRefresh / EnqueueRefreshBatch 空入参与去重
/// - 后台消费：批量拉取快照并更新对应购物车
/// - ACL 调用失败时跳过本批，不向上抛
/// - 反向索引无命中时跳过写入
/// - 批量与并发处理
/// - CancellationToken 取消时退出
/// </summary>
public class SkuSnapshotRefreshQueueTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId1 = Guid.NewGuid();
    private static readonly Guid SkuId2 = Guid.NewGuid();
    private static readonly Guid CartId1 = Guid.NewGuid();
    private static readonly Guid CartId2 = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public void EnqueueRefresh_EmptyGuid_ShouldBeIgnored()
    {
        // Arrange
        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        var sut = CreateSut(CreateServiceProvider(mockAntiCorruption.Object), out _);

        // Act
        sut.EnqueueRefresh(Guid.Empty);

        // Assert：不会调用 ACL（即使启动后台任务也无 SKU 需要刷新）
        mockAntiCorruption.Verify(
            a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void EnqueueRefreshBatch_NullInput_ShouldThrow()
    {
        var sut = CreateSut(CreateServiceProvider(new Mock<IProductSnapshotAntiCorruption>().Object), out _);

        var act = () => sut.EnqueueRefreshBatch(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skuIds");
    }

    [Fact]
    public async Task StartAsync_EnqueueRefresh_ShouldFetchSnapshotAndUpdateCarts()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        // 种子：Cart1 与 Cart2 都持有 SkuId1
        var cart1 = CartAggregate.Create(CartId1, UserId);
        cart1.AddItem(SkuId1, 2, SellerId);
        var cart2 = CartAggregate.Create(CartId2, UserId);
        cart2.AddItem(SkuId1, 1, SellerId);
        context.Carts.AddRange(cart1, cart2);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var snapshotDto = new SkuSnapshotDto
        {
            SkuId = SkuId1,
            Title = "新商品1",
            MainImageUrl = "https://cdn.example.com/new.png",
            UnitPrice = 99m,
            IsOnSale = true
        };
        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        var fetchCompletion = new TaskCompletionSource<bool>();
        mockAntiCorruption
            .Setup(a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuSnapshotDto> { snapshotDto })
            .Callback(() => fetchCompletion.TrySetResult(true));

        var mockIndexService = new Mock<ICartSkuIndexService>();
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1, CartId2 });

        var services = CreateServiceProvider(
            mockAntiCorruption.Object,
            mockIndexService.Object,
            context);
        var sut = CreateSut(services, out _);

        // Act：启动后台服务并入队
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefresh(SkuId1);

        // Assert：等待 ACL 调用完成
        await WaitAsync(fetchCompletion.Task, TimeSpan.FromSeconds(3));
        await StopGracefullyAsync(sut);

        mockAntiCorruption.Verify(
            a => a.GetSkuSnapshotsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(SkuId1)),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        mockIndexService.Verify(
            s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // 验证购物车快照已更新（重新查询确认）
        context.ChangeTracker.Clear();
        var cart1Reloaded = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == CartId1);
        var cart2Reloaded = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == CartId2);
        cart1Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot.Should().NotBeNull();
        cart1Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot!.Price.Should().Be(99m);
        cart1Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot!.SkuName.Should().Be("新商品1");
        cart2Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot.Should().NotBeNull();
        cart2Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot!.Price.Should().Be(99m);
    }

    [Fact]
    public async Task StartAsync_BatchEnqueue_ShouldDeduplicateSameSkuId()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var cart = CartAggregate.Create(CartId1, UserId);
        cart.AddItem(SkuId1, 1, SellerId);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var snapshotDto = new SkuSnapshotDto { SkuId = SkuId1, Title = "T", UnitPrice = 5m, IsOnSale = true };
        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        var fetchCalls = 0;
        var fetchCompletion = new TaskCompletionSource<bool>();
        mockAntiCorruption
            .Setup(a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuSnapshotDto> { snapshotDto })
            .Callback(() =>
            {
                Interlocked.Increment(ref fetchCalls);
                fetchCompletion.TrySetResult(true);
            });

        var mockIndexService = new Mock<ICartSkuIndexService>();
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1 });

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, context);
        var sut = CreateSut(services, out _);

        // Act：入队 5 次相同 SkuId1，去重后批量内应只出现 1 次
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefreshBatch(new[] { SkuId1, SkuId1, SkuId1, SkuId1, SkuId1 });

        await WaitAsync(fetchCompletion.Task, TimeSpan.FromSeconds(3));
        await StopGracefullyAsync(sut);

        // Assert：同一批次去重，ACL 调用参数仅含一个 SkuId1
        mockAntiCorruption.Verify(
            a => a.GetSkuSnapshotsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count() == 1 && ids.Contains(SkuId1)),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_AclThrowsAntiCorruptionException_ShouldSkipBatchAndContinue()
    {
        // Arrange：ACL 抛 AntiCorruptionException，本批被跳过，下一次入队仍可处理
        await using var context = CreateInMemoryContext();
        var cart = CartAggregate.Create(CartId1, UserId);
        cart.AddItem(SkuId1, 1, SellerId);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var firstAttempt = new TaskCompletionSource<bool>();
        var secondSnapshot = new SkuSnapshotDto { SkuId = SkuId1, Title = "成功商品", UnitPrice = 7m, IsOnSale = true };
        var callCount = 0;
        var secondCompletion = new TaskCompletionSource<bool>();

        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        mockAntiCorruption
            .Setup(a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1)
                {
                    firstAttempt.TrySetResult(true);
                    throw new AntiCorruptionException("PRODUCT_UNAVAILABLE", "商品域不可用");
                }
                secondCompletion.TrySetResult(true);
                return Task.FromResult<IReadOnlyList<SkuSnapshotDto>>(new List<SkuSnapshotDto> { secondSnapshot });
            });

        var mockIndexService = new Mock<ICartSkuIndexService>();
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1 });

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, context);
        var sut = CreateSut(services, out _);

        // Act：第一次入队触发 ACL 失败
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefresh(SkuId1);

        await WaitAsync(firstAttempt.Task, TimeSpan.FromSeconds(3));
        // 等待一小段时间让失败处理完成
        await Task.Delay(200);

        // 第二次入队应能成功
        sut.EnqueueRefresh(SkuId1);
        await WaitAsync(secondCompletion.Task, TimeSpan.FromSeconds(3));
        await StopGracefullyAsync(sut);

        // Assert：第二次成功后快照写入
        context.ChangeTracker.Clear();
        var cartReloaded = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == CartId1);
        cartReloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot.Should().NotBeNull();
        cartReloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot!.Price.Should().Be(7m);
    }

    [Fact]
    public async Task StartAsync_NoCartHoldsSku_ShouldSkipUnitOfWorkSave()
    {
        // Arrange：反向索引无命中，不应调用 SaveEntitiesAsync
        await using var context = CreateInMemoryContext();

        var snapshotDto = new SkuSnapshotDto { SkuId = SkuId1, Title = "T", UnitPrice = 1m, IsOnSale = true };
        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        var fetchCompletion = new TaskCompletionSource<bool>();
        mockAntiCorruption
            .Setup(a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuSnapshotDto> { snapshotDto })
            .Callback(() => fetchCompletion.TrySetResult(true));

        var mockIndexService = new Mock<ICartSkuIndexService>();
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var services = CreateServiceProvider(
            mockAntiCorruption.Object,
            mockIndexService.Object,
            context,
            mockUnitOfWork.Object);
        var sut = CreateSut(services, out _);

        // Act
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefresh(SkuId1);
        await WaitAsync(fetchCompletion.Task, TimeSpan.FromSeconds(3));
        await StopGracefullyAsync(sut);

        // Assert：无购物车持有 SKU，不应保存
        mockUnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_MultipleSkusInOneBatch_ShouldUpdateAllCorrespondingCarts()
    {
        // Arrange：一次入队两个 SKU，分别命中不同购物车
        await using var context = CreateInMemoryContext();
        var cart1 = CartAggregate.Create(CartId1, UserId);
        cart1.AddItem(SkuId1, 1, SellerId);
        var cart2 = CartAggregate.Create(CartId2, UserId);
        cart2.AddItem(SkuId2, 1, SellerId);
        context.Carts.AddRange(cart1, cart2);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var snapshot1 = new SkuSnapshotDto { SkuId = SkuId1, Title = "T1", UnitPrice = 11m, IsOnSale = true };
        var snapshot2 = new SkuSnapshotDto { SkuId = SkuId2, Title = "T2", UnitPrice = 22m, IsOnSale = true };
        var mockAntiCorruption = new Mock<IProductSnapshotAntiCorruption>();
        var fetchCompletion = new TaskCompletionSource<bool>();
        var callCount = 0;
        mockAntiCorruption
            .Setup(a => a.GetSkuSnapshotsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuSnapshotDto> { snapshot1, snapshot2 })
            .Callback(() =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    fetchCompletion.TrySetResult(true);
                }
            });

        var mockIndexService = new Mock<ICartSkuIndexService>();
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1 });
        mockIndexService
            .Setup(s => s.GetCartIdsBySkuAsync(SkuId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId2 });

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, context);
        var sut = CreateSut(services, out _);

        // Act
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefreshBatch(new[] { SkuId1, SkuId2 });
        await WaitAsync(fetchCompletion.Task, TimeSpan.FromSeconds(3));
        // 等待批量处理完成
        await Task.Delay(300);
        await StopGracefullyAsync(sut);

        // Assert
        context.ChangeTracker.Clear();
        var cart1Reloaded = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == CartId1);
        var cart2Reloaded = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == CartId2);
        cart1Reloaded.Items.Single(i => i.SkuId == SkuId1).SkuSnapshot!.Price.Should().Be(11m);
        cart2Reloaded.Items.Single(i => i.SkuId == SkuId2).SkuSnapshot!.Price.Should().Be(22m);
    }

    [Fact]
    public async Task StopAsync_ShouldCompleteWithoutThrowing()
    {
        var services = CreateServiceProvider(new Mock<IProductSnapshotAntiCorruption>().Object);
        var sut = CreateSut(services, out _);

        await sut.StartAsync(CancellationToken.None);
        // 不入队任何 SKU，直接停止
        await StopGracefullyAsync(sut);

        // 无异常即视为通过
        Assert.True(true);
    }

    /// <summary>
    /// 构造 SUT 与对应的 ServiceProvider，方便在测试中启动后台服务。
    /// </summary>
    private static SkuSnapshotRefreshQueue CreateSut(IServiceProvider services, out IOptionsMonitor<CartSnapshotOptions> options)
    {
        var optionsValue = new CartSnapshotOptions
        {
            UseSkuSnapshot = true,
            SnapshotMaxAge = TimeSpan.FromMinutes(5),
            RefreshConcurrency = 1,
            RefreshQueueCapacity = 100,
            RefreshBatchSize = 50
        };
        var optionsMonitorMock = new Mock<IOptionsMonitor<CartSnapshotOptions>>();
        optionsMonitorMock.SetupGet(m => m.CurrentValue).Returns(optionsValue);
        options = optionsMonitorMock.Object;

        var logger = new Mock<ILogger<SkuSnapshotRefreshQueue>>();

        return new SkuSnapshotRefreshQueue(services, optionsMonitorMock.Object, logger.Object);
    }

    /// <summary>
    /// 构造测试用 ServiceProvider，注册 SKU 快照刷新队列所需的所有依赖。
    /// 允许调用方覆盖默认 Mock，方便针对不同场景定制行为。
    /// </summary>
    private static ServiceProvider CreateServiceProvider(
        IProductSnapshotAntiCorruption antiCorruption,
        ICartSkuIndexService? indexService = null,
        CartDbContext? dbContext = null,
        IUnitOfWork? unitOfWork = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(antiCorruption);
        services.AddSingleton(indexService ?? new Mock<ICartSkuIndexService>().Object);
        services.AddSingleton(Mock.Of<ICartRepository>());
        services.AddSingleton(unitOfWork ?? Mock.Of<IUnitOfWork>());

        if (dbContext is null)
        {
            var options = new DbContextOptionsBuilder<CartDbContext>()
                .UseInMemoryDatabase($"cart-queue-{Guid.NewGuid()}")
                .Options;
            dbContext = new CartDbContext(options);
            dbContext.Database.EnsureCreated();
        }
        // 注意：DbContext 不使用 AddDbContext，直接注册实例（保证测试用 ServiceProvider.CreateScope 解析到同一实例）
        services.AddSingleton(dbContext);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建 InMemory CartDbContext，用于后台刷新队列测试。
    /// </summary>
    private static CartDbContext CreateInMemoryContext()
    {
        var dbName = $"cart-queue-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new CartDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// 等待指定 Task 完成，超时则抛 TimeoutException。
    /// </summary>
    private static async Task WaitAsync(Task task, TimeSpan timeout, [CallerMemberName] string memberName = "")
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            throw new TimeoutException($"等待 {memberName} 超时，未在 {timeout} 内完成");
        }
    }

    /// <summary>
    /// 优雅停止 BackgroundService：使用带超时的 CancellationTokenSource，
    /// 防止消费者未消费任何消息时阻塞在 WaitToReadAsync 导致测试卡死。
    /// BackgroundService.StopAsync 会取消内部 stoppingCts 通知消费者退出。
    /// </summary>
    private static async Task StopGracefullyAsync(BackgroundService service)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(cts.Token);
        if (service is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

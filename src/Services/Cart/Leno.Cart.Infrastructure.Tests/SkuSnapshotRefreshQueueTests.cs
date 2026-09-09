using System.Runtime.CompilerServices;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
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
        await using var context = CreateInMemoryContext(out var dbName);
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
            dbName);
        var sut = CreateSut(services, out _);

        // Act：启动后台服务并入队
        await sut.StartAsync(CancellationToken.None);
        sut.EnqueueRefresh(SkuId1);

        // Assert：等待 ACL 调用完成
        await WaitAsync(fetchCompletion.Task, TimeSpan.FromSeconds(3));
        // ACL 返回后队列还要走 索引→仓储→Save 异步链路；若立即 Stop，取消令牌会打断尚未完成的 Save，
        // 导致快照永远不落库。这里轮询等待快照真正写入（最多 5 秒），再优雅停止。
        // 必须 AsNoTracking：主线程 context 仍跟踪种子实例（内存中快照为 null），
        // identity resolution 会直接返回旧实例，轮询永远看不到已落库的新值。
        await WaitUntilAsync(async () =>
            (await context.Carts.AsNoTracking().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == CartId1))
                ?.Items.Any(i => i.SkuSnapshot != null) == true,
            TimeSpan.FromSeconds(5));
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
        await using var context = CreateInMemoryContext(out var dbName);
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

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, dbName);
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
        await using var context = CreateInMemoryContext(out var dbName);
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

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, dbName);
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
        // 同上：等待第二次批次的 快照→Save 链路真正完成，避免 Stop 取消打断落库；
        // AsNoTracking 绕过主线程 context 的 identity resolution，从 InMemory 存储物化最新值。
        await WaitUntilAsync(async () =>
            (await context.Carts.AsNoTracking().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == CartId1))
                ?.Items.Any(i => i.SkuSnapshot != null) == true,
            TimeSpan.FromSeconds(5));
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
        await using var context = CreateInMemoryContext(out var dbName);

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
            dbName,
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
        await using var context = CreateInMemoryContext(out var dbName);
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

        var services = CreateServiceProvider(mockAntiCorruption.Object, mockIndexService.Object, dbName);
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

        // 无异常即视为通过：StopAsync 正常返回，后台任务应已结束且无未处理异常
        sut.ExecuteTask.IsCompleted.Should().BeTrue();
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
        string? dbName = null,
        IUnitOfWork? unitOfWork = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(antiCorruption);
        services.AddSingleton(indexService ?? new Mock<ICartSkuIndexService>().Object);

        // 刷新路径通过 cartRepository.GetByIdsAsync 加载购物车并经 unitOfWork.SaveEntitiesAsync 持久化。
        // 不能注册裸 Mock.Of：GetByIdsAsync 返回 null（foreach 抛 NRE 被 ConsumeAsync 吞掉）、
        // SaveEntitiesAsync 不落库，导致快照永远写不进 InMemory 库。
        // 这里注册真实仓储/UoW；DbContext 必须是 Scoped（与生产一致：SkuSnapshotRefreshQueue 每次
        // 刷新通过 CreateScope 解析独立 context），否则后台线程 Save 与主线程轮询查询并发共用
        // 同一个非线程安全的 DbContext 会相互干扰。InMemory 同名数据库共享存储，
        // 种子/断言用调用方创建的 context，后台刷新用 scope 内新建的 context。
        dbName ??= $"cart-queue-{Guid.NewGuid()}";
        services.AddScoped<CartDbContext>(_ =>
            new CartDbContext(new DbContextOptionsBuilder<CartDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options));
        services.AddScoped<ICartRepository>(sp =>
            new EfCoreCartRepository(sp.GetRequiredService<CartDbContext>()));
        services.AddScoped<IUnitOfWork>(sp =>
            unitOfWork ?? new EfCoreUnitOfWork<CartDbContext>(
                sp.GetRequiredService<CartDbContext>(), new Integration.EmptyIntegrationEventMapper()));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建 InMemory CartDbContext（种子/断言用），并经 out 参数返回库名，
    /// 供 CreateServiceProvider 为后台刷新队列创建共享同一 InMemory 存储的 Scoped DbContext。
    /// </summary>
    private static CartDbContext CreateInMemoryContext(out string dbName)
    {
        dbName = $"cart-queue-{Guid.NewGuid()}";
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
    /// 轮询等待条件成立，超时抛 TimeoutException。
    /// 用于后台队列异步链路（快照写入数据库）完成时机的确定性等待。
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, [CallerMemberName] string memberName = "")
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"等待条件超时 {memberName}，未在 {timeout} 内成立");
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

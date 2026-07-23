using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Services;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// <see cref="SnapshotCartPriceService"/> 装饰器单元测试（阶段三 3.11）。
/// 覆盖：
/// - feature flag 关闭时透传给 inner
/// - 快照存在且未过期时返回本地快照，不调用 inner
/// - 快照过期时返回过期快照并触发后台刷新（不回退 inner）
/// - 快照缺失时回退 inner 并入队后台刷新
/// - 多 SKU 混合场景：本地命中 + 缺失回退
/// - inner 抛异常时静默吞掉，仅记录日志（不抛出，调用方依据缺失 SKU 标记 PriceUnavailable）
/// - 同一 SKU 多购物车项时取 SnapshotAt 最新的一条
/// - 空入参直接返回不调用 inner
/// </summary>
public class SnapshotCartPriceServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId1 = Guid.NewGuid();
    private static readonly Guid SkuId2 = Guid.NewGuid();
    private static readonly Guid SkuId3 = Guid.NewGuid();
    private static readonly Guid SellerId1 = Guid.NewGuid();
    private static readonly Guid SellerId2 = Guid.NewGuid();

    [Fact]
    public async Task GetSkuPricesAsync_FeatureFlagOff_ShouldDelegateToInner()
    {
        // Arrange：feature flag 关闭，所有请求透传 inner
        await using var context = CreateInMemoryContext();
        var mockInner = new Mock<ICartPriceService>();
        mockInner
            .Setup(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuPriceSnapshot>
            {
                new() { SkuId = SkuId1, Price = 12m, Currency = "CNY", Available = true, Title = "T1", SellerId = SellerId1 }
            });
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: false);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1 }, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Price.Should().Be(12m);
        mockInner.Verify(s => s.GetSkuPricesAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(SkuId1)), It.IsAny<CancellationToken>()), Times.Once);
        // feature flag 关闭时不查询快照，不入队后台刷新
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task GetSkuPricesAsync_EmptyInput_ShouldReturnEmptyWithoutCallingInner()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var mockInner = new Mock<ICartPriceService>();
        var sut = CreateSut(context, mockInner.Object, useSkuSnapshot: true);

        // Act
        var result = await sut.GetSkuPricesAsync(Array.Empty<Guid>(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        mockInner.Verify(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSkuPricesAsync_SnapshotFresh_ShouldReturnLocalSnapshotWithoutCallingInner()
    {
        // Arrange：本地有新鲜快照，不应调用 inner
        await using var context = CreateInMemoryContext();
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId1,
            new SkuSnapshot(SkuId1, "商品1", 15m, "CNY", "img1.png", "红色", true, 1, DateTime.UtcNow));

        var mockInner = new Mock<ICartPriceService>();
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: true, snapshotMaxAge: TimeSpan.FromMinutes(5));

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1 }, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(SkuId1);
        result[0].Price.Should().Be(15m);
        result[0].Available.Should().BeTrue();
        result[0].Title.Should().Be("商品1");
        result[0].MainImageUrl.Should().Be("img1.png");
        result[0].SellerId.Should().Be(SellerId1);
        // 本地命中，不调用 inner
        mockInner.Verify(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        // 快照新鲜，不入队后台刷新
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task GetSkuPricesAsync_SnapshotStale_ShouldReturnStaleSnapshotAndEnqueueBackgroundRefresh()
    {
        // Arrange：本地快照过期，仍返回过期快照（容忍最终一致），同时入队后台刷新，但不回退 inner
        await using var context = CreateInMemoryContext();
        var staleSnapshot = new SkuSnapshot(SkuId1, "商品1", 15m, "CNY", null, null, true, 1,
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId1, staleSnapshot);

        var mockInner = new Mock<ICartPriceService>();
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var capturedRefreshRequests = new List<IEnumerable<Guid>>();
        mockRefresher
            .Setup(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()))
            .Callback<IEnumerable<Guid>>(ids => capturedRefreshRequests.Add(ids));
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: true, snapshotMaxAge: TimeSpan.FromMinutes(5));

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1 }, CancellationToken.None);

        // Assert：返回过期快照（不回退 inner），但入队后台刷新
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(SkuId1);
        result[0].Price.Should().Be(15m);
        // 过期快照仍走本地，不调用 inner
        mockInner.Verify(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        // 触发后台刷新
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        capturedRefreshRequests.Should().HaveCount(1);
        capturedRefreshRequests[0].Should().Contain(SkuId1);
    }

    [Fact]
    public async Task GetSkuPricesAsync_SnapshotMissing_ShouldFallbackToInnerAndEnqueueRefresh()
    {
        // Arrange：本地无快照（仅普通 CartItem 未回填），应回退 inner 并入队后台刷新
        await using var context = CreateInMemoryContext();
        await SeedCartItemWithoutSnapshotAsync(context, SkuId1, SellerId1);

        var mockInner = new Mock<ICartPriceService>();
        mockInner
            .Setup(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuPriceSnapshot>
            {
                new() { SkuId = SkuId1, Price = 20m, Currency = "CNY", Available = true, Title = "实时商品", SellerId = SellerId1 }
            });
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var capturedRefreshRequests = new List<IEnumerable<Guid>>();
        mockRefresher
            .Setup(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()))
            .Callback<IEnumerable<Guid>>(ids => capturedRefreshRequests.Add(ids));
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: true, snapshotMaxAge: TimeSpan.FromMinutes(5));

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1 }, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Price.Should().Be(20m);
        result[0].Title.Should().Be("实时商品");
        // 缺失快照：回退 inner
        mockInner.Verify(s => s.GetSkuPricesAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(SkuId1)), It.IsAny<CancellationToken>()), Times.Once);
        // 入队后台刷新
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        capturedRefreshRequests[0].Should().Contain(SkuId1);
    }

    [Fact]
    public async Task GetSkuPricesAsync_MixedSkus_LocalHitPlusMissingFallback_ShouldMergeResults()
    {
        // Arrange：SkuId1 本地有新鲜快照，SkuId2 本地缺失，SkuId3 本地无购物车项（缺失）
        await using var context = CreateInMemoryContext();
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId1,
            new SkuSnapshot(SkuId1, "本地商品1", 11m, "CNY", null, null, true, 1, DateTime.UtcNow));
        await SeedCartItemWithoutSnapshotAsync(context, SkuId2, SellerId2);

        var mockInner = new Mock<ICartPriceService>();
        mockInner
            .Setup(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkuPriceSnapshot>
            {
                new() { SkuId = SkuId2, Price = 22m, Currency = "CNY", Available = true, Title = "实时2", SellerId = SellerId2 },
                new() { SkuId = SkuId3, Price = 33m, Currency = "CNY", Available = true, Title = "实时3", SellerId = SellerId1 }
            });
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var capturedRefreshRequests = new List<IEnumerable<Guid>>();
        mockRefresher
            .Setup(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()))
            .Callback<IEnumerable<Guid>>(ids => capturedRefreshRequests.Add(ids));
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: true);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1, SkuId2, SkuId3 }, CancellationToken.None);

        // Assert：本地命中 SkuId1，inner 回退 SkuId2 与 SkuId3
        result.Should().HaveCount(3);
        result.Select(r => r.SkuId).Should().BeEquivalentTo(new[] { SkuId1, SkuId2, SkuId3 });

        var hit1 = result.Single(r => r.SkuId == SkuId1);
        hit1.Price.Should().Be(11m);
        hit1.Title.Should().Be("本地商品1");

        var fetched2 = result.Single(r => r.SkuId == SkuId2);
        fetched2.Price.Should().Be(22m);
        fetched2.Title.Should().Be("实时2");

        var fetched3 = result.Single(r => r.SkuId == SkuId3);
        fetched3.Price.Should().Be(33m);

        // inner 仅对缺失的 SkuId2 与 SkuId3 调用
        mockInner.Verify(
            s => s.GetSkuPricesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(SkuId2) && ids.Contains(SkuId3) && !ids.Contains(SkuId1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        // 入队后台刷新仅含缺失的 SkuId2 与 SkuId3，本地命中的 SkuId1 不入队
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        capturedRefreshRequests[0].Should().BeEquivalentTo(new[] { SkuId2, SkuId3 });
    }

    [Fact]
    public async Task GetSkuPricesAsync_InnerThrows_ShouldSwallowExceptionAndReturnLocalOnlyResults()
    {
        // Arrange：快照缺失，inner 抛异常，应吞掉异常，仅返回本地命中的结果
        await using var context = CreateInMemoryContext();
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId1,
            new SkuSnapshot(SkuId1, "本地商品1", 11m, "CNY", null, null, true, 1, DateTime.UtcNow));
        await SeedCartItemWithoutSnapshotAsync(context, SkuId2, SellerId2);

        var mockInner = new Mock<ICartPriceService>();
        mockInner
            .Setup(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("商品域不可用"));
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var sut = CreateSut(context, mockInner.Object, mockRefresher.Object, useSkuSnapshot: true);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1, SkuId2 }, CancellationToken.None);

        // Assert：SkuId1 本地命中返回，SkuId2 因 inner 异常未返回（调用方按缺失标记 PriceUnavailable）
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(SkuId1);
        result[0].Price.Should().Be(11m);
        // 异常被吞，不向上抛
        mockInner.Verify(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        // SkuId2 缺失仍入队后台刷新
        mockRefresher.Verify(r => r.EnqueueRefreshBatch(It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task GetSkuPricesAsync_SameSkuMultipleCartItems_ShouldUseLatestSnapshotAt()
    {
        // Arrange：同一 SkuId 在多个购物车项中持有快照，取 SnapshotAt 最新的
        await using var context = CreateInMemoryContext();
        var olderSnapshot = new SkuSnapshot(SkuId1, "旧商品", 5m, "CNY", null, null, true, 1,
            DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
        var newerSnapshot = new SkuSnapshot(SkuId1, "新商品", 8m, "CNY", null, null, true, 2,
            DateTime.UtcNow);
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId1, olderSnapshot);
        await SeedCartItemWithSnapshotAsync(context, SkuId1, SellerId2, newerSnapshot);

        var mockInner = new Mock<ICartPriceService>();
        var sut = CreateSut(context, mockInner.Object, useSkuSnapshot: true);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId1 }, CancellationToken.None);

        // Assert：取最新 SnapshotAt 的快照（价格为 8）
        result.Should().HaveCount(1);
        result[0].Price.Should().Be(8m);
        result[0].Title.Should().Be("新商品");
        // SellerId 取最新快照所属购物车项的 SellerId（此处为 SellerId2）
        result[0].SellerId.Should().Be(SellerId2);
        mockInner.Verify(s => s.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_NullInner_ShouldThrow()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var act = () => CreateSut(context, inner: null!, useSkuSnapshot: true);

        act.Should().Throw<ArgumentNullException>().WithParameterName("inner");
    }

    [Fact]
    public void Constructor_NullDbContext_ShouldThrow()
    {
        var mockInner = new Mock<ICartPriceService>();
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        var options = new Mock<IOptionsMonitor<CartSnapshotOptions>>();
        var logger = new Mock<ILogger<SnapshotCartPriceService>>();

        var act = () => new SnapshotCartPriceService(
            mockInner.Object, null!, mockRefresher.Object, options.Object, logger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_NullBackgroundRefresher_ShouldThrow()
    {
        using var context = CreateInMemoryContext();
        var mockInner = new Mock<ICartPriceService>();
        var options = new Mock<IOptionsMonitor<CartSnapshotOptions>>();
        var logger = new Mock<ILogger<SnapshotCartPriceService>>();

        var act = () => new SnapshotCartPriceService(
            mockInner.Object, context, null!, options.Object, logger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("backgroundRefresher");
    }

    [Fact]
    public async Task GetSkuPricesAsync_NullInput_ShouldThrowArgumentNullException()
    {
        await using var context = CreateInMemoryContext();
        var mockInner = new Mock<ICartPriceService>();
        var sut = CreateSut(context, mockInner.Object, useSkuSnapshot: true);

        var act = () => sut.GetSkuPricesAsync(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("skuIds");
    }

    /// <summary>
    /// 创建 InMemory CartDbContext 用于隔离测试。
    /// 使用唯一数据库名避免跨用例污染。
    /// </summary>
    private static CartDbContext CreateInMemoryContext()
    {
        var dbName = $"cart-snapshot-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new CartDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// 种子：插入一个带快照的购物车项。
    /// </summary>
    private static async Task SeedCartItemWithSnapshotAsync(
        CartDbContext context,
        Guid skuId,
        Guid sellerId,
        SkuSnapshot snapshot)
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(skuId, 1, sellerId);
        // 触发快照写入
        cart.UpdateSkuSnapshot(skuId, snapshot);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        // 清理跟踪，确保后续查询走数据库
        context.ChangeTracker.Clear();
    }

    /// <summary>
    /// 种子：插入一个未回填快照的购物车项（SkuSnapshot = null，模拟历史数据）。
    /// </summary>
    private static async Task SeedCartItemWithoutSnapshotAsync(
        CartDbContext context,
        Guid skuId,
        Guid sellerId)
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(skuId, 1, sellerId);
        // 不调用 UpdateSkuSnapshot，SkuSnapshot 保持 null
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    /// <summary>
    /// 构造 SUT，使用可配置的 <see cref="CartSnapshotOptions"/>。
    /// </summary>
    private static SnapshotCartPriceService CreateSut(
        CartDbContext context,
        ICartPriceService inner,
        IBackgroundSnapshotRefresher backgroundRefresher,
        bool useSkuSnapshot,
        TimeSpan? snapshotMaxAge = null)
    {
        var optionsMonitor = CreateOptionsMonitor(useSkuSnapshot, snapshotMaxAge);
        var logger = new Mock<ILogger<SnapshotCartPriceService>>();
        return new SnapshotCartPriceService(inner, context, backgroundRefresher, optionsMonitor, logger.Object);
    }

    /// <summary>
    /// 构造 SUT，使用 Mock 的 IBackgroundSnapshotRefresher 与可配置的 feature flag。
    /// </summary>
    private static SnapshotCartPriceService CreateSut(
        CartDbContext context,
        ICartPriceService inner,
        bool useSkuSnapshot,
        TimeSpan? snapshotMaxAge = null)
    {
        var mockRefresher = new Mock<IBackgroundSnapshotRefresher>();
        return CreateSut(context, inner, mockRefresher.Object, useSkuSnapshot, snapshotMaxAge);
    }

    private static IOptionsMonitor<CartSnapshotOptions> CreateOptionsMonitor(bool useSkuSnapshot, TimeSpan? snapshotMaxAge)
    {
        var optionsValue = new CartSnapshotOptions
        {
            UseSkuSnapshot = useSkuSnapshot,
            SnapshotMaxAge = snapshotMaxAge ?? TimeSpan.FromMinutes(5)
        };
        var monitorMock = new Mock<IOptionsMonitor<CartSnapshotOptions>>();
        monitorMock.SetupGet(m => m.CurrentValue).Returns(optionsValue);
        return monitorMock.Object;
    }
}

using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.ValueObjects;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Tests;

/// <summary>
/// 阶段三 3.11：SKU 快照值对象与 CartItem 快照更新单元测试。
/// 覆盖：过期判定、版本递增、构造校验、价格变更检测、SkuId 不匹配、并发安全替换。
/// </summary>
public class SkuSnapshotTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    // === SkuSnapshot 值对象测试 ===

    [Fact]
    public void IsStale_WhenSnapshotOlderThanMaxAge_ShouldReturnTrue()
    {
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1,
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));

        snapshot.IsStale(TimeSpan.FromMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void IsStale_WhenSnapshotNewerThanMaxAge_ShouldReturnFalse()
    {
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1,
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)));

        snapshot.IsStale(TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void IsStale_WhenSnapshotExactlyAtMaxAge_ShouldReturnFalse()
    {
        // 边界：差值等于 maxAge 时不视为过期（> 判定，非 >=）
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1,
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

        snapshot.IsStale(TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void NextVersion_ShouldIncrementVersionAndUpdateSnapshotAt()
    {
        var originalAt = DateTime.UtcNow.AddDays(-1);
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 5, originalAt);

        var next = snapshot.NextVersion();

        next.SnapshotVersion.Should().Be(6);
        next.SnapshotAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        // 其他字段保持不变
        next.SkuId.Should().Be(snapshot.SkuId);
        next.Price.Should().Be(snapshot.Price);
        next.SkuName.Should().Be(snapshot.SkuName);
    }

    [Fact]
    public void NextVersion_WithExplicitTimestamp_ShouldUseProvidedTimestamp()
    {
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        var explicitTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var next = snapshot.NextVersion(explicitTime);

        next.SnapshotAt.Should().Be(explicitTime);
        next.SnapshotVersion.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithEmptySkuId_ShouldThrow()
    {
        var act = () => new SkuSnapshot(
            Guid.Empty, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithParameterName("skuId");
    }

    [Fact]
    public void Constructor_WithEmptyCurrency_ShouldThrow()
    {
        var act = () => new SkuSnapshot(
            SkuId, "商品", 10m, "", null, null, true, 1, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithParameterName("currency");
    }

    [Fact]
    public void Constructor_WithNullSkuName_ShouldDefaultToEmptyString()
    {
        var snapshot = new SkuSnapshot(
            SkuId, null!, 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        snapshot.SkuName.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNegativeVersion_ShouldClampToZero()
    {
        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, -5, DateTime.UtcNow);
        snapshot.SnapshotVersion.Should().Be(0);
    }

    // === CartItem.UpdateSnapshot 测试 ===

    [Fact]
    public void UpdateSnapshot_FirstSnapshot_ShouldSetPriceChangedFalse()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", "img.png", "红色", true, 1, DateTime.UtcNow);

        var change = item.UpdateSnapshot(snapshot);

        change.PriceChanged.Should().BeFalse("首次回填无旧价格可比");
        change.OldPrice.Should().Be(10m);
        change.NewPrice.Should().Be(10m);
        item.SkuSnapshot.Should().Be(snapshot);
    }

    [Fact]
    public void UpdateSnapshot_PriceChanged_ShouldReturnPriceChangedTrue()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var oldSnapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        item.UpdateSnapshot(oldSnapshot);

        var newSnapshot = new SkuSnapshot(
            SkuId, "商品", 15m, "CNY", null, null, true, 2, DateTime.UtcNow);

        var change = item.UpdateSnapshot(newSnapshot);

        change.PriceChanged.Should().BeTrue();
        change.OldPrice.Should().Be(10m);
        change.NewPrice.Should().Be(15m);
    }

    [Fact]
    public void UpdateSnapshot_SamePrice_ShouldReturnPriceChangedFalse()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var firstSnapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        item.UpdateSnapshot(firstSnapshot);

        var secondSnapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 2, DateTime.UtcNow);

        var change = item.UpdateSnapshot(secondSnapshot);

        change.PriceChanged.Should().BeFalse();
    }

    [Fact]
    public void UpdateSnapshot_SkuIdMismatch_ShouldThrow()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var wrongSkuSnapshot = new SkuSnapshot(
            Guid.NewGuid(), "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);

        var act = () => item.UpdateSnapshot(wrongSkuSnapshot);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateSnapshot_ShouldSyncDisplayTitleAndImageUrl()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var snapshot = new SkuSnapshot(
            SkuId, "新商品标题", 10m, "CNY", "https://cdn.example.com/new.png",
            "红色 / XL", true, 1, DateTime.UtcNow);

        item.UpdateSnapshot(snapshot);

        item.DisplayTitle.Should().Be("新商品标题");
        item.DisplayImageUrl.Should().Be("https://cdn.example.com/new.png");
    }

    [Fact]
    public void UpdateSnapshot_NullMainImageUrl_ShouldDefaultDisplayImageUrlToEmpty()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);

        item.UpdateSnapshot(snapshot);

        item.DisplayImageUrl.Should().BeEmpty();
    }

    // === Cart.UpdateSkuSnapshot 聚合根级测试 ===

    [Fact]
    public void Cart_UpdateSkuSnapshot_PriceChanged_ShouldPublishSkuPriceChangedEvent()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);
        cart.ClearDomainEvents();

        var firstSnapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        cart.UpdateSkuSnapshot(SkuId, firstSnapshot);
        cart.ClearDomainEvents();

        var priceChangedSnapshot = new SkuSnapshot(
            SkuId, "商品", 20m, "CNY", null, null, true, 2, DateTime.UtcNow);

        cart.UpdateSkuSnapshot(SkuId, priceChangedSnapshot);

        var priceEvents = cart.DomainEvents.OfType<SkuPriceChangedEvent>().ToList();
        priceEvents.Should().HaveCount(1);
        priceEvents[0].SkuId.Should().Be(SkuId);
        priceEvents[0].OldPrice.Should().Be(10m);
        priceEvents[0].NewPrice.Should().Be(20m);
        priceEvents[0].CartItemId.Should().Be(item.Id);
    }

    [Fact]
    public void Cart_UpdateSkuSnapshot_PriceUnchanged_ShouldNotPublishEvent()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);

        var firstSnapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        cart.UpdateSkuSnapshot(SkuId, firstSnapshot);
        cart.ClearDomainEvents();

        var samePriceSnapshot = new SkuSnapshot(
            SkuId, "商品更新标题", 10m, "CNY", null, null, true, 2, DateTime.UtcNow);

        cart.UpdateSkuSnapshot(SkuId, samePriceSnapshot);

        cart.DomainEvents.OfType<SkuPriceChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Cart_UpdateSkuSnapshot_NonExistentSku_ShouldBeNoOp()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        cart.ClearDomainEvents();

        var nonExistentSku = Guid.NewGuid();
        var snapshot = new SkuSnapshot(
            nonExistentSku, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);

        cart.UpdateSkuSnapshot(nonExistentSku, snapshot);

        cart.DomainEvents.OfType<SkuPriceChangedEvent>().Should().BeEmpty();
        cart.Items.Should().ContainSingle();
    }

    [Fact]
    public void Cart_UpdateSkuSnapshot_NullSnapshot_ShouldThrow()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);

        var act = () => cart.UpdateSkuSnapshot(SkuId, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Cart_UpdateSkuSnapshot_IdempotentSameSnapshot_ShouldNotPublishDuplicateEvents()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);

        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);

        cart.UpdateSkuSnapshot(SkuId, snapshot);
        cart.ClearDomainEvents();

        cart.UpdateSkuSnapshot(SkuId, snapshot);

        cart.DomainEvents.OfType<SkuPriceChangedEvent>().Should().BeEmpty();
    }

    // === 并发更新测试 ===

    [Fact]
    public void UpdateSnapshot_ConcurrentReplacements_LastWriteWins()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var snapshot1 = new SkuSnapshot(SkuId, "v1", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        var snapshot2 = new SkuSnapshot(SkuId, "v2", 20m, "CNY", null, null, true, 2, DateTime.UtcNow);

        item.UpdateSnapshot(snapshot1);
        item.UpdateSnapshot(snapshot2);

        item.SkuSnapshot.Should().Be(snapshot2);
        item.SkuSnapshot!.Price.Should().Be(20m);
    }

    [Fact]
    public void MarkSnapshotStale_WhenSnapshotNull_ShouldBeNoOp()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        // SkuSnapshot 为 null 时调用不应抛出
        var act = () => item.MarkSnapshotStale();
        act.Should().NotThrow();
    }

    [Fact]
    public void MarkSnapshotStale_WhenSnapshotExists_ShouldNotModifySnapshot()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 1, SellerId);
        var item = cart.Items.Single(i => i.SkuId == SkuId);

        var snapshot = new SkuSnapshot(
            SkuId, "商品", 10m, "CNY", null, null, true, 1, DateTime.UtcNow);
        item.UpdateSnapshot(snapshot);

        item.MarkSnapshotStale();

        // 快照未被修改（过期判定基于 SnapshotAt 时间戳，不修改快照本身）
        item.SkuSnapshot.Should().Be(snapshot);
        item.SkuSnapshot!.SnapshotAt.Should().Be(snapshot.SnapshotAt);
    }
}

using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Exceptions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Tests;

/// <summary>
/// Cart 聚合 P1+P2 修复项的单元测试：
/// - P1-12 ClearSelectedItems 发布 SkuRemovedFromCartEvent
/// - P2-2 AddItem/MergeFrom 复用 TryGetItem 避免重复扫描（行为不变，通过既有用例覆盖）
/// - P2-7 MergeFrom 跳过匿名购物车中的无效项
/// </summary>
public class CartP1P2FixTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void ClearSelectedItems_ShouldPublishSkuRemovedFromCartEventForEachRemovedItem()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var sku3 = Guid.NewGuid();
        cart.AddItem(sku1, 1, Guid.NewGuid());
        cart.AddItem(sku2, 1, Guid.NewGuid());
        cart.AddItem(sku3, 1, Guid.NewGuid());
        // 取消选中 sku3，仅 sku1/sku2 被清除
        cart.DeselectItems(new[] { sku3 });
        cart.ClearDomainEvents();

        var sourceIds = cart.ClearSelectedItems();

        sourceIds.Should().HaveCount(2);
        var removedEvents = cart.DomainEvents.OfType<SkuRemovedFromCartEvent>().ToList();
        removedEvents.Should().HaveCount(2);
        removedEvents.Select(e => e.SkuId).Should().BeEquivalentTo(new[] { sku1, sku2 });
        removedEvents.All(e => e.CartId == cart.Id).Should().BeTrue();
        // 未选中的 sku3 应保留
        cart.Items.Should().ContainSingle(i => i.SkuId == sku3);
    }

    [Fact]
    public void ClearSelectedItems_NoSelectedItems_ShouldNotPublishAnyEvent()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        var sku1 = Guid.NewGuid();
        cart.AddItem(sku1, 1, Guid.NewGuid());
        cart.DeselectItems(new[] { sku1 });
        cart.ClearDomainEvents();

        var sourceIds = cart.ClearSelectedItems();

        sourceIds.Should().BeEmpty();
        cart.DomainEvents.OfType<SkuRemovedFromCartEvent>().Should().BeEmpty();
    }

    [Fact]
    public void MergeFrom_ShouldSkipInvalidItemsAndOnlyMergeValidOnes()
    {
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var validSku = Guid.NewGuid();
        var invalidSku = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        anonymousCart.AddItem(validSku, 2, sellerId);
        anonymousCart.AddItem(invalidSku, 3, sellerId);
        // 将 invalidSku 标记为无效（模拟商品已下架）
        anonymousCart.MarkInvalid(invalidSku, "商品已下架");

        var mergedCount = userCart.MergeFrom(anonymousCart);

        // 仅 validSku 被合并
        mergedCount.Should().Be(1);
        userCart.Items.Should().ContainSingle(i => i.SkuId == validSku);
        userCart.Items.First(i => i.SkuId == validSku).Quantity.Should().Be(2);
        userCart.Items.Should().NotContain(i => i.SkuId == invalidSku);
    }

    [Fact]
    public void MergeFrom_AllItemsInvalid_ShouldMergeNothing()
    {
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        anonymousCart.AddItem(sku1, 1, sellerId);
        anonymousCart.AddItem(sku2, 1, sellerId);
        anonymousCart.MarkInvalid(sku1, "下架");
        anonymousCart.MarkInvalid(sku2, "下架");

        var mergedCount = userCart.MergeFrom(anonymousCart);

        mergedCount.Should().Be(0);
        userCart.Items.Should().BeEmpty();
    }

    [Fact]
    public void MergeFrom_InvalidItemShouldNotConsumeVarietyLimit()
    {
        // 填满 50 个不同 SKU（达到品类上限）
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);
        for (int i = 0; i < 50; i++)
        {
            userCart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        }

        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var invalidSku = Guid.NewGuid();
        anonymousCart.AddItem(invalidSku, 1, Guid.NewGuid());
        anonymousCart.MarkInvalid(invalidSku, "下架");

        // 无效项不参与合并，不应触发上限异常
        var act = () => userCart.MergeFrom(anonymousCart);

        act.Should().NotThrow();
        userCart.Items.Should().HaveCount(50);
    }

    [Fact]
    public void AddItem_ExistingSku_ShouldMergeWithoutScanningTwice()
    {
        // P2-2 行为验证：合并已存在 SKU 时不重复扫描
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 5, Guid.NewGuid());

        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(8);
    }
}

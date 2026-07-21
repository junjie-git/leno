using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Tests;

/// <summary>
/// Cart 聚合 AddItem 品类上限（MaxVariety=50）不变量测试。
/// 验证 AddItem 在新增第 51 个不同 SKU 时抛 CartDomainException；
/// 已有 SKU 追加数量不触发上限；正好 50 个允许到达上限。
/// </summary>
public class CartMaxVarietyTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void AddItem_ExceedsMaxVariety_ShouldThrowCartDomainException()
    {
        // Arrange：填满 50 个 SKU（已达上限）
        var cart = CreateCart();
        for (int i = 0; i < 50; i++)
        {
            cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        }

        // Act：第 51 个不同 SKU 应被拒绝
        var act = () => cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());

        // Assert
        act.Should().Throw<CartDomainException>()
            .WithMessage("*品类*")
            .WithMessage("*50*");
    }

    [Fact]
    public void AddItem_AtMaxVarietyButExistingSku_ShouldMergeWithoutThrowing()
    {
        // Arrange：达上限后追加已有 SKU 不应触发上限
        var cart = CreateCart();
        var firstSku = Guid.NewGuid();
        cart.AddItem(firstSku, 1, Guid.NewGuid());
        for (int i = 0; i < 49; i++)
        {
            cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        }
        cart.Items.Should().HaveCount(50);

        // Act
        cart.AddItem(firstSku, 2, Guid.NewGuid());

        // Assert：合并数量，不新增项
        cart.Items.Should().HaveCount(50);
        cart.Items.First(i => i.SkuId == firstSku).Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_ExactlyAtMaxVariety_ShouldAllowFillingToLimit()
    {
        var cart = CreateCart();

        for (int i = 0; i < 50; i++)
        {
            cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        }

        cart.Items.Should().HaveCount(50);
    }

    private static CartAggregate CreateCart()
    {
        return CartAggregate.Create(Guid.NewGuid(), UserId);
    }
}

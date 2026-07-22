using Leno.SharedKernel.ValueObjects;

namespace Leno.Infrastructure.Tests.ValueObjects;

/// <summary>
/// P1-T11 单元测试：验证 <see cref="Money.RequirePositive()"/> 方法。
/// 修复审计 #11：Money.Create 允许 amount=0（语义为"免费/赠品"），各 BC 自行决定是否拒绝 0。
/// RequirePositive() 提供"金额必须为正"的显式断言，不满足时抛 ArgumentException。
/// </summary>
public class MoneyRequirePositiveTests
{
    /// <summary>
    /// Money.Create(0, "CNY") 应合法（0 表示免费/赠品），不抛异常。
    /// </summary>
    [Fact]
    public void Create_ZeroAmount_ShouldBeValid()
    {
        var act = () => Money.Create(0m, "CNY");

        act.Should().NotThrow();
        var money = Money.Create(0m, "CNY");
        money.Amount.Should().Be(0m);
    }

    /// <summary>
    /// 正数金额调用 RequirePositive() 应返回自身（链式可用），不抛异常。
    /// </summary>
    [Fact]
    public void RequirePositive_PositiveAmount_ReturnsSelf()
    {
        var money = Money.Create(99.99m, "CNY");

        var result = money.RequirePositive();

        result.Should().BeSameAs(money);
        result.Amount.Should().Be(99.99m);
    }

    /// <summary>
    /// 零金额调用 RequirePositive() 应抛 ArgumentException。
    /// </summary>
    [Fact]
    public void RequirePositive_ZeroAmount_ThrowsArgumentException()
    {
        var money = Money.Create(0m, "CNY");

        var act = () => money.RequirePositive();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*正数*");
    }

    /// <summary>
    /// Money.Create 不允许负数，因此 RequirePositive 的负数路径不可直接测试；
    /// 验证 Money.Create(-1, "CNY") 自身抛 ArgumentException。
    /// </summary>
    [Fact]
    public void Create_NegativeAmount_ThrowsArgumentException()
    {
        var act = () => Money.Create(-1m, "CNY");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*负*");
    }

    /// <summary>
    /// RequirePositive() 可用于链式调用：Money.Create(100, "CNY").RequirePositive() 合法。
    /// </summary>
    [Fact]
    public void RequirePositive_ChainedAfterCreate_PositiveAmount_DoesNotThrow()
    {
        var act = () => Money.Create(100m, "CNY").RequirePositive();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Money.Zero("CNY") 应创建金额为 0 的实例，调用 RequirePositive() 应抛异常。
    /// 验证 Zero 工厂方法与 RequirePositive 的协作。
    /// </summary>
    [Fact]
    public void Zero_ThenRequirePositive_ThrowsArgumentException()
    {
        var money = Money.Zero("CNY");
        money.Amount.Should().Be(0m);

        var act = () => money.RequirePositive();

        act.Should().Throw<ArgumentException>();
    }
}

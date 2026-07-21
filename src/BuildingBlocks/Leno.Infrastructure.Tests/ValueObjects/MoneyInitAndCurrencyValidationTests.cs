using Leno.SharedKernel.ValueObjects;

namespace Leno.Infrastructure.Tests.ValueObjects;

/// <summary>
/// T29 + T30 单元测试：
/// T29 — Money 属性改为 init，验证对象初始化器与反射赋值（EF Core 反序列化路径）可工作。
/// T30 — 币种校验改用 != 3，验证 1/2/4/5 位币种码被拒绝、3 位通过。
/// 不修改既有 SellerShop.Domain.Tests/MoneyTests.cs 中的断言。
/// </summary>
public class MoneyInitAndCurrencyValidationTests
{
    // ===== T29：init 属性可写性测试 =====

    /// <summary>
    /// T29：init 属性应可通过反射赋值，模拟 EF Core 物化路径。
    /// EF Core 在 .NET 10 中通过反射调用 init setter 设置属性值，
    /// 原 private set 也支持反射，但 init 明确表达"仅构造阶段可写"的意图。
    /// </summary>
    [Fact]
    public void Money_InitProperties_ShouldBeSettableViaReflection_EfCoreMaterializationPath()
    {
        // Arrange — 模拟 EF Core 物化路径：先创建默认实例（无参私有构造），再通过反射设置属性
        var money = (Money)Activator.CreateInstance(typeof(Money), nonPublic: true)!;

        // Act — 通过反射调用 init setter（EF Core 在 .NET 10 中支持 init 属性的反射赋值）
        var amountProp = typeof(Money).GetProperty(nameof(Money.Amount));
        var currencyProp = typeof(Money).GetProperty(nameof(Money.Currency));
        amountProp!.SetValue(money, 42.50m);
        currencyProp!.SetValue(money, "JPY");

        // Assert — 反射赋值成功，值正确
        money.Amount.Should().Be(42.50m);
        money.Currency.Should().Be("JPY");
    }

    /// <summary>
    /// T29：使用 Create 工厂方法构造的 Money，其属性值应与原 private set 行为一致。
    /// 验证 init 改造未破坏工厂方法的正常路径。
    /// </summary>
    [Fact]
    public void Money_Create_FactoryStillWorksAfterInitChange()
    {
        var money = Money.Create(123.45m, "USD");

        money.Amount.Should().Be(123.45m);
        money.Currency.Should().Be("USD");
    }

    /// <summary>
    /// T29：init 改造后，record 相等性契约不变（基于 Amount + Currency）。
    /// </summary>
    [Fact]
    public void Money_RecordEquality_StillWorksAfterInitChange()
    {
        var m1 = Money.Create(100m, "USD");
        var m2 = Money.Create(100m, "USD");
        var m3 = Money.Create(200m, "USD");

        m1.Should().Be(m2);
        m1.GetHashCode().Should().Be(m2.GetHashCode());
        m1.Should().NotBe(m3);
    }

    /// <summary>
    /// T29：init 属性的 CanWrite 应为 true（验证编译器确实生成了可写的 init setter，
    /// 而非误改为 get-only）。EF Core 通过 CanWrite 检查决定是否尝试赋值。
    /// </summary>
    [Fact]
    public void Money_InitProperties_CanWrite_ShouldBeTrue()
    {
        var amountProp = typeof(Money).GetProperty(nameof(Money.Amount));
        var currencyProp = typeof(Money).GetProperty(nameof(Money.Currency));

        amountProp!.CanWrite.Should().BeTrue("Amount 应有 init setter 可写");
        currencyProp!.CanWrite.Should().BeTrue("Currency 应有 init setter 可写");
    }

    // ===== T30：币种校验 != 3 测试 =====

    /// <summary>
    /// T30：3 位币种码应通过校验（ISO 4217 标准）。
    /// </summary>
    [Theory]
    [InlineData("USD")]
    [InlineData("CNY")]
    [InlineData("EUR")]
    [InlineData("JPY")]
    [InlineData("GBP")]
    public void Create_WithThreeLetterCurrency_ShouldSucceed(string currency)
    {
        var money = Money.Create(100m, currency);

        money.Currency.Should().Be(currency);
    }

    /// <summary>
    /// T30：1/2/4/5 位币种码应被拒绝（验证 != 3 校验生效）。
    /// </summary>
    [Theory]
    [InlineData("U")]        // 1 位
    [InlineData("US")]       // 2 位
    [InlineData("USDD")]     // 4 位
    [InlineData("USDDD")]    // 5 位
    [InlineData("USDDDD")]   // 6 位
    public void Create_WithNonThreeLetterCurrency_ShouldThrow(string currency)
    {
        var act = () => Money.Create(100m, currency);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种须为 3 位 ISO 4217 代码*");
    }

    /// <summary>
    /// T30：3 位但带空格的币种码经 Trim+Upper 后应通过校验。
    /// </summary>
    [Fact]
    public void Create_WithThreeLetterCurrencyWithSpaces_ShouldTrimAndSucceed()
    {
        var money = Money.Create(100m, " usd ");

        money.Currency.Should().Be("USD");
    }

    /// <summary>
    /// T30：3 位小写币种码经 ToUpperInvariant 后应通过校验。
    /// </summary>
    [Fact]
    public void Create_WithLowercaseThreeLetterCurrency_ShouldNormalizeToUpper()
    {
        var money = Money.Create(100m, "cny");

        money.Currency.Should().Be("CNY");
    }

    /// <summary>
    /// T30：空/空白/null 币种码应抛"币种不可为空"（早于长度校验）。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCurrency_ShouldThrowEmptyError(string currency)
    {
        var act = () => Money.Create(100m, currency);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种不可为空*");
    }

    [Fact]
    public void Create_WithNullCurrency_ShouldThrowEmptyError()
    {
        var act = () => Money.Create(100m, null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种不可为空*");
    }
}

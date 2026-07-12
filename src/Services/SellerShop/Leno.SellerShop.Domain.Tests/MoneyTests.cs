using Leno.SharedKernel.ValueObjects;

namespace Leno.SellerShop.Domain.Tests;

public class MoneyTests
{
    private const string Usd = "USD";
    private const string Cny = "CNY";

    // ==================== Create ====================

    [Fact]
    public void Create_WithValidParameters_ShouldSetAmountAndCurrency()
    {
        var money = Money.Create(100.55m, Usd);

        money.Amount.Should().Be(100.55m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldSucceed()
    {
        var money = Money.Create(0m, Usd);

        money.Amount.Should().Be(0m);
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUpper()
    {
        var money = Money.Create(100m, "usd");

        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_ShouldTrimCurrency()
    {
        var money = Money.Create(100m, " usd ");

        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithThreeDecimalPlaces_ShouldRoundToTwo()
    {
        var money = Money.Create(100.555m, Usd);

        money.Amount.Should().Be(100.56m); // AwayFromZero rounding
    }

    [Fact]
    public void Create_WithFourDecimalPlaces_ShouldRoundToTwo()
    {
        var money = Money.Create(100.554m, Usd);

        money.Amount.Should().Be(100.55m);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowArgumentException()
    {
        var act = () => Money.Create(-1m, Usd);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*金额不可为负*");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrowArgumentException()
    {
        var act = () => Money.Create(100m, "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种不可为空*");
    }

    [Fact]
    public void Create_WithWhitespaceCurrency_ShouldThrowArgumentException()
    {
        var act = () => Money.Create(100m, "   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种不可为空*");
    }

    [Fact]
    public void Create_WithNullCurrency_ShouldThrowArgumentException()
    {
        var act = () => Money.Create(100m, null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种不可为空*");
    }

    [Theory]
    [InlineData("U")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("USDDD")]
    public void Create_WithInvalidCurrencyLength_ShouldThrowArgumentException(string currency)
    {
        var act = () => Money.Create(100m, currency);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*币种须为 3 位 ISO 4217 代码*");
    }

    // ==================== Zero ====================

    [Fact]
    public void Zero_ShouldCreateMoneyWithZeroAmount()
    {
        var money = Money.Zero(Usd);

        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Zero_WithInvalidCurrency_ShouldThrow()
    {
        var act = () => Money.Zero("");

        act.Should().Throw<ArgumentException>();
    }

    // ==================== Add ====================

    [Fact]
    public void Add_WithSameCurrency_ShouldReturnSum()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(50m, Usd);

        var result = m1.Add(m2);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDecimalPlaces_ShouldRoundCorrectly()
    {
        var m1 = Money.Create(100.55m, Usd);
        var m2 = Money.Create(0.05m, Usd);

        var result = m1.Add(m2);

        result.Amount.Should().Be(100.60m);
    }

    [Fact]
    public void Add_WithMismatchedCurrency_ShouldThrowInvalidOperationException()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(50m, Cny);

        var act = () => m1.Add(m2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*币种不匹配*");
    }

    [Fact]
    public void Add_ShouldNotModifyOriginal()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(50m, Usd);

        m1.Add(m2);

        m1.Amount.Should().Be(100m); // Unchanged
    }

    // ==================== Subtract ====================

    [Fact]
    public void Subtract_WithSameCurrency_ShouldReturnDifference()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(30m, Usd);

        var result = m1.Subtract(m2);

        result.Amount.Should().Be(70m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_ResultingInNegative_ShouldAllow()
    {
        var m1 = Money.Create(50m, Usd);
        var m2 = Money.Create(100m, Usd);

        var result = m1.Subtract(m2);

        result.Amount.Should().Be(-50m);
    }

    [Fact]
    public void Subtract_WithMismatchedCurrency_ShouldThrowInvalidOperationException()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(50m, Cny);

        var act = () => m1.Subtract(m2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*币种不匹配*");
    }

    // ==================== Multiply ====================

    [Fact]
    public void Multiply_ByInt_ShouldReturnProduct()
    {
        var money = Money.Create(100m, Usd);

        var result = money.Multiply(3);

        result.Amount.Should().Be(300m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Multiply_ByDecimal_ShouldReturnProduct()
    {
        var money = Money.Create(100m, Usd);

        var result = money.Multiply(1.5m);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Multiply_ByDecimalFraction_ShouldRoundCorrectly()
    {
        var money = Money.Create(100m, Usd);

        var result = money.Multiply(0.333m);

        result.Amount.Should().Be(33.30m);
    }

    [Fact]
    public void Multiply_ByZero_ShouldReturnZero()
    {
        var money = Money.Create(100m, Usd);

        var result = money.Multiply(0);

        result.Amount.Should().Be(0m);
    }

    [Fact]
    public void Multiply_ByNegative_ShouldReturnNegative()
    {
        var money = Money.Create(100m, Usd);

        var result = money.Multiply(-2);

        result.Amount.Should().Be(-200m);
    }

    // ==================== Sum ====================

    [Fact]
    public void Sum_WithMultipleItems_ShouldReturnTotal()
    {
        var items = new[]
        {
            Money.Create(100m, Usd),
            Money.Create(200m, Usd),
            Money.Create(50m, Usd)
        };

        var result = Money.Sum(items);

        result.Amount.Should().Be(350m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Sum_WithSingleItem_ShouldReturnSameValue()
    {
        var items = new[] { Money.Create(100m, Usd) };

        var result = Money.Sum(items);

        result.Amount.Should().Be(100m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Sum_WithEmptyCollection_ShouldThrowArgumentException()
    {
        var act = () => Money.Sum(Array.Empty<Money>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*集合不可为空*");
    }

    [Fact]
    public void Sum_WithNullCollection_ShouldThrowArgumentNullException()
    {
        var act = () => Money.Sum(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Sum_WithMismatchedCurrencies_ShouldThrowInvalidOperationException()
    {
        var items = new[]
        {
            Money.Create(100m, Usd),
            Money.Create(200m, Cny)
        };

        var act = () => Money.Sum(items);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*币种不匹配*");
    }

    // ==================== Comparison operators ====================

    [Fact]
    public void CompareTo_LessThan_ShouldReturnNegative()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(200m, Usd);

        m1.CompareTo(m2).Should().BeLessThan(0);
    }

    [Fact]
    public void CompareTo_GreaterThan_ShouldReturnPositive()
    {
        var m1 = Money.Create(200m, Usd);
        var m2 = Money.Create(100m, Usd);

        m1.CompareTo(m2).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_Equal_ShouldReturnZero()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Usd);

        m1.CompareTo(m2).Should().Be(0);
    }

    [Fact]
    public void CompareTo_Null_ShouldReturnPositive()
    {
        var m1 = Money.Create(100m, Usd);

        m1.CompareTo(null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithMismatchedCurrency_ShouldThrow()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Cny);

        var act = () => m1.CompareTo(m2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OperatorLessThan_ShouldWork()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(200m, Usd);

        (m1 < m2).Should().BeTrue();
        (m2 < m1).Should().BeFalse();
    }

    [Fact]
    public void OperatorGreaterThan_ShouldWork()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(200m, Usd);

        (m2 > m1).Should().BeTrue();
        (m1 > m2).Should().BeFalse();
    }

    [Fact]
    public void OperatorLessThanOrEqual_ShouldWork()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Usd);
        var m3 = Money.Create(200m, Usd);

        (m1 <= m2).Should().BeTrue();  // Equal
        (m1 <= m3).Should().BeTrue();  // Less
        (m3 <= m1).Should().BeFalse(); // Greater
    }

    [Fact]
    public void OperatorGreaterThanOrEqual_ShouldWork()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Usd);
        var m3 = Money.Create(200m, Usd);

        (m3 >= m1).Should().BeTrue();  // Greater
        (m1 >= m2).Should().BeTrue();  // Equal
        (m1 >= m3).Should().BeFalse(); // Less
    }

    // ==================== Arithmetic operators ====================

    [Fact]
    public void OperatorPlus_ShouldReturnSum()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(50m, Usd);

        var result = m1 + m2;

        result.Amount.Should().Be(150m);
    }

    [Fact]
    public void OperatorMinus_ShouldReturnDifference()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(30m, Usd);

        var result = m1 - m2;

        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void OperatorMultiplyByInt_ShouldReturnProduct()
    {
        var money = Money.Create(100m, Usd);

        var result = money * 3;

        result.Amount.Should().Be(300m);
    }

    [Fact]
    public void OperatorMultiplyByDecimal_ShouldReturnProduct()
    {
        var money = Money.Create(100m, Usd);

        var result = money * 1.5m;

        result.Amount.Should().Be(150m);
    }

    // ==================== ToString ====================

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var money = Money.Create(100.50m, Usd);

        money.ToString().Should().Be("100.50 USD");
    }

    [Fact]
    public void ToString_WithZeroDecimals_ShouldStillShowTwoPlaces()
    {
        var money = Money.Create(100m, Usd);

        money.ToString().Should().Be("100.00 USD");
    }

    // ==================== Record equality ====================

    [Fact]
    public void Equals_SameAmountAndCurrency_ShouldBeEqual()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Usd);

        m1.Should().Be(m2);
        m1.GetHashCode().Should().Be(m2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentAmount_ShouldNotBeEqual()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(200m, Usd);

        m1.Should().NotBe(m2);
    }

    [Fact]
    public void Equals_DifferentCurrency_ShouldNotBeEqual()
    {
        var m1 = Money.Create(100m, Usd);
        var m2 = Money.Create(100m, Cny);

        m1.Should().NotBe(m2);
    }
}
using Leno.SharedKernel.ValueObjects;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;

namespace Leno.SellerShop.Domain.Tests;

public class ShopMetricsTests
{
    private static readonly Guid ValidMetricsId = Guid.NewGuid();
    private static readonly Guid ValidShopId = Guid.NewGuid();
    private static readonly DateOnly ValidDate = new(2026, 7, 12);
    private const string ValidCurrency = "CNY";

    // ==================== Create factory ====================

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.Id.Should().Be(ValidMetricsId);
        metrics.ShopId.Should().Be(ValidShopId);
        metrics.Date.Should().Be(ValidDate);
        metrics.OrderCount.Should().Be(0);
        metrics.SalesAmount.Amount.Should().Be(0);
        metrics.SalesAmount.Currency.Should().Be("CNY");
        metrics.ProductCount.Should().Be(0);
        metrics.AvgRating.Should().Be(0);
        metrics.RatingSum.Should().Be(0);
        metrics.RatingCount.Should().Be(0);
        metrics.RefundCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeRefundAmountToZero()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RefundAmount.Amount.Should().Be(0);
        metrics.RefundAmount.Currency.Should().Be("CNY");
    }

    [Fact]
    public void Create_WithEmptyMetricsId_ShouldThrowMetricsIdEmpty()
    {
        var act = () => ShopMetrics.Create(Guid.Empty, ValidShopId, ValidDate, ValidCurrency);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyShopId_ShouldThrowMetricsShopEmpty()
    {
        var act = () => ShopMetrics.Create(ValidMetricsId, Guid.Empty, ValidDate, ValidCurrency);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_SHOP_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrowMetricsCurrencyEmpty()
    {
        var act = () => ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_CURRENCY_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceCurrency_ShouldThrowMetricsCurrencyEmpty()
    {
        var act = () => ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_CURRENCY_EMPTY");
    }

    [Fact]
    public void Create_WithDifferentCurrency_ShouldNormalizeAndSet()
    {
        // Money.Create validates currency, but ShopMetrics.Create just passes it
        // The currency must be valid for Money.Zero to work
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, "usd");

        metrics.SalesAmount.Currency.Should().Be("USD");
    }

    // ==================== RecordOrder ====================

    [Fact]
    public void RecordOrder_WithSameCurrency_ShouldIncrementOrderCountAndSalesAmount()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount = Money.Create(100.50m, ValidCurrency);

        metrics.RecordOrder(amount);

        metrics.OrderCount.Should().Be(1);
        metrics.SalesAmount.Amount.Should().Be(100.50m);
    }

    [Fact]
    public void RecordOrder_MultipleOrders_ShouldAccumulate()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount1 = Money.Create(100m, ValidCurrency);
        var amount2 = Money.Create(200m, ValidCurrency);
        var amount3 = Money.Create(50m, ValidCurrency);

        metrics.RecordOrder(amount1);
        metrics.RecordOrder(amount2);
        metrics.RecordOrder(amount3);

        metrics.OrderCount.Should().Be(3);
        metrics.SalesAmount.Amount.Should().Be(350m);
    }

    [Fact]
    public void RecordOrder_WithMismatchedCurrency_ShouldThrowCurrencyMismatch()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount = Money.Create(100m, "USD");

        var act = () => metrics.RecordOrder(amount);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_CURRENCY_MISMATCH");
    }

    [Fact]
    public void RecordOrder_WithNullAmount_ShouldThrowArgumentNullException()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        var act = () => metrics.RecordOrder(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ==================== UpdateProductCount ====================

    [Fact]
    public void UpdateProductCount_WithValidValue_ShouldUpdate()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.UpdateProductCount(42);

        metrics.ProductCount.Should().Be(42);
    }

    [Fact]
    public void UpdateProductCount_WithZero_ShouldSucceed()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.UpdateProductCount(0);

        metrics.ProductCount.Should().Be(0);
    }

    [Fact]
    public void UpdateProductCount_WithNegativeValue_ShouldThrowProductCountNegative()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        var act = () => metrics.UpdateProductCount(-1);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_PRODUCT_COUNT_NEGATIVE");
    }

    // ==================== RecordRating ====================

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void RecordRating_WithValidRating_ShouldUpdateRatingStats(int rating)
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordRating(rating);

        metrics.RatingCount.Should().Be(1);
        metrics.RatingSum.Should().Be(rating);
        metrics.AvgRating.Should().Be(rating);
    }

    [Fact]
    public void RecordRating_MultipleRatings_ShouldComputeCorrectAverage()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordRating(3);
        metrics.RecordRating(5);
        metrics.RecordRating(4);

        metrics.RatingCount.Should().Be(3);
        metrics.RatingSum.Should().Be(12);
        metrics.AvgRating.Should().Be(4.00m); // 12/3 = 4.00
    }

    [Fact]
    public void RecordRating_AverageWithRounding_ShouldRoundCorrectly()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordRating(4);
        metrics.RecordRating(4);
        metrics.RecordRating(5);

        // (4+4+5)/3 = 13/3 = 4.333... rounded to 2 decimal places = 4.33
        metrics.AvgRating.Should().Be(4.33m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(10)]
    public void RecordRating_WithInvalidRating_ShouldThrowRatingRange(int rating)
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        var act = () => metrics.RecordRating(rating);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_RATING_RANGE");
    }

    // ==================== RecordRefund ====================

    [Fact]
    public void RecordRefund_ShouldIncrementRefundCount()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordRefund();
        metrics.RecordRefund();
        metrics.RecordRefund();

        metrics.RefundCount.Should().Be(3);
    }

    [Fact]
    public void RecordRefund_InitialValue_ShouldBeZero()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RefundCount.Should().Be(0);
    }

    // ==================== RecordRefund with Money ====================

    [Fact]
    public void RecordRefund_WithMoney_ShouldIncrementRefundCountAndRefundAmount()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount = Money.Create(50m, ValidCurrency);

        metrics.RecordRefund(amount);

        metrics.RefundCount.Should().Be(1);
        metrics.RefundAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void RecordRefund_WithMoneyMultipleRefunds_ShouldAccumulate()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount1 = Money.Create(30m, ValidCurrency);
        var amount2 = Money.Create(70m, ValidCurrency);

        metrics.RecordRefund(amount1);
        metrics.RecordRefund(amount2);

        metrics.RefundCount.Should().Be(2);
        metrics.RefundAmount.Amount.Should().Be(100m);
    }

    [Fact]
    public void RecordRefund_WithMoneyNullAmount_ShouldThrowArgumentNullException()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        var act = () => metrics.RecordRefund(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordRefund_WithMoneyMismatchedCurrency_ShouldThrowCurrencyMismatch()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);
        var amount = Money.Create(50m, "USD");

        var act = () => metrics.RecordRefund(amount);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("METRICS_CURRENCY_MISMATCH");
    }

    // ==================== RecordOrderCreation ====================

    [Fact]
    public void RecordOrderCreation_ShouldIncrementOrderCount()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordOrderCreation();

        metrics.OrderCount.Should().Be(1);
    }

    [Fact]
    public void RecordOrderCreation_MultipleCreations_ShouldAccumulate()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordOrderCreation();
        metrics.RecordOrderCreation();
        metrics.RecordOrderCreation();

        metrics.OrderCount.Should().Be(3);
    }

    [Fact]
    public void RecordOrderCreation_ShouldNotAffectSalesAmount()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordOrderCreation();
        metrics.RecordOrderCreation();

        metrics.OrderCount.Should().Be(2);
        metrics.SalesAmount.Amount.Should().Be(0);
    }

    // ==================== Combined operations ====================

    [Fact]
    public void FullScenario_ShouldMaintainConsistency()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordOrderCreation();
        metrics.RecordOrderCreation();
        metrics.RecordOrder(Money.Create(100m, ValidCurrency));
        metrics.RecordOrder(Money.Create(200m, ValidCurrency));
        metrics.UpdateProductCount(10);
        metrics.RecordRating(5);
        metrics.RecordRating(4);
        metrics.RecordRefund();
        metrics.RecordRefund(Money.Create(50m, ValidCurrency));

        metrics.OrderCount.Should().Be(4);
        metrics.SalesAmount.Amount.Should().Be(300m);
        metrics.ProductCount.Should().Be(10);
        metrics.RatingCount.Should().Be(2);
        metrics.RatingSum.Should().Be(9);
        metrics.AvgRating.Should().Be(4.5m);
        metrics.RefundCount.Should().Be(2);
        metrics.RefundAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void FullScenario_WithRefundAmountOnly_ShouldMaintainConsistency()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RecordRefund(Money.Create(30m, ValidCurrency));
        metrics.RecordRefund(Money.Create(70m, ValidCurrency));

        metrics.RefundCount.Should().Be(2);
        metrics.RefundAmount.Amount.Should().Be(100m);
        metrics.OrderCount.Should().Be(0);
        metrics.SalesAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void RefundAmount_InitialValueAfterCreate_ShouldBeZero()
    {
        var metrics = ShopMetrics.Create(ValidMetricsId, ValidShopId, ValidDate, ValidCurrency);

        metrics.RefundAmount.Amount.Should().Be(0);
        metrics.RefundAmount.Currency.Should().Be("CNY");
    }
}
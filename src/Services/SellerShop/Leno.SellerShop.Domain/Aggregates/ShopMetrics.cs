using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Leno.SellerShop.Domain.Exceptions;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 店铺运营指标聚合根，按店铺与日期维度聚合订单数、销售额、商品数、平均评分与售后数。
/// 由订单域、商品域、评价域事件驱动维护，查询走聚合本身（读模型同步预留扩展）。
/// </summary>
public sealed class ShopMetrics : AggregateRoot
{
    private const int MaxRating = 5;
    private const int MinRating = 1;

    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>统计日期（UTC 日期）。</summary>
    public DateOnly Date { get; private set; }

    /// <summary>当日订单数（已完成）。</summary>
    public int OrderCount { get; private set; }

    /// <summary>当日销售额。</summary>
    public Money SalesAmount { get; private set; } = null!;

    /// <summary>当日商品数快照。</summary>
    public int ProductCount { get; private set; }

    /// <summary>当日平均评分（1-5）。</summary>
    public decimal AvgRating { get; private set; }

    /// <summary>当日累计评分总和（用于增量计算平均评分）。</summary>
    public decimal RatingSum { get; private set; }

    /// <summary>当日累计评分次数。</summary>
    public int RatingCount { get; private set; }

    /// <summary>当日售后数。</summary>
    public int RefundCount { get; private set; }

    /// <summary>当日退款金额。</summary>
    public Money RefundAmount { get; private set; } = null!;

    /// <summary>EF Core 无参构造。</summary>
    private ShopMetrics() { }

    private ShopMetrics(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建指定店铺与日期的初始指标记录（零值）。
    /// </summary>
    /// <param name="metricsId">指标记录标识，由应用层生成。</param>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="date">统计日期。</param>
    /// <param name="currency">币种（ISO 4217）。</param>
    public static ShopMetrics Create(Guid metricsId, Guid shopId, DateOnly date, string currency)
    {
        if (metricsId == Guid.Empty)
        {
            throw new SellerShopDomainException("指标标识不可为空", "METRICS_ID_EMPTY");
        }

        if (shopId == Guid.Empty)
        {
            throw new SellerShopDomainException("店铺标识不可为空", "METRICS_SHOP_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new SellerShopDomainException("币种不可为空", "METRICS_CURRENCY_EMPTY");
        }

        return new ShopMetrics(metricsId)
        {
            ShopId = shopId,
            Date = date,
            OrderCount = 0,
            SalesAmount = Money.Zero(currency.Trim().ToUpperInvariant()),
            ProductCount = 0,
            AvgRating = 0m,
            RatingSum = 0m,
            RatingCount = 0,
            RefundCount = 0,
            RefundAmount = Money.Zero(currency.Trim().ToUpperInvariant())
        };
    }

    /// <summary>
    /// 记录一笔已完成订单，累加订单数与销售额。
    /// 幂等：同一订单重复记录由调用方按 EventId 去重，此处只做增量。
    /// </summary>
    /// <param name="salesAmount">订单销售额。</param>
    public void RecordOrder(Money salesAmount)
    {
        ArgumentNullException.ThrowIfNull(salesAmount);

        if (SalesAmount.Currency != salesAmount.Currency)
        {
            throw new SellerShopDomainException(
                $"币种不匹配: {SalesAmount.Currency} vs {salesAmount.Currency}", "METRICS_CURRENCY_MISMATCH");
        }

        OrderCount++;
        SalesAmount = SalesAmount.Add(salesAmount);
    }

    /// <summary>
    /// 更新当日商品数快照（由商品域事件驱动的商品数同步）。
    /// </summary>
    public void UpdateProductCount(int productCount)
    {
        if (productCount < 0)
        {
            throw new SellerShopDomainException("商品数不可为负", "METRICS_PRODUCT_COUNT_NEGATIVE");
        }

        ProductCount = productCount;
    }

    /// <summary>
    /// 记录一条评价，增量更新平均评分。
    /// 幂等：同一评价重复记录由调用方按 EventId 去重，此处只做增量。
    /// </summary>
    /// <param name="rating">评分（1-5）。</param>
    public void RecordRating(int rating)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new SellerShopDomainException(
                $"评分须为 {MinRating}-{MaxRating}", "METRICS_RATING_RANGE");
        }

        RatingCount++;
        RatingSum += rating;
        AvgRating = Math.Round(RatingSum / RatingCount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 记录一笔售后，累加售后数。
    /// </summary>
    public void RecordRefund()
    {
        RefundCount++;
    }

    /// <summary>
    /// 记录一笔售后退款金额，累加售后数与退款金额。
    /// 幂等：同一售后重复记录由调用方按 EventId 去重，此处只做增量。
    /// </summary>
    /// <param name="refundAmount">退款金额。</param>
    public void RecordRefund(Money refundAmount)
    {
        ArgumentNullException.ThrowIfNull(refundAmount);

        if (RefundAmount.Currency != refundAmount.Currency)
        {
            throw new SellerShopDomainException(
                $"币种不匹配: {RefundAmount.Currency} vs {refundAmount.Currency}", "METRICS_CURRENCY_MISMATCH");
        }

        RefundCount++;
        RefundAmount = RefundAmount.Add(refundAmount);
    }

    /// <summary>
    /// 记录一笔新订单创建，仅累加订单数（不累加销售额，销售额由完成订单驱动）。
    /// 幂等：同一订单重复记录由调用方按 EventId 去重，此处只做增量。
    /// </summary>
    public void RecordOrderCreation()
    {
        OrderCount++;
    }
}

using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Exceptions;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 店铺经营数据读取模型，聚合全量订单数、待处理订单数、已完成订单数、总收入等经营指标。
/// 与 Shop 一对一关联，由订单域事件驱动更新，不维护独立领域事件。
/// 非聚合根，但作为独立实体持久化以支持高效查询。
/// </summary>
public sealed class ShopDashboardData : AggregateRoot
{
    /// <summary>关联店铺标识（与 Shop.Id 一致）。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>累计订单总数（含已取消）。</summary>
    public int TotalOrders { get; private set; }

    /// <summary>待处理订单数（待发货/待支付）。</summary>
    public int PendingOrders { get; private set; }

    /// <summary>已确认订单数（已支付待发货），由订单支付成功事件驱动维护。</summary>
    public int ConfirmedOrders { get; private set; }

    /// <summary>已完成订单数。</summary>
    public int CompletedOrders { get; private set; }

    /// <summary>已取消订单数，由订单取消事件驱动维护。</summary>
    public int CancelledOrders { get; private set; }

    /// <summary>累计销售收入。</summary>
    public decimal TotalRevenue { get; private set; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; private set; } = "CNY";

    /// <summary>最后更新时间（UTC）。</summary>
    public DateTime LastUpdatedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ShopDashboardData() { }

    private ShopDashboardData(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，为指定店铺创建零值初始数据。
    /// </summary>
    public static ShopDashboardData Create(Guid shopId)
    {
        if (shopId == Guid.Empty)
        {
            throw new SellerShopDomainException("店铺标识不可为空", "DASHBOARD_SHOP_EMPTY");
        }

        return new ShopDashboardData(shopId)
        {
            ShopId = shopId,
            TotalOrders = 0,
            PendingOrders = 0,
            ConfirmedOrders = 0,
            CompletedOrders = 0,
            CancelledOrders = 0,
            TotalRevenue = 0m,
            Currency = "CNY",
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 订单创建时：总订单数 +1，待处理订单数 +1。
    /// </summary>
    public void OnOrderCreated()
    {
        TotalOrders++;
        PendingOrders++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 订单支付成功时：累计收入增加，已确认订单数 +1。
    /// </summary>
    public void OnOrderPaid(decimal amount)
    {
        if (amount <= 0)
        {
            throw new SellerShopDomainException("支付金额须大于 0", "DASHBOARD_AMOUNT_INVALID");
        }

        TotalRevenue += amount;
        ConfirmedOrders++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 订单取消时：待处理订单数 -1（不可为负），已取消订单数 +1。
    /// </summary>
    public void OnOrderCancelled()
    {
        if (PendingOrders > 0)
        {
            PendingOrders--;
        }

        CancelledOrders++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 订单完成时：待处理订单数 -1，已完成订单数 +1。
    /// </summary>
    public void OnOrderCompleted()
    {
        if (PendingOrders > 0)
        {
            PendingOrders--;
        }

        CompletedOrders++;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
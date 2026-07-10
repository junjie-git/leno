namespace Leno.Order.Infrastructure.ReadModels;

/// <summary>
/// 订单 ES 读模型文档，用于订单查询场景的 CQRS 读库。
/// 由 <see cref="OrderReadModelSyncConsumer"/> 在订单状态变更时同步索引到 Elasticsearch。
/// </summary>
public sealed class OrderReadModel
{
    /// <summary>订单标识。</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>订单编号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>买家账号标识。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>卖家（店铺）标识。</summary>
    public string SellerId { get; set; } = string.Empty;

    /// <summary>订单状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>订单类型。</summary>
    public string OrderType { get; set; } = string.Empty;

    /// <summary>商品总金额。</summary>
    public decimal ItemsAmount { get; set; }

    /// <summary>优惠总金额。</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>积分抵现金额。</summary>
    public decimal PointsOffsetAmount { get; set; }

    /// <summary>运费金额。</summary>
    public decimal FreightAmount { get; set; }

    /// <summary>订单总金额（实付）。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>支付时间（UTC），未支付为 null。</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>发货时间（UTC），未发货为 null。</summary>
    public DateTime? ShippedAt { get; set; }

    /// <summary>完成时间（UTC），未完成为 null。</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>取消时间（UTC），未取消为 null。</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>订单明细列表。</summary>
    public List<OrderItemReadModel> Items { get; set; } = new();

    /// <summary>
    /// 订单明细读模型。
    /// </summary>
    public sealed class OrderItemReadModel
    {
        /// <summary>SKU 标识。</summary>
        public string SkuId { get; set; } = string.Empty;

        /// <summary>商品名称。</summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>SKU 名称。</summary>
        public string SkuName { get; set; } = string.Empty;

        /// <summary>成交单价。</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>购买数量。</summary>
        public int Quantity { get; set; }

        /// <summary>小计金额。</summary>
        public decimal Subtotal { get; set; }
    }
}

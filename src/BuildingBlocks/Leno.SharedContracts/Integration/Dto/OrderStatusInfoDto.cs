namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 订单状态概要共享 DTO（D2.1 ACL 模式去重）。
/// 各 BC 的 OrderStatusProvider 防腐层统一返回此类型，消除 4 BC 重复定义。
/// 字段为各 BC 需求的超集，未使用的字段默认为 Guid.Empty / default。
/// </summary>
public sealed class OrderStatusInfoDto
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>订单状态码（int，与 <see cref="Enums.OrderStatusEnum"/> 值对齐）。</summary>
    public int Status { get; init; }

    /// <summary>订单状态文本（如 "Shipped"、"Completed"），可选。</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订单归属卖家标识，由订单域防腐层查询填充，防止客户端伪造。</summary>
    public Guid SellerId { get; init; }

    /// <summary>订单完成时间（UTC），未完成为 default。</summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>订单创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>订单行状态概要列表。</summary>
    public List<OrderItemStatusInfoDto> Items { get; init; } = [];
}

/// <summary>
/// 订单行状态概要共享 DTO。
/// </summary>
public sealed class OrderItemStatusInfoDto
{
    /// <summary>订单行标识。</summary>
    public Guid OrderLineId { get; init; }

    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>SPU 标识，由订单域防腐层查询填充，防止客户端伪造。</summary>
    public Guid SpuId { get; init; }

    /// <summary>订单行归属卖家标识（从订单级别复制到行级别）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>购买数量。</summary>
    public int Quantity { get; init; }

    /// <summary>售后状态码。</summary>
    public int AfterSalesStatus { get; init; }
}

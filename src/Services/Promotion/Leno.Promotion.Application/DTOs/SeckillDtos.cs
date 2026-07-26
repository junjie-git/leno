using Leno.Promotion.Domain.ValueObjects;

namespace Leno.Promotion.Application.DTOs;

/// <summary>
/// 秒杀活动 DTO。
/// </summary>
public sealed class SeckillActivityDto
{
    public Guid Id { get; init; }

    /// <summary>活动名称。</summary>
    public string Name { get; init; } = string.Empty;

    public Guid SpuId { get; init; }

    public Guid SkuId { get; init; }

    public decimal SeckillPrice { get; init; }

    public decimal OriginalPrice { get; init; }

    public int TotalStock { get; init; }

    /// <summary>DB 基线可用库存。展示层应优先使用 <see cref="AvailableStockRealtime"/>（Redis 实时值）。</summary>
    public int AvailableStock { get; init; }

    /// <summary>Redis 实时可用库存，由 <c>ISeckillStockService.GetAvailableAsync</c> 读取。</summary>
    public int AvailableStockRealtime { get; init; }

    public int LimitPerUser { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public SeckillStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 创建秒杀活动 DTO（运营端）。
/// </summary>
public sealed class CreateSeckillActivityDto
{
    /// <summary>活动名称，不可为空。</summary>
    public string Name { get; init; } = string.Empty;

    public Guid SpuId { get; init; }

    public Guid SkuId { get; init; }

    public decimal SeckillPrice { get; init; }

    public decimal OriginalPrice { get; init; }

    public int TotalStock { get; init; }

    public int LimitPerUser { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }
}

/// <summary>
/// 秒杀活动分页查询结果 DTO，包含当前页数据与总记录数。
/// </summary>
public sealed class SeckillListResultDto
{
    /// <summary>当前页的秒杀活动列表。</summary>
    public IReadOnlyList<SeckillActivityDto> Items { get; init; } = Array.Empty<SeckillActivityDto>();

    /// <summary>满足筛选条件的总记录数。</summary>
    public int Total { get; init; }
}

/// <summary>
/// 秒杀下单 DTO（买家端）。
/// </summary>
public sealed class SeckillPlaceOrderDto
{
    /// <summary>下单 SKU 标识，须与活动 SKU 一致。</summary>
    public Guid SkuId { get; init; }

    /// <summary>下单数量，须 &gt; 0 且 ≤ 活动限购。</summary>
    public int Quantity { get; init; }
}

/// <summary>
/// 秒杀下单结果 DTO。下单以异步模式处理，前端凭 <see cref="OrderId"/> 轮询订单域获取结果。
/// </summary>
public sealed class SeckillPlaceOrderResultDto
{
    /// <summary>应用层生成的订单标识，订单域消费 <c>SeckillOrderCreatedEvent</c> 后异步创建订单。</summary>
    public Guid OrderId { get; init; }

    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>秒杀单价。</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>下单时间（UTC）。</summary>
    public DateTime PlacedAt { get; init; }
}

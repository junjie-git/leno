using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Application.DTOs;

/// <summary>
/// 库存预占结果，返回给调用方（Order BC）。
/// </summary>
public sealed class StockReservationResult
{
    /// <summary>是否预占成功。</summary>
    public bool Success { get; init; }

    /// <summary>预占成功的 SKU 列表（含数量与卖家）。</summary>
    public IReadOnlyList<ReservedSkuItem> ReservedItems { get; init; } = Array.Empty<ReservedSkuItem>();

    /// <summary>失败原因（库存不足时填第一个失败 SKU）。</summary>
    public string? FailureReason { get; init; }

    /// <summary>预占过期时间（UTC）。</summary>
    public DateTime? ExpiresAt { get; init; }

    public static StockReservationResult Succeeded(IReadOnlyList<ReservedSkuItem> items, DateTime? expiresAt) =>
        new()
        {
            Success = true,
            ReservedItems = items,
            ExpiresAt = expiresAt
        };

    public static StockReservationResult Failed(string reason) =>
        new()
        {
            Success = false,
            FailureReason = reason
        };
}

/// <summary>
/// 库存预占命令 DTO（应用层接口输入）。
/// </summary>
public sealed record ReserveStockRequestDto(
    Guid OrderId,
    IReadOnlyList<ReserveStockItem> Items,
    Guid IdempotencyKey,
    TimeSpan? ReservationTtl = null);

/// <summary>
/// 秒杀库存预扣结果，返回给调用方（Promotion BC 秒杀下单流程）。
/// </summary>
public sealed class SeckillDeductResult
{
    /// <summary>结果码：0=成功，1=库存不足，2=超出用户限购上限。</summary>
    public int Code { get; init; }

    /// <summary>是否预扣成功（Code == 0）。</summary>
    public bool IsSuccess => Code == 0;

    /// <summary>失败原因描述（成功时为 null）。</summary>
    public string? FailureReason { get; init; }

    public static SeckillDeductResult Succeeded() => new() { Code = 0 };

    public static SeckillDeductResult Failed(int code, string reason) =>
        new() { Code = code, FailureReason = reason };
}

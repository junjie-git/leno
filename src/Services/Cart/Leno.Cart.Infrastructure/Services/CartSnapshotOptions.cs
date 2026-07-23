namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车 SKU 快照本地化配置（阶段三 3.11）。
/// 通过 <c>Cart:UseSkuSnapshot</c> feature flag 控制快照模式开关，
/// false 时走旧 gRPC/HTTP 实时调用，true 时走本地快照读取 + 后台异步刷新。
/// </summary>
public sealed class CartSnapshotOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "Cart";

    /// <summary>
    /// 是否启用 SKU 快照本地化模式。
    /// false（默认）：购物车读取路径走旧 <see cref="ICartPriceService"/> 实时跨进程调用。
    /// true：购物车读取路径优先读取本地快照，过期时回退实时调用并触发后台刷新。
    /// </summary>
    public bool UseSkuSnapshot { get; set; } = false;

    /// <summary>
    /// 快照过期阈值。超过此阈值的快照视为过期，读取时触发后台刷新并回退实时调用。
    /// 默认 5 分钟，与计划 §4.4 要求一致。
    /// </summary>
    public TimeSpan SnapshotMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 后台刷新队列最大并发度。
    /// 控制同时从商品域拉取快照的最大并发请求数，避免热门 SKU 集中过期时压垮商品域。
    /// 默认 3。
    /// </summary>
    public int RefreshConcurrency { get; set; } = 3;

    /// <summary>
    /// 后台刷新队列最大容量。
    /// 达到容量时新的刷新请求被丢弃（已有刷新在队列中会覆盖），避免无界队列内存溢出。
    /// 默认 1000。
    /// </summary>
    public int RefreshQueueCapacity { get; set; } = 1000;

    /// <summary>
    /// 后台刷新单次批量查询的 SKU 数量上限。
    /// 队列中累积多个 skuId 时合并为一次批量 ACL 调用，减少跨进程调用次数。
    /// 默认 50。
    /// </summary>
    public int RefreshBatchSize { get; set; } = 50;
}

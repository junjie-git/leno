namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车 SKU 快照本地化配置（阶段三 3.11）。
/// 快照模式的运行参数（过期阈值/刷新并发/队列容量/批量大小）；
/// 双轨下线 D4 后快照优先为唯一路径（开关已删除）。
/// </summary>
public sealed class CartSnapshotOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "Cart";

    // 双轨下线 D4（2026-09-23）：UseSkuSnapshot feature flag 已删除。
    // 快照优先读取无条件生效（SnapshotCartPriceService 始终走快照路径，缺失/过期回退实时调用并触发后台刷新）；
    // 原 flag 的 true/false 分支收敛为唯一路径。

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

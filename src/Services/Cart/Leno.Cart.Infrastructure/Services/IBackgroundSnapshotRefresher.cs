namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 后台 SKU 快照刷新器接口（阶段三 3.11）。
/// <para>
/// 购物车读取路径检测到快照过期或缺失时，通过此接口非阻塞地入队刷新请求，
/// 后台服务异步从商品域拉取最新快照并更新对应 CartItem。
/// </para>
/// <para>
/// 入队操作不阻塞调用方，不抛异常（队列满时静默丢弃，已有相同 skuId 的刷新在队列中会覆盖）。
/// 实际刷新由 <see cref="SkuSnapshotRefreshQueue"/> 后台服务执行。
/// </para>
/// </summary>
public interface IBackgroundSnapshotRefresher
{
    /// <summary>
    /// 入队一个 SKU 快照刷新请求。非阻塞，队列满时静默丢弃。
    /// </summary>
    /// <param name="skuId">需刷新的 SKU 标识。</param>
    void EnqueueRefresh(Guid skuId);

    /// <summary>
    /// 批量入队多个 SKU 快照刷新请求。非阻塞。
    /// </summary>
    /// <param name="skuIds">需刷新的 SKU 标识集合。</param>
    void EnqueueRefreshBatch(IEnumerable<Guid> skuIds);
}

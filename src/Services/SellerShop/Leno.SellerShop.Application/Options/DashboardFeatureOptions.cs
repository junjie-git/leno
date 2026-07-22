namespace Leno.SellerShop.Application.Options;

/// <summary>
/// 卖家工作台 Feature Flag 配置，控制 Dashboard 数据源切换（DB → ES 读模型）与双发对比。
/// 通过 appsettings.json 的 "Dashboard" 节绑定，由 DI 容器注册为 IOptions&lt;DashboardFeatureOptions&gt;。
/// </summary>
public sealed class DashboardFeatureOptions
{
    /// <summary>
    /// 是否使用 ES 读模型作为 Dashboard 数据源。
    /// false（默认）= 走 SellerDashboardAppService.GetDashboardAsync 读 DB；
    /// true = 走 ShopDashboardQueryHandler 读 ES 读模型。
    /// 灰度切换开关，依赖 P0-2/P0-3 修复后 ES 读模型数据完整。
    /// </summary>
    public bool UseReadModel { get; init; }

    /// <summary>
    /// 是否启用双发对比模式。
    /// true 时同时调用 DB 与 ES 两条路径，对比 TotalOrders/TotalRevenue 差异并记录 Warning 日志，
    /// 用于灰度切换前验证数据一致性。默认 false。
    /// </summary>
    public bool EnableComparison { get; init; }
}

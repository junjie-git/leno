namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 运营人员角色枚举，决定系统管理后台的权限层级。
/// </summary>
public enum OperatorRole
{
    /// <summary>超级管理员：拥有全部权限，可管理其他运营人员。</summary>
    SuperAdmin = 0,

    /// <summary>管理员：拥有大部分运营权限，不可管理超级管理员。</summary>
    Admin = 1,

    /// <summary>运营专员：仅拥有授权范围内的操作权限。</summary>
    Operator = 2
}

/// <summary>
/// 运营人员状态枚举。
/// </summary>
public enum OperatorStatus
{
    /// <summary>启用：可登录后台并执行操作。</summary>
    Active = 0,

    /// <summary>停用：不可登录，保留历史记录。</summary>
    Inactive = 1
}

/// <summary>
/// 系统配置状态枚举。
/// </summary>
public enum ConfigStatus
{
    /// <summary>停用：配置不参与读取。</summary>
    Disabled = 0,

    /// <summary>启用：配置可被各域读取。</summary>
    Enabled = 1
}

/// <summary>
/// 字典与字典项状态枚举。
/// </summary>
public enum DictionaryStatus
{
    /// <summary>停用：字典/字典项不在选项中展示。</summary>
    Disabled = 0,

    /// <summary>启用：字典/字典项可被引用。</summary>
    Enabled = 1
}

/// <summary>
/// 公告类型枚举。
/// </summary>
public enum AnnouncementType
{
    /// <summary>系统公告：平台级别通知。</summary>
    System = 0,

    /// <summary>维护公告：停机/维护时间通知。</summary>
    Maintenance = 1,

    /// <summary>促销公告：活动/优惠通知。</summary>
    Promotion = 2
}

/// <summary>
/// 公告状态枚举。
/// 状态流转：Draft → Published → Expired；Published → Draft（撤回）。
/// </summary>
public enum AnnouncementStatus
{
    /// <summary>草稿：未发布，仅内部可见。</summary>
    Draft = 0,

    /// <summary>已发布：对外可见。</summary>
    Published = 1,

    /// <summary>已过期：超过过期时间自动置位或手动置位。</summary>
    Expired = 2
}

/// <summary>
/// 特性开关状态枚举（用于查询过滤）。
/// </summary>
public enum FeatureFlagStatus
{
    /// <summary>停用：评估始终返回 false。</summary>
    Disabled = 0,

    /// <summary>启用：按策略评估。</summary>
    Enabled = 1
}

/// <summary>
/// 特性开关评估策略枚举。
/// </summary>
public enum FeatureFlagStrategy
{
    /// <summary>全局：对所有用户生效。</summary>
    Global = 0,

    /// <summary>用户白名单：仅白名单内用户生效。</summary>
    UserWhitelist = 1,

    /// <summary>按角色：按用户角色匹配生效。</summary>
    RoleBased = 2,

    /// <summary>按比例：按百分比灰度生效。</summary>
    Percentage = 3
}

/// <summary>
/// 定时任务状态枚举。
/// </summary>
public enum ScheduledTaskStatus
{
    /// <summary>停用：调度器不触发。</summary>
    Disabled = 0,

    /// <summary>启用：调度器按 Cron 触发。</summary>
    Enabled = 1
}

/// <summary>
/// 定时任务运行状态枚举。
/// </summary>
public enum TaskRunStatus
{
    /// <summary>从未运行：任务创建后的初始态。</summary>
    Never = 0,

    /// <summary>成功：上次运行成功完成。</summary>
    Success = 1,

    /// <summary>失败：上次运行异常终止。</summary>
    Failed = 2,

    /// <summary>运行中：任务正在执行。</summary>
    Running = 3
}

/// <summary>
/// 索引重建任务状态枚举。
/// 状态机：Created → Running → Completed/Failed → Retry → Running。
/// </summary>
public enum RebuildTaskStatus
{
    /// <summary>已创建：任务已创建但未开始执行。</summary>
    Created = 0,

    /// <summary>运行中：任务正在执行重建。</summary>
    Running = 1,

    /// <summary>已完成：任务成功完成。</summary>
    Completed = 2,

    /// <summary>失败：任务执行失败，可重试。</summary>
    Failed = 3
}

/// <summary>
/// 死信消息状态枚举。
/// 状态流转：Pending → Retried；Pending → Discarded。
/// </summary>
public enum DeadLetterStatus
{
    /// <summary>待处理：初始进入死信队列。</summary>
    Pending = 0,

    /// <summary>已重投：已重新发布回原主题。</summary>
    Retried = 1,

    /// <summary>已丢弃：经人工审核后确认丢弃。</summary>
    Discarded = 2
}

/// <summary>
/// 运营数据报表类型枚举。
/// </summary>
public enum ReportType
{
    /// <summary>订单GMV（成交总额）。</summary>
    OrderGmv = 0,

    /// <summary>支付成功率。</summary>
    PaymentSuccessRate = 1,

    /// <summary>积分发放量。</summary>
    PointsIssued = 2,

    /// <summary>通知送达率。</summary>
    NotificationDelivery = 3,

    /// <summary>售后量/退款金额。</summary>
    AfterSalesVolume = 4,

    /// <summary>店铺排行。</summary>
    ShopRanking = 5,

    /// <summary>转化率。</summary>
    ConversionRate = 6
}

/// <summary>
/// 模块健康状态枚举。
/// </summary>
public enum ModuleHealthStatus
{
    /// <summary>健康：模块正常运行。</summary>
    Healthy = 0,

    /// <summary>降级：模块部分功能不可用。</summary>
    Degraded = 1,

    /// <summary>不健康：模块不可用。</summary>
    Unhealthy = 2
}

/// <summary>
/// 统计对账状态枚举。
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>一致：SystemAdmin 聚合数据与各域统计数据一致。</summary>
    Consistent = 0,

    /// <summary>发现差异：存在不一致的指标，需人工审核或自动修正。</summary>
    DiscrepancyFound = 1,

    /// <summary>对账失败：对账过程发生异常，无法完成比对。</summary>
    Error = 2
}

/// <summary>
/// 公告目标受众枚举。
/// </summary>
public enum AnnouncementTargetAudience
{
    /// <summary>全部用户。</summary>
    All = 0,

    /// <summary>仅买家。</summary>
    Buyers = 1,

    /// <summary>仅卖家。</summary>
    Sellers = 2,

    /// <summary>仅运营人员。</summary>
    Operators = 3
}

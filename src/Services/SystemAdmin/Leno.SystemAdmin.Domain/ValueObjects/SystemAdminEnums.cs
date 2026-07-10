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

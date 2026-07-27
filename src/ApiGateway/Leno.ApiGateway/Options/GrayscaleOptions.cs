namespace Leno.ApiGateway.Options;

/// <summary>
/// 域拆分迁移阶段2：灰度路由配置选项，对应 appsettings.json 中 <c>Grayscale</c> 节。
/// <para>
/// 灰度策略（参考 Spec §3.3.2）：
/// <list type="bullet">
///   <item>灰度维度：按用户 ID hash（<c>hash(userId) % 100 &lt; Threshold</c>）</item>
///   <item>灰度梯度：5% → 25% → 50% → 100%，每档观察 ≥ 24 小时</item>
///   <item>内部端点：<c>internal/v1/*</c> 不走灰度，100% 切新域</item>
///   <item>回滚机制：feature flag 一键切回旧域，TTL &lt; 30 秒</item>
/// </list>
/// </para>
/// <para>
/// 使用 <see cref="IOptionsMonitor{TOptions}"/> 绑定以支持运行时热更新（appsettings.json 文件变更或 Consul KV 推送）。
/// </para>
/// </summary>
public sealed class GrayscaleOptions
{
    public const string SectionName = "Grayscale";

    /// <summary>
    /// 是否启用灰度分流。<c>false</c> 时所有请求走旧域（等同于 <see cref="RollbackToLegacy"/>）。
    /// 用于总开关，可在紧急情况下快速关闭灰度。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 灰度百分比阈值（0-100）。
    /// <para>
    /// 判定逻辑：<c>hash(userId) % 100 &lt; Threshold</c> → 走新域，否则走旧域。
    /// 灰度梯度建议：5 → 25 → 50 → 100。
    /// </para>
    /// <para>
    /// 边界值：
    /// <list type="bullet">
    ///   <item>0：无人走新域（等同回滚）</item>
    ///   <item>100：所有人走新域（全量切换）</item>
    /// </list>
    /// </para>
    /// </summary>
    public int Threshold { get; set; } = 5;

    /// <summary>
    /// 内部端点（<c>internal/v1/*</c>）是否 100% 切新域。
    /// <para>
    /// Spec §3.3.2：internal/v1/* 不走灰度，100% 切新域（内部调用方协调切换，可快速回滚）。
    /// 设为 <c>false</c> 时内部端点也走灰度判定。
    /// </para>
    /// </summary>
    public bool InternalSwitchAllToNew { get; set; } = true;

    /// <summary>
    /// 回滚开关。<c>true</c> 时所有请求强制走旧域，无视灰度判定。
    /// <para>
    /// 用于紧急回滚：发现新域异常时一键切回旧域，TTL &lt; 30 秒（通过 IOptionsMonitor 热更新）。
    /// 优先级高于 <see cref="Enabled"/> 和 <see cref="Threshold"/>。
    /// </para>
    /// </summary>
    public bool RollbackToLegacy { get; set; } = false;
}

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 特性开关评估器接口，按策略与上下文判定开关是否生效。
/// 实现位于应用/基础设施层，可结合本地缓存提升评估性能。
/// </summary>
public interface IFeatureFlagEvaluator
{
    /// <summary>
    /// 评估指定开关在给定上下文下是否生效。
    /// </summary>
    /// <param name="flagKey">开关键。</param>
    /// <param name="context">评估上下文（用户标识、角色等），不可为空。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>开关是否生效。</returns>
    Task<bool> EvaluateAsync(string flagKey, Dictionary<string, string> context, CancellationToken ct = default);
}

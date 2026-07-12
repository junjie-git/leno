using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 限流策略解析器领域服务，根据请求上下文匹配适用的限流规则。
/// 定义在领域层，由基础设施层实现具体的匹配逻辑。
/// </summary>
public interface IRateLimitPolicyResolver
{
    /// <summary>
    /// 根据 API 路径与请求上下文解析适用的限流规则列表。
    /// </summary>
    /// <param name="targetApi">目标 API 路径。</param>
    /// <param name="contextKey">限流上下文键（如用户 ID、IP 地址），可空。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>匹配的限流规则列表，按优先级排序。</returns>
    Task<List<RateLimitRule>> ResolveAsync(string targetApi, string? contextKey, CancellationToken ct = default);
}
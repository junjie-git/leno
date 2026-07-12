using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 限流策略解析器实现，根据 API 路径与请求上下文匹配适用的限流规则。
/// 优先级：精确匹配 > 前缀匹配 > 全局规则。
/// </summary>
public sealed class RateLimitPolicyResolver : IRateLimitPolicyResolver
{
    private readonly IRateLimitRuleRepository _repository;
    private readonly ILogger<RateLimitPolicyResolver> _logger;

    public RateLimitPolicyResolver(
        IRateLimitRuleRepository repository,
        ILogger<RateLimitPolicyResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<RateLimitRule>> ResolveAsync(string targetApi, string? contextKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targetApi);

        var enabledRules = await _repository.GetAllEnabledAsync(ct);

        var matched = enabledRules
            .Where(r => IsMatch(r, targetApi))
            .OrderByDescending(r => GetMatchPriority(r, targetApi))
            .ThenBy(r => r.Scope)
            .ToList();

        _logger.LogDebug(
            "限流策略解析完成 TargetApi={TargetApi} 匹配规则数={Count}",
            targetApi, matched.Count);

        return matched;
    }

    private static bool IsMatch(RateLimitRule rule, string targetApi)
    {
        var ruleApi = rule.TargetApi.TrimEnd('/');
        var requestApi = targetApi.TrimEnd('/');

        // 精确匹配
        if (string.Equals(ruleApi, requestApi, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 前缀匹配（通配符以 /* 结尾）
        if (ruleApi.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = ruleApi[..^2];
            return requestApi.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static int GetMatchPriority(RateLimitRule rule, string targetApi)
    {
        var ruleApi = rule.TargetApi.TrimEnd('/');
        var requestApi = targetApi.TrimEnd('/');

        // 精确匹配优先级最高
        if (string.Equals(ruleApi, requestApi, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        // 前缀匹配，路径越长越精确
        if (ruleApi.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            return ruleApi.Length;
        }

        return 0;
    }
}
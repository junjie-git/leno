using System.Text;
using System.Text.Json;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 特性开关评估器实现，优先读取 Redis 缓存，缓存缺失时回源仓储并回写。
/// 按策略（全局/用户白名单/角色/百分比）评估，任意异常均失败关闭返回 false。
/// </summary>
public sealed class FeatureFlagEvaluator : IFeatureFlagEvaluator
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FeatureFlagCache _cache;
    private readonly IFeatureFlagRepository _featureFlagRepository;
    private readonly ILogger<FeatureFlagEvaluator> _logger;

    public FeatureFlagEvaluator(
        FeatureFlagCache cache,
        IFeatureFlagRepository featureFlagRepository,
        ILogger<FeatureFlagEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(featureFlagRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _featureFlagRepository = featureFlagRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateAsync(string flagKey, Dictionary<string, string> context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var cached = await _cache.GetAsync(flagKey, ct);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedData = JsonSerializer.Deserialize<CachedFlagData>(cached, CacheJsonOptions);
                if (cachedData is null)
                {
                    return false;
                }

                return EvaluateCore(cachedData, context);
            }

            var flag = await _featureFlagRepository.GetByKeyAsync(flagKey, ct);
            if (flag is null || !flag.IsEnabled)
            {
                return false;
            }

            var data = new CachedFlagData
            {
                IsEnabled = flag.IsEnabled,
                Strategy = flag.Strategy,
                Rules = flag.Rules
            };
            var json = JsonSerializer.Serialize(data, CacheJsonOptions);
            await _cache.SetAsync(flagKey, json, ct);
            return EvaluateCore(data, context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "评估特性开关失败 FlagKey={FlagKey}", flagKey);
            return false;
        }
    }

    private static bool EvaluateCore(CachedFlagData data, Dictionary<string, string> context)
    {
        return data.Strategy switch
        {
            FeatureFlagStrategy.Global => data.IsEnabled,
            FeatureFlagStrategy.UserWhitelist => EvaluateWhitelist(data.Rules, context),
            FeatureFlagStrategy.RoleBased => EvaluateRole(data.Rules, context),
            FeatureFlagStrategy.Percentage => EvaluatePercentage(data.Rules, context),
            _ => false
        };
    }

    private static bool EvaluateWhitelist(string? rules, Dictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(rules))
        {
            return false;
        }

        if (!context.TryGetValue("userId", out var userId) || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var whitelist = JsonSerializer.Deserialize<List<string>>(rules);
        return whitelist is not null && whitelist.Contains(userId);
    }

    private static bool EvaluateRole(string? rules, Dictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(rules))
        {
            return false;
        }

        if (!context.TryGetValue("role", out var role) || string.IsNullOrEmpty(role))
        {
            return false;
        }

        var roles = JsonSerializer.Deserialize<List<string>>(rules);
        return roles is not null && roles.Contains(role);
    }

    private static bool EvaluatePercentage(string? rules, Dictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(rules))
        {
            return false;
        }

        if (!context.TryGetValue("userId", out var userId) || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var percentage = JsonSerializer.Deserialize<int>(rules);
        if (percentage <= 0)
        {
            return false;
        }

        var hash = ComputeFnv1aHash(userId);
        return hash % 100u < (uint)percentage;
    }

    /// <summary>对用户标识计算 FNV-1a 32 位哈希，保证跨进程稳定，用于百分比灰度。</summary>
    private static uint ComputeFnv1aHash(string value)
    {
        uint hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return hash;
    }

    private sealed class CachedFlagData
    {
        public bool IsEnabled { get; set; }

        public FeatureFlagStrategy Strategy { get; set; }

        public string? Rules { get; set; }
    }
}

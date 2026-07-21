using Microsoft.Extensions.Caching.Memory;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// ErrorCode 到 HTTP 状态码的映射中心。
/// 采用混合方案：优先查显式注册表，未命中按 ErrorCode 后缀约定推断，再未命中返回 400。
/// 各 BC 启动时通过 <see cref="Register"/> 注册不遵循后缀约定的特殊 ErrorCode。
/// </summary>
public static class ErrorCodeMapping
{
    /// <summary>
    /// 显式注册表，使用 <see cref="MemoryCache"/> 替代 <c>ConcurrentDictionary</c>，
    /// 通过 <see cref="MemoryCacheOptions.SizeLimit"/> 限制最大条目数（10,000），
    /// 防止长期运行后动态注册导致的无限增长。
    /// 启动期注册使用 <see cref="CacheItemPriority.NeverRemove"/> 优先级，
    /// 保证不被自动驱逐；显式 <see cref="Reset"/> 仍可清空全部条目（用于测试隔离）。
    /// </summary>
    private static readonly MemoryCache _explicit = new(new MemoryCacheOptions
    {
        SizeLimit = 10_000
    });

    // 后缀约定规则（按优先级排序，先匹配先返回）
    private static readonly (string Suffix, int StatusCode)[] _suffixRules =
    [
        ("_NOT_FOUND", 404),
        ("_ALREADY_", 409),
        ("_EXISTS", 409),
        ("_CONFLICT", 409),
        ("_FORBIDDEN", 403),
        ("_UNAVAILABLE", 503),
        ("_FAILED", 502),
        ("_MISSING", 500),
        ("_EXPIRED", 401),
        ("_REQUIRED", 401),
    ];

    /// <summary>
    /// 显式注册 ErrorCode 到 HTTP 状态码映射（用于不遵循后缀约定的特殊 ErrorCode）。
    /// </summary>
    public static void Register(string errorCode, int statusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        _explicit.Set(errorCode, statusCode, new MemoryCacheEntryOptions
        {
            SizeValue = 1,
            Priority = CacheItemPriority.NeverRemove
        });
    }

    /// <summary>
    /// 批量注册。
    /// </summary>
    public static void RegisterAll(params (string ErrorCode, int StatusCode)[] entries)
    {
        foreach (var (code, status) in entries)
        {
            Register(code, status);
        }
    }

    /// <summary>
    /// 查询 ErrorCode 对应的 HTTP 状态码。
    /// 优先显式表 → 后缀规则 → 默认 400。
    /// </summary>
    public static int GetStatusCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return 400;
        }

        if (_explicit.TryGetValue(errorCode, out var cached) && cached is int explicitCode)
        {
            return explicitCode;
        }

        foreach (var (suffix, statusCode) in _suffixRules)
        {
            // 修复 T33：Contains 子串匹配会产生误匹配（如 "NOT_FOUND_USER" 误匹配 "_NOT_FOUND"）。
            // 后缀规则以 '_' 结尾的（如 "_ALREADY_"）是中间标记而非真正后缀，
            // 按 '_' 分割后做 token 精确匹配；其余使用 EndsWith 精确后缀匹配。
            if (suffix.EndsWith('_'))
            {
                var token = suffix.Trim('_');
                if (errorCode.Split('_').Contains(token, StringComparer.Ordinal))
                {
                    return statusCode;
                }
            }
            else if (errorCode.EndsWith(suffix, StringComparison.Ordinal))
            {
                return statusCode;
            }
        }

        return 400;
    }

    /// <summary>
    /// 重置显式注册表（仅用于单元测试隔离）。
    /// 使用 <see cref="MemoryCache.Compact"/> 清空全部条目（包括 NeverRemove 优先级）。
    /// </summary>
    internal static void Reset() => _explicit.Compact(1.0);
}

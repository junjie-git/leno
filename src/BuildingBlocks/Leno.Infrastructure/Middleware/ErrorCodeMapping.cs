using System.Collections.Concurrent;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// ErrorCode 到 HTTP 状态码的映射中心。
/// 采用混合方案：优先查显式注册表，未命中按 ErrorCode 后缀约定推断，再未命中返回 400。
/// 各 BC 启动时通过 <see cref="Register"/> 注册不遵循后缀约定的特殊 ErrorCode。
/// </summary>
public static class ErrorCodeMapping
{
    private static readonly ConcurrentDictionary<string, int> _explicit = new(StringComparer.Ordinal);

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
        _explicit[errorCode] = statusCode;
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

        if (_explicit.TryGetValue(errorCode, out var explicitCode))
        {
            return explicitCode;
        }

        foreach (var (suffix, statusCode) in _suffixRules)
        {
            if (errorCode.Contains(suffix, StringComparison.Ordinal))
            {
                return statusCode;
            }
        }

        return 400;
    }

    /// <summary>
    /// 重置显式注册表（仅用于单元测试隔离）。
    /// </summary>
    internal static void Reset() => _explicit.Clear();
}

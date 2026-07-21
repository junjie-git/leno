using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 刷新令牌存储的进程内默认实现，仅供开发与单实例部署使用。
/// 生产环境必须使用 <see cref="RedisRefreshTokenStore"/> 以保证多实例间共享与持久化。
/// 注册为单例以在请求间共享令牌表。
/// 使用 <see cref="MemoryCache"/> 的 <c>AbsoluteExpirationRelativeToNow</c> 自动驱逐过期令牌，
/// 避免 <c>ConcurrentDictionary</c> 实现下未刷新令牌永不清理导致的内存泄漏。
/// </summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly JwtTokenGenerator _generator;
    private readonly MemoryCache _store;
    private readonly ILogger<InMemoryRefreshTokenStore> _logger;

    public InMemoryRefreshTokenStore(JwtTokenGenerator generator, ILogger<InMemoryRefreshTokenStore> logger)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(logger);
        _generator = generator;
        _logger = logger;
        _store = new MemoryCache(new MemoryCacheOptions());
        _logger.LogWarning(
            "InMemoryRefreshTokenStore 已启用：刷新令牌仅存储于当前进程内存，水平扩容或进程重启后失效。" +
            "生产环境必须配置 RefreshToken:Provider=Redis 与 Redis:Connection。");
    }

    /// <inheritdoc />
    public Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var token = JwtTokenGenerator.GenerateRefreshToken();
        _store.Set(
            token,
            userId,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _generator.RefreshTokenExpiry,
                Priority = CacheItemPriority.Normal
            });
        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task<Guid?> ValidateAndRotateAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Task.FromResult<Guid?>(null);
        }

        // MemoryCache 在 TryGetValue 时会惰性驱逐已过期条目（AbsoluteExpiration），过期则返回 false。
        if (_store.TryGetValue(refreshToken, out var userIdObj) && userIdObj is Guid userId)
        {
            _store.Remove(refreshToken);
            return Task.FromResult<Guid?>(userId);
        }

        return Task.FromResult<Guid?>(null);
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        // MemoryCache 枚举当前驻留的键值对快照，按 userId 过滤后逐个移除。
        var keysToRemove = new List<object>();
        foreach (var kvp in _store)
        {
            if (kvp.Value is Guid uid && uid == userId)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _store.Remove(key);
        }

        return Task.CompletedTask;
    }
}

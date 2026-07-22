using System.Collections.Concurrent;
using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<string, Guid> _tokens;
    private readonly ILogger<InMemoryRefreshTokenStore> _logger;

    public InMemoryRefreshTokenStore(JwtTokenGenerator generator, ILogger<InMemoryRefreshTokenStore> logger)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(logger);
        _generator = generator;
        _logger = logger;
        _store = new MemoryCache(new MemoryCacheOptions());
        _tokens = new ConcurrentDictionary<string, Guid>();
        _logger.LogWarning(
            "InMemoryRefreshTokenStore 已启用：刷新令牌仅存储于当前进程内存，水平扩容或进程重启后失效。" +
            "生产环境必须配置 RefreshToken:Provider=Redis 与 Redis:Connection。");
    }

    /// <inheritdoc />
    public Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var token = JwtTokenGenerator.GenerateRefreshToken();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _generator.RefreshTokenExpiry,
            Priority = CacheItemPriority.Normal
        };
        // 注册驱逐回调：MemoryCache 过期或被显式移除时同步清理键索引，避免索引无限增长。
        options.RegisterPostEvictionCallback(OnEntryEvicted);
        _store.Set(token, userId, options);
        _tokens[token] = userId;
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
            _tokens.TryRemove(refreshToken, out _);
            return Task.FromResult<Guid?>(userId);
        }

        return Task.FromResult<Guid?>(null);
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        // MemoryCache 不可枚举，因此通过 ConcurrentDictionary 维护已签发令牌的键索引。
        // 遍历索引快照，按 userId 过滤；对仍存活于 MemoryCache 的条目执行移除，
        // 过期条目已由 AbsoluteExpiration 驱逐并经 OnEntryEvicted 回调清理索引，此处 TryGetValue 二次确认避免误判。
        foreach (var kvp in _tokens)
        {
            if (kvp.Value != userId)
            {
                continue;
            }

            if (_store.TryGetValue(kvp.Key, out var boundUserId) && boundUserId is Guid uid && uid == userId)
            {
                _store.Remove(kvp.Key);
            }

            _tokens.TryRemove(kvp.Key, out _);
        }

        return Task.CompletedTask;
    }

    private void OnEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string tokenKey)
        {
            _tokens.TryRemove(tokenKey, out _);
        }
    }
}

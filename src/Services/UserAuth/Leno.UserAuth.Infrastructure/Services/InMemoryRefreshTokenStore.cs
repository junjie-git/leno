using System.Collections.Concurrent;
using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 刷新令牌存储的进程内默认实现，供开发与单实例部署使用。
/// 生产环境应替换为基于 Redis 或数据库的实现以保证多实例间共享与持久化。
/// 注册为单例以在请求间共享令牌表。
/// </summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly JwtTokenGenerator _generator;
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _store = new();

    public InMemoryRefreshTokenStore(JwtTokenGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    /// <inheritdoc />
    public Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var token = JwtTokenGenerator.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow + _generator.RefreshTokenExpiry;
        _store[token] = new RefreshTokenEntry(userId, expiresAt);
        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task<Guid?> ValidateAndRotateAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Task.FromResult<Guid?>(null);
        }

        if (_store.TryRemove(refreshToken, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<Guid?>(entry.UserId);
        }

        return Task.FromResult<Guid?>(null);
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        var keysToRemove = _store
            .Where(kvp => kvp.Value.UserId == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private sealed record RefreshTokenEntry(Guid UserId, DateTime ExpiresAt);
}

using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure.Repositories;

/// <summary>
/// 刷新令牌仓储 EF Core 实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 从 UserAuth BC 的 RedisRefreshTokenStore 演化而来，承载完整聚合根持久化与审计查询能力。
/// 单次签发对应一条记录，轮换时旧令牌标记 Revoked 而非物理删除，保留审计轨迹。
/// </summary>
public sealed class EfCoreRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _context;

    public EfCoreRefreshTokenRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.RefreshTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult<RefreshToken?>(null);
        }

        return _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<RefreshToken>();
        }

        var now = DateTime.UtcNow;
        var items = await _context.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.IssuedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items;
    }

    /// <inheritdoc />
    public async Task RevokeAllByUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("撤销原因不可为空", nameof(reason));
        }

        // 仅查询未撤销的令牌，避免对已撤销令牌重复写入触发不必要的 UPDATE
        var now = DateTime.UtcNow;
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var token in activeTokens)
        {
            token.Revoke(reason);
        }
    }

    /// <inheritdoc />
    public Task AddAsync(RefreshToken aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.RefreshTokens.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(RefreshToken aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.RefreshTokens.Attach(aggregate);
        }
        _context.Entry(aggregate).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(RefreshToken aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.RefreshTokens.Remove(aggregate);
        return Task.CompletedTask;
    }
}

using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure.Repositories;

/// <summary>
/// 双因子认证会话仓储 EF Core 实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 从 UserAuth BC 的 RedisTwoFactorTempTokenStore 演化而来，承载完整聚合根持久化与会话生命周期管理。
/// </summary>
public sealed class EfCoreTwoFactorSessionRepository : ITwoFactorSessionRepository
{
    private readonly IdentityDbContext _context;

    public EfCoreTwoFactorSessionRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<TwoFactorSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.TwoFactorSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<TwoFactorSession?> GetByTempTokenAsync(string tempToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tempToken))
        {
            return Task.FromResult<TwoFactorSession?>(null);
        }

        return _context.TwoFactorSessions.FirstOrDefaultAsync(s => s.TempToken == tempToken, ct);
    }

    /// <inheritdoc />
    public async Task CleanupExpiredByUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var now = DateTime.UtcNow;
        // 物理删除已过期或已结束的会话，避免同一用户累积过多会话记录
        var staleSessions = await _context.TwoFactorSessions
            .Where(s => s.UserId == userId &&
                        (s.ExpiresAt <= now || s.Status != TwoFactorSessionStatus.Pending))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (staleSessions.Count > 0)
        {
            _context.TwoFactorSessions.RemoveRange(staleSessions);
        }
    }

    /// <inheritdoc />
    public Task AddAsync(TwoFactorSession aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.TwoFactorSessions.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(TwoFactorSession aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.TwoFactorSessions.Attach(aggregate);
        }
        _context.Entry(aggregate).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(TwoFactorSession aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.TwoFactorSessions.Remove(aggregate);
        return Task.CompletedTask;
    }
}

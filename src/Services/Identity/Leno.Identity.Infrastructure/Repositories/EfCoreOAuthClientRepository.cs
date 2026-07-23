using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure.Repositories;

/// <summary>
/// OAuth2 客户端配置仓储 EF Core 实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 从 UserAuth BC 的 EfCoreOAuthClientRepository 迁入，逻辑保持一致。
/// </summary>
public sealed class EfCoreOAuthClientRepository : IOAuthClientRepository
{
    private readonly IdentityDbContext _context;

    public EfCoreOAuthClientRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.OAuthClients.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<OAuthClient?> GetByProviderAsync(string provider, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return Task.FromResult<OAuthClient?>(null);
        }

        var normalized = provider.Trim().ToLowerInvariant();
        return _context.OAuthClients.FirstOrDefaultAsync(c => c.Provider == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OAuthClient>> GetEnabledAsync(CancellationToken ct = default)
    {
        var items = await _context.OAuthClients
            .AsNoTracking()
            .Where(c => c.Enabled)
            .OrderBy(c => c.Provider)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items;
    }

    /// <inheritdoc />
    public Task AddAsync(OAuthClient aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.OAuthClients.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(OAuthClient aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.OAuthClients.Attach(aggregate);
        }
        _context.Entry(aggregate).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(OAuthClient aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.OAuthClients.Remove(aggregate);
        return Task.CompletedTask;
    }
}

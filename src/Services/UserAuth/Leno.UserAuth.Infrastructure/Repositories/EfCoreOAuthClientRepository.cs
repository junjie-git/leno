using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// OAuth 客户端配置仓储 EF Core 实现。
/// </summary>
public sealed class EfCoreOAuthClientRepository : IOAuthClientRepository
{
    private readonly UserAuthDbContext _context;

    public EfCoreOAuthClientRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<OAuthClient?> GetByProviderAsync(string provider, CancellationToken ct = default)
    {
        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return _context.OAuthClients.FirstOrDefaultAsync(o => o.Provider == normalized, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OAuthClient>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.OAuthClients.AsNoTracking().ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task AddAsync(OAuthClient client, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        _context.OAuthClients.Add(client);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(OAuthClient client, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (_context.Entry(client).State == EntityState.Detached)
        {
            _context.OAuthClients.Attach(client);
        }
        return Task.CompletedTask;
    }
}
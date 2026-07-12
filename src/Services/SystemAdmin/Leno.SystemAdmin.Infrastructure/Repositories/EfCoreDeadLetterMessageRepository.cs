using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 死信消息 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreDeadLetterMessageRepository : IDeadLetterMessageRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreDeadLetterMessageRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<DeadLetterMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.DeadLetterMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public async Task<DeadLetterMessage?> GetByOriginalMessageIdAsync(string originalMessageId, CancellationToken ct = default)
        => await _context.DeadLetterMessages.FirstOrDefaultAsync(m => m.OriginalMessageId == originalMessageId, ct);

    /// <inheritdoc />
    public async Task<List<DeadLetterMessage>> QueryAsync(string? sourceContext, DeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.DeadLetterMessages.AsQueryable(), sourceContext, status);
        return await query
            .OrderByDescending(m => m.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? sourceContext, DeadLetterStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.DeadLetterMessages.AsQueryable(), sourceContext, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(DeadLetterMessage aggregate, CancellationToken ct = default)
        => await _context.DeadLetterMessages.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(DeadLetterMessage aggregate, CancellationToken ct = default)
    {
        _context.DeadLetterMessages.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(DeadLetterMessage aggregate, CancellationToken ct = default)
    {
        _context.DeadLetterMessages.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<DeadLetterMessage> ApplyFilters(
        IQueryable<DeadLetterMessage> query,
        string? sourceContext,
        DeadLetterStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(sourceContext))
        {
            query = query.Where(m => m.SourceContext == sourceContext.Trim());
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        return query;
    }
}
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 对账记录 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreReconciliationRecordRepository : IReconciliationRecordRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreReconciliationRecordRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<ReconciliationRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ReconciliationRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(ReconciliationRecord aggregate, CancellationToken ct = default)
        => await _context.ReconciliationRecords.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(ReconciliationRecord aggregate, CancellationToken ct = default)
    {
        _context.ReconciliationRecords.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ReconciliationRecord aggregate, CancellationToken ct = default)
    {
        _context.ReconciliationRecords.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ReconciliationRecord?> GetLatestAsync(CancellationToken ct = default)
    {
        return await _context.ReconciliationRecords
            .OrderByDescending(r => r.ReconciledAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ReconciliationRecord?> GetLatestByTypeAsync(ReportType reportType, CancellationToken ct = default)
    {
        return await _context.ReconciliationRecords
            .Where(r => r.ReportType == reportType)
            .OrderByDescending(r => r.ReconciledAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ReconciliationRecord>> GetByPeriodAsync(
        ReportType reportType,
        DateTime start,
        DateTime endTime,
        CancellationToken ct = default)
    {
        return await _context.ReconciliationRecords
            .Where(r => r.ReportType == reportType)
            .Where(r => r.ReconciledAt >= start && r.ReconciledAt <= endTime)
            .OrderByDescending(r => r.ReconciledAt)
            .ToListAsync(ct);
    }
}
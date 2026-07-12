using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 运营数据看板报表 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreDashboardReportRepository : IDashboardReportRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreDashboardReportRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<DashboardReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.DashboardReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(DashboardReport aggregate, CancellationToken ct = default)
        => await _context.DashboardReports.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(DashboardReport aggregate, CancellationToken ct = default)
    {
        _context.DashboardReports.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(DashboardReport aggregate, CancellationToken ct = default)
    {
        _context.DashboardReports.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DashboardReport?> GetLatestAsync(ReportType reportType, CancellationToken ct = default)
    {
        return await _context.DashboardReports
            .Where(r => r.ReportType == reportType)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<DashboardReport>> GetByPeriodAsync(
        ReportType reportType,
        DateTime start,
        DateTime endTime,
        CancellationToken ct = default)
    {
        return await _context.DashboardReports
            .Where(r => r.ReportType == reportType)
            .Where(r => r.GeneratedAt >= start && r.GeneratedAt <= endTime)
            .OrderByDescending(r => r.GeneratedAt)
            .ToListAsync(ct);
    }
}
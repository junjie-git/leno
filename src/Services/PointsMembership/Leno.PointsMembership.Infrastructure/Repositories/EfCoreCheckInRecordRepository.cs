using Leno.PointsMembership.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using CheckInRecordAggregate = Leno.PointsMembership.Domain.Aggregates.CheckInRecord;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 签到记录 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreCheckInRecordRepository : ICheckInRecordRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreCheckInRecordRepository(PointsMembershipDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CheckInRecordAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CheckInRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<CheckInRecordAggregate?> GetByUserIdAndDateAsync(
        Guid userId,
        DateOnly checkInDate,
        CancellationToken ct = default)
        => await _context.CheckInRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.CheckInDate == checkInDate, ct);

    /// <inheritdoc />
    public async Task<CheckInRecordAggregate?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.CheckInRecords
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CheckInDate)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(CheckInRecordAggregate aggregate, CancellationToken ct = default)
        => await _context.CheckInRecords.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(CheckInRecordAggregate aggregate, CancellationToken ct = default)
    {
        _context.CheckInRecords.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(CheckInRecordAggregate aggregate, CancellationToken ct = default)
    {
        _context.CheckInRecords.Remove(aggregate);
        return Task.CompletedTask;
    }
}

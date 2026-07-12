using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Payment.Infrastructure.Repositories;

/// <summary>
/// 支付渠道配置 EF Core 仓储实现。
/// </summary>
public sealed class EfCorePaymentChannelConfigRepository : IPaymentChannelConfigRepository
{
    private readonly PaymentDbContext _context;

    public EfCorePaymentChannelConfigRepository(PaymentDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PaymentChannelConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PaymentChannelConfigs
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<PaymentChannelConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.PaymentChannelConfigs
            .OrderBy(c => c.Channel)
            .ThenBy(c => c.ConfigName)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PaymentChannelConfig>> GetByChannelAsync(PaymentChannel channel, CancellationToken ct = default)
        => await _context.PaymentChannelConfigs
            .Where(c => c.Channel == channel)
            .OrderBy(c => c.ConfigName)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(PaymentChannelConfig aggregate, CancellationToken ct = default)
        => await _context.PaymentChannelConfigs.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PaymentChannelConfig aggregate, CancellationToken ct = default)
    {
        _context.PaymentChannelConfigs.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PaymentChannelConfig aggregate, CancellationToken ct = default)
    {
        _context.PaymentChannelConfigs.Remove(aggregate);
        return Task.CompletedTask;
    }
}
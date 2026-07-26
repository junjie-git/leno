using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Repositories;
using Leno.UserCenter.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserCenter.Infrastructure.Repositories;

/// <summary>
/// 收货地址仓储 EF Core 实现。
/// 地址软删除基于 <see cref="AddressStatus"/> 枚举（非 <c>ISoftDeletable</c>），故查询显式过滤 <see cref="AddressStatus.Active"/>。
/// 单实体查询带跟踪以支持默认地址切换的读改写编排。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class EfCoreAddressRepository : IAddressRepository
{
    private readonly UserCenterDbContext _context;

    public EfCoreAddressRepository(UserCenterDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Address?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Addresses.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Address>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var addresses = await _context.Addresses
            .Where(a => a.UserId == userId && a.Status == AddressStatus.Active)
            .ToListAsync(ct);
        return addresses;
    }

    /// <inheritdoc />
    public Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Addresses
            .AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.Status == AddressStatus.Active, ct);

    /// <inheritdoc />
    public Task AddAsync(Address address, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        _context.Addresses.Add(address);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Address address, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (_context.Entry(address).State == EntityState.Detached)
        {
            _context.Addresses.Attach(address);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Address address, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        _context.Addresses.Remove(address);
        return Task.CompletedTask;
    }
}

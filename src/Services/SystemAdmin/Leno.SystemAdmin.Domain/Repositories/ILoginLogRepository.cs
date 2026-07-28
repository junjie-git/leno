using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>登录日志仓储接口（仅追加，无 Update/Delete）。</summary>
public interface ILoginLogRepository
{
    Task<LoginLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<LoginLog> Items, int Total)> QueryAsync(LoginLogQuery query, CancellationToken ct = default);
    Task AddAsync(LoginLog log, CancellationToken ct = default);
    IAsyncEnumerable<LoginLog> StreamAsync(LoginLogQuery query, int limit, CancellationToken ct = default);
}

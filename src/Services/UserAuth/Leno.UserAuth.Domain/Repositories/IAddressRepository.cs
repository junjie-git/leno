using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Aggregates;

namespace Leno.UserAuth.Domain.Repositories;

/// <summary>
/// 收货地址仓储接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface IAddressRepository : IRepository<Address>
{
    /// <summary>查询用户下所有 Active 地址（默认地址优先）。</summary>
    Task<IReadOnlyList<Address>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>统计用户下 Active 地址数量（用于上限校验，INV-05：每用户最多 20 条）。</summary>
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
}

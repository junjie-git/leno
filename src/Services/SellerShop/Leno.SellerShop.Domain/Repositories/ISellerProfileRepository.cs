using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Aggregates;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 卖家档案仓储接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface ISellerProfileRepository : IRepository<SellerProfile>
{
    /// <summary>按卖家账号标识（用户域 UserId）查询卖家档案。</summary>
    Task<SellerProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}

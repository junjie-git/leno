using Leno.SharedKernel.Abstractions;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 店铺仓储接口，定义在领域层，由基础设施层实现。
/// 查询方法返回聚合根，写操作不立即持久化，由工作单元统一提交。
/// </summary>
public interface IShopRepository : IRepository<Shop>
{
    /// <summary>按卖家账号标识查询店铺（一卖家一店铺）。</summary>
    Task<Shop?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询店铺列表，支持按状态过滤与关键词模糊匹配。
    /// </summary>
    /// <param name="status">店铺状态过滤，可空表示不限。</param>
    /// <param name="keyword">店铺名称关键词，可空。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数，最大 100。</param>
    Task<(IReadOnlyList<Shop> Items, int Total)> QueryAsync(
        ShopStatus? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}

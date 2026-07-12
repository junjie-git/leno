using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;

namespace Leno.Product.Application;

/// <summary>
/// 商品发布与管理应用服务，编排卖家创建草稿、提交审核、运营审核上下架与编辑用例。
/// 事务边界由工作单元统一控制；状态流转产生的集成事件经发件箱发布。
/// </summary>
public interface ISPUAppService
{
    /// <summary>卖家创建草稿商品。</summary>
    Task<ProductDto> CreateAsync(Guid sellerId, Guid shopId, CreateProductDto dto, CancellationToken ct = default);

    /// <summary>卖家更新商品基础信息。</summary>
    Task<ProductDto> UpdateAsync(Guid sellerId, Guid spuId, UpdateProductDto dto, CancellationToken ct = default);

    /// <summary>卖家为商品新增 SKU。</summary>
    Task<ProductDto> AddSkuAsync(Guid sellerId, Guid spuId, AddSkuDto dto, CancellationToken ct = default);

    /// <summary>卖家提交审核。</summary>
    Task SubmitForReviewAsync(Guid sellerId, Guid spuId, CancellationToken ct = default);

    /// <summary>卖家下架商品。</summary>
    Task TakeDownAsync(Guid sellerId, Guid spuId, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>卖家重新上架商品（进入待审核）。</summary>
    Task RepublishAsync(Guid sellerId, Guid spuId, CancellationToken ct = default);

    /// <summary>查询商品详情（含 SKU）。</summary>
    Task<ProductDto> GetByIdAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>分页查询商品列表。</summary>
    Task<PageResult<ProductDto>> QueryProductsAsync(ProductQueryDto query, CancellationToken ct = default);

    /// <summary>运营审核通过上架。</summary>
    Task ApproveAsync(Guid spuId, Guid reviewedBy, CancellationToken ct = default);

    /// <summary>运营审核驳回。</summary>
    Task RejectAsync(Guid spuId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>调整 SKU 价格，记录价格变更历史。</summary>
    Task AdjustPriceAsync(Guid spuId, Guid skuId, AdjustPriceDto dto, string changedBy, CancellationToken ct = default);

    /// <summary>查询商品价格变更历史。</summary>
    Task<IReadOnlyList<PriceChangeRecordDto>> GetPriceHistoryAsync(Guid spuId, Guid? skuId = null, CancellationToken ct = default);

    /// <summary>调整 SKU 库存（delta 方式），记录操作日志并发布事件。</summary>
    Task UpdateStockAsync(Guid spuId, Guid skuId, UpdateStockDto dto, string operatorId, CancellationToken ct = default);
}

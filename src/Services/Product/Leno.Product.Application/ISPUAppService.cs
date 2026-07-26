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
    [Obsolete("请使用 IQueryHandler<ProductDetailQuery, ProductDetailResult?>，将在 2026-08-01 移除")]
    Task<ProductDto> GetByIdAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>分页查询商品列表。</summary>
    [Obsolete("请使用 IQueryHandler<ProductSearchQuery, ProductSearchResult>，将在 2026-08-01 移除")]
    Task<PageResult<ProductDto>> QueryProductsAsync(ProductQueryDto query, CancellationToken ct = default);

    /// <summary>运营审核通过上架。</summary>
    Task ApproveAsync(Guid spuId, Guid reviewedBy, CancellationToken ct = default);

    /// <summary>运营审核驳回。</summary>
    Task RejectAsync(Guid spuId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default);

    /// <summary>
    /// 批量审核通过上架。遍历 <paramref name="ids"/> 逐个调用 <see cref="ApproveAsync"/>，
    /// 单个失败（商品不存在、状态不可流转等）捕获并记录到 <see cref="BatchOperationResultDto.Failures"/>，
    /// 不阻塞整批；成功的标识收集到 <see cref="BatchOperationResultDto.SucceededIds"/>。
    /// </summary>
    /// <param name="ids">商品标识列表。</param>
    /// <param name="reviewedBy">审核人标识，透传给单个 <see cref="ApproveAsync"/> 用于审核历史记录。</param>
    /// <param name="reason">审核原因，通过时可选（仅记录，不参与状态流转校验）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<BatchOperationResultDto> BatchApproveAsync(List<Guid> ids, Guid reviewedBy, string? reason, CancellationToken ct = default);

    /// <summary>
    /// 批量审核驳回。遍历 <paramref name="ids"/> 逐个调用 <see cref="RejectAsync"/>，
    /// 单个失败捕获并记录到 <see cref="BatchOperationResultDto.Failures"/>，不阻塞整批；
    /// 成功的标识收集到 <see cref="BatchOperationResultDto.SucceededIds"/>。
    /// </summary>
    /// <param name="ids">商品标识列表。</param>
    /// <param name="reviewedBy">审核人标识，透传给单个 <see cref="RejectAsync"/> 用于审核历史记录。</param>
    /// <param name="reason">驳回原因，必填，会写入审核历史。</param>
    /// <param name="ct">取消令牌。</param>
    Task<BatchOperationResultDto> BatchRejectAsync(List<Guid> ids, Guid reviewedBy, string reason, CancellationToken ct = default);

    /// <summary>调整 SKU 价格，记录价格变更历史。</summary>
    Task AdjustPriceAsync(Guid spuId, Guid skuId, AdjustPriceDto dto, string changedBy, CancellationToken ct = default);

    /// <summary>查询商品价格变更历史。</summary>
    Task<IReadOnlyList<PriceChangeRecordDto>> GetPriceHistoryAsync(Guid spuId, Guid? skuId = null, CancellationToken ct = default);

    /// <summary>调整 SKU 库存（delta 方式），记录操作日志并发布事件。</summary>
    Task UpdateStockAsync(Guid spuId, Guid skuId, UpdateStockDto dto, string operatorId, CancellationToken ct = default);
}

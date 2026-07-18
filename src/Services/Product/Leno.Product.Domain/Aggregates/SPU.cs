using Leno.Product.Domain.Events;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// 商品 SPU 聚合根，封装商品基础信息、状态机与 SKU 集合不变量。
/// 状态机：Draft → PendingReview → OnSale → TakenDown；TakenDown → PendingReview（重新上架）；PendingReview → Rejected（驳回）。
/// 所有状态流转通过行为意图明确的方法完成，禁止外部直接 set 字段。
/// </summary>
public sealed class SPU : AggregateRoot
{
    private const int MinTitleLength = 2;
    private const int MaxTitleLength = 100;
    private const int MaxSubtitleLength = 200;
    private const int MaxMainImageUrlLength = 512;
    private const int MaxSpecDimensionCount = 10;
    private const int MaxSkuCount = 100;

    private readonly List<SKU> _skus = new();
    private readonly List<AuditInfo> _auditHistory = new();

    /// <summary>所属店铺标识（引用卖家与店铺管理域 ShopId）。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>所属卖家账号标识（用户域 UserId），用于归属校验。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>商品标题，2-100 字。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>副标题/卖点，≤200 字，可空。</summary>
    public string? Subtitle { get; private set; }

    /// <summary>主图 URL。</summary>
    public string MainImageUrl { get; private set; } = string.Empty;

    /// <summary>所属分类标识。</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>所属品牌标识，可空。</summary>
    public Guid? BrandId { get; private set; }

    /// <summary>商品状态。</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>规格维度名集合（如颜色、尺寸），可空。</summary>
    public IReadOnlyList<string> Specs { get; private set; } = Array.Empty<string>();

    /// <summary>商品图片画廊。</summary>
    public IReadOnlyList<ProductImage> Images { get; private set; } = Array.Empty<ProductImage>();

    /// <summary>SKU 实体集合，聚合内实体，仅经聚合根访问。</summary>
    public IReadOnlyCollection<SKU> SKUs => _skus.AsReadOnly();

    /// <summary>店铺暂停标记，店铺事件驱动置位，恢复时清除。</summary>
    public bool SuspendedByShop { get; private set; }

    /// <summary>审核人标识（通过/驳回时记录）。</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SPU() { }

    private SPU(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，卖家创建草稿商品，置状态为草稿，附加 <see cref="ProductCreatedEvent"/>（本地领域事件）。
    /// </summary>
    /// <param name="spuId">商品标识，由应用层生成。</param>
    /// <param name="shopId">所属店铺标识。</param>
    /// <param name="sellerId">所属卖家账号标识。</param>
    /// <param name="title">商品标题。</param>
    /// <param name="mainImageUrl">主图 URL。</param>
    /// <param name="categoryId">所属分类标识。</param>
    /// <param name="subtitle">副标题，可空。</param>
    /// <param name="brandId">所属品牌标识，可空。</param>
    /// <param name="specs">规格维度名集合，可空。</param>
    /// <param name="images">商品图片画廊，可空。</param>
    public static SPU Create(
        Guid spuId,
        Guid shopId,
        Guid sellerId,
        string title,
        string mainImageUrl,
        Guid categoryId,
        string? subtitle = null,
        Guid? brandId = null,
        IEnumerable<string>? specs = null,
        IEnumerable<ProductImage>? images = null)
    {
        if (spuId == Guid.Empty)
        {
            throw new ProductDomainException("商品标识不可为空", "SPU_ID_EMPTY");
        }

        if (shopId == Guid.Empty)
        {
            throw new ProductDomainException("店铺标识不可为空", "SPU_SHOP_EMPTY");
        }

        if (sellerId == Guid.Empty)
        {
            throw new ProductDomainException("卖家标识不可为空", "SPU_SELLER_EMPTY");
        }

        if (categoryId == Guid.Empty)
        {
            throw new ProductDomainException("分类标识不可为空", "SPU_CATEGORY_EMPTY");
        }

        ValidateTitle(title);
        ValidateMainImageUrl(mainImageUrl);
        ValidateSubtitle(subtitle);
        ValidateSpecs(specs);
        ArgumentNullException.ThrowIfNull(images);

        var spu = new SPU(spuId)
        {
            ShopId = shopId,
            SellerId = sellerId,
            Title = title.Trim(),
            MainImageUrl = mainImageUrl.Trim(),
            CategoryId = categoryId,
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim(),
            BrandId = brandId == Guid.Empty ? null : brandId,
            Specs = specs?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>(),
            Images = images.ToList(),
            Status = ProductStatus.Draft,
            SuspendedByShop = false
        };

        spu.AddDomainEvent(new ProductCreatedEvent(spu.Id, sellerId, spu.Title));

        return spu;
    }

    /// <summary>
    /// 提交审核，仅草稿态可调用，流转至待审核，校验至少 1 个有效 SKU。
    /// </summary>
    public void SubmitForReview()
    {
        if (Status != ProductStatus.Draft)
        {
            throw new ProductDomainException($"当前状态为 {Status}，不可提交审核", "SPU_INVALID_TRANSITION", 409);
        }

        EnsureHasSkus();

        Status = ProductStatus.PendingReview;
    }

    /// <summary>
    /// 审核通过上架，仅待审核态可调用，流转至已上架，附加跨域 <see cref="ProductPublishedDomainEvent"/> 与本地 <see cref="ProductReviewedEvent"/>。
    /// 同时追加审核历史记录。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    /// <param name="operatorName">审核人名称，可选。</param>
    public void Approve(Guid reviewedBy, string? operatorName = null)
    {
        if (Status != ProductStatus.PendingReview)
        {
            throw new ProductDomainException($"当前状态为 {Status}，不可审核通过", "SPU_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new ProductDomainException("审核人标识不可为空", "SPU_REVIEWER_EMPTY");
        }

        EnsureHasSkus();

        Status = ProductStatus.OnSale;
        ReviewedBy = reviewedBy;
        SuspendedByShop = false;

        // 追加审核历史
        _auditHistory.Add(AuditInfo.Approved(
            reviewedBy.ToString(),
            operatorName ?? reviewedBy.ToString()));

        // ProductPublishedDomainEvent.SellerId 语义等同卖家与店铺管理域的 ShopId，故传 ShopId。
        AddDomainEvent(new ProductPublishedDomainEvent(Id, ShopId));
        AddDomainEvent(new ProductReviewedEvent(Id, ProductStatus.OnSale, reviewedBy));
    }

    /// <summary>
    /// 审核驳回，仅待审核态可调用，流转至已驳回，附加本地 <see cref="ProductReviewedEvent"/>。
    /// 同时追加审核历史记录（含驳回原因）。
    /// </summary>
    /// <param name="reviewedBy">审核人标识。</param>
    /// <param name="reason">驳回原因。</param>
    /// <param name="operatorName">审核人名称，可选。</param>
    public void Reject(Guid reviewedBy, string reason, string? operatorName = null)
    {
        if (Status != ProductStatus.PendingReview)
        {
            throw new ProductDomainException($"当前状态为 {Status}，不可驳回", "SPU_INVALID_TRANSITION", 409);
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new ProductDomainException("审核人标识不可为空", "SPU_REVIEWER_EMPTY");
        }

        ValidateReason(reason);

        Status = ProductStatus.Rejected;
        ReviewedBy = reviewedBy;

        // 追加审核历史
        _auditHistory.Add(AuditInfo.Rejected(
            reviewedBy.ToString(),
            operatorName ?? reviewedBy.ToString(),
            reason));

        AddDomainEvent(new ProductReviewedEvent(Id, ProductStatus.Rejected, reviewedBy));
    }

    /// <summary>
    /// 下架，仅已上架态可调用，流转至已下架，附加跨域 <see cref="ProductTakenDownDomainEvent"/>。
    /// </summary>
    /// <param name="reason">下架原因。</param>
    public void TakeDown(string reason)
    {
        if (Status != ProductStatus.OnSale)
        {
            throw new ProductDomainException($"当前状态为 {Status}，不可下架", "SPU_INVALID_TRANSITION", 409);
        }

        ValidateReason(reason);

        Status = ProductStatus.TakenDown;
        SuspendedByShop = false;

        // ProductTakenDownDomainEvent.SellerId 语义等同卖家与店铺管理域的 ShopId，故传 ShopId。
        AddDomainEvent(new ProductTakenDownDomainEvent(Id, ShopId));
    }

    /// <summary>
    /// 重新上架，仅已下架态可调用，流转回待审核（须重新审核）。
    /// </summary>
    public void Republish()
    {
        if (Status != ProductStatus.TakenDown)
        {
            throw new ProductDomainException($"当前状态为 {Status}，不可重新上架", "SPU_INVALID_TRANSITION", 409);
        }

        EnsureHasSkus();

        Status = ProductStatus.PendingReview;
        SuspendedByShop = false;
    }

    /// <summary>
    /// 更新商品基础信息（标题、副标题、主图、分类、品牌），任意非下架终态可调用。
    /// </summary>
    public void UpdateInfo(
        string title,
        string mainImageUrl,
        Guid categoryId,
        string? subtitle = null,
        Guid? brandId = null,
        IEnumerable<ProductImage>? images = null)
    {
        EnsureEditable();

        ValidateTitle(title);
        ValidateMainImageUrl(mainImageUrl);
        ValidateSubtitle(subtitle);
        if (categoryId == Guid.Empty)
        {
            throw new ProductDomainException("分类标识不可为空", "SPU_CATEGORY_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(images);

        Title = title.Trim();
        MainImageUrl = mainImageUrl.Trim();
        CategoryId = categoryId;
        Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
        BrandId = brandId == Guid.Empty ? null : brandId;
        Images = images.ToList();
    }

    /// <summary>
    /// 更新规格维度名集合。
    /// </summary>
    public void UpdateSpecs(IEnumerable<string> specs)
    {
        EnsureEditable();
        ValidateSpecs(specs);
        Specs = specs.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
    }

    /// <summary>
    /// 新增 SKU，校验 SkuCode 与规格组合在同 SPU 下唯一。
    /// </summary>
    public void AddSku(SKU sku)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(sku);

        if (_skus.Count >= MaxSkuCount)
        {
            throw new ProductDomainException($"SKU 数量不可超过 {MaxSkuCount}", "SPU_SKU_LIMIT", 409);
        }

        if (_skus.Any(s => string.Equals(s.SkuCode, sku.SkuCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProductDomainException($"SKU 编码已存在: {sku.SkuCode}", "SPU_SKU_CODE_DUPLICATE", 409);
        }

        if (_skus.Any(s => s.SpecAttributes.Equals(sku.SpecAttributes)))
        {
            throw new ProductDomainException("SKU 规格组合已存在", "SPU_SKU_SPEC_DUPLICATE", 409);
        }

        _skus.Add(sku);
    }

    /// <summary>
    /// 按标识获取 SKU，不存在抛领域异常。
    /// </summary>
    public SKU GetSku(Guid skuId)
    {
        var sku = _skus.FirstOrDefault(s => s.Id == skuId);
        if (sku is null)
        {
            throw new ProductDomainException("SKU 不存在", "SPU_SKU_NOT_FOUND", 404);
        }

        return sku;
    }

    /// <summary>
    /// 店铺暂停事件驱动，仅已上架态流转至店铺暂停（不发布下架事件，避免店铺商品数误减）。
    /// </summary>
    public void SuspendByShop()
    {
        if (Status != ProductStatus.OnSale)
        {
            return;
        }

        Status = ProductStatus.ShopSuspended;
        SuspendedByShop = true;
    }

    /// <summary>
    /// 店铺恢复事件驱动，仅店铺暂停态恢复至已上架。
    /// </summary>
    public void ResumeByShop()
    {
        if (!SuspendedByShop)
        {
            return;
        }

        Status = ProductStatus.OnSale;
        SuspendedByShop = false;
    }

    /// <summary>
    /// 店铺关闭事件驱动下架，已上架态流转至已下架并附加 <see cref="ProductTakenDownDomainEvent"/>。
    /// </summary>
    public void TakeDownForShopClosure(string reason)
    {
        if (Status != ProductStatus.OnSale)
        {
            return;
        }

        ValidateReason(reason);

        Status = ProductStatus.TakenDown;
        SuspendedByShop = false;

        AddDomainEvent(new ProductTakenDownDomainEvent(Id, ShopId));
    }

    #region Audit History

    /// <summary>
    /// 获取审核历史记录（只读）。
    /// </summary>
    public IReadOnlyList<AuditInfo> GetAuditHistory() => _auditHistory.AsReadOnly();

    #endregion

    #region Price Adjustment

    /// <summary>
    /// 调整指定 SKU 的价格。价格变更历史由独立 PriceHistory 聚合记录（应用层负责创建并持久化）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="newPrice">新价格，须 > 0。</param>
    /// <param name="changedBy">变更人标识。</param>
    public decimal AdjustPrice(Guid skuId, Money newPrice, string changedBy)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(changedBy))
        {
            throw new ProductDomainException("变更人标识不可为空", "SPU_CHANGED_BY_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(newPrice);
        if (newPrice.Amount <= 0)
        {
            throw new ProductDomainException("SKU 价格须大于 0", "SKU_PRICE_INVALID");
        }

        var sku = GetSku(skuId);
        var oldPrice = sku.Price.Amount;

        sku.UpdatePrice(newPrice);

        return oldPrice;
    }

    #endregion

    #region Stock Operations

    /// <summary>
    /// 调整指定 SKU 的库存（delta 方式），校验结果 ≥ 0 并发布 <see cref="StockAdjustedDomainEvent"/>。
    /// 库存操作历史不再于 SPU 内记录，由 StockBaseline 聚合承载。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="delta">库存变动量（正数补货，负数扣减）。</param>
    /// <param name="operatorId">操作人标识。</param>
    public void UpdateStock(Guid skuId, int delta, string operatorId)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ProductDomainException("操作人标识不可为空", "SPU_OPERATOR_EMPTY");
        }

        var sku = GetSku(skuId);
        var newStock = sku.StockQty + delta;

        if (newStock < 0)
        {
            throw new ProductDomainException("库存调整后不可为负", "SPU_STOCK_NEGATIVE");
        }

        sku.UpdateStock(newStock);

        AddDomainEvent(new StockAdjustedDomainEvent(Id, skuId, Id, newStock, delta, DateTime.UtcNow));
    }

    #endregion

    private void EnsureEditable()
    {
        if (Status is ProductStatus.TakenDown or ProductStatus.Rejected or ProductStatus.ShopSuspended)
        {
            throw new ProductDomainException("已下架/已驳回/店铺暂停商品不可直接编辑，请先重新上架", "SPU_OFF_SHELF", 409);
        }
    }

    private void EnsureHasSkus()
    {
        if (_skus.Count == 0)
        {
            throw new ProductDomainException("商品至少需要 1 个 SKU", "SPU_NO_SKU");
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ProductDomainException("商品标题不可为空", "SPU_TITLE_EMPTY");
        }

        var trimmed = title.Trim();
        if (trimmed.Length is < MinTitleLength or > MaxTitleLength)
        {
            throw new ProductDomainException(
                $"商品标题长度须为 {MinTitleLength}-{MaxTitleLength} 字符", "SPU_TITLE_LENGTH");
        }
    }

    private static void ValidateSubtitle(string? subtitle)
    {
        if (!string.IsNullOrWhiteSpace(subtitle) && subtitle.Trim().Length > MaxSubtitleLength)
        {
            throw new ProductDomainException($"副标题长度不可超过 {MaxSubtitleLength} 字符", "SPU_SUBTITLE_LENGTH");
        }
    }

    private static void ValidateMainImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ProductDomainException("主图 URL 不可为空", "SPU_MAIN_IMAGE_EMPTY");
        }

        if (url.Trim().Length > MaxMainImageUrlLength)
        {
            throw new ProductDomainException($"主图 URL 长度不可超过 {MaxMainImageUrlLength} 字符", "SPU_MAIN_IMAGE_LENGTH");
        }
    }

    private static void ValidateSpecs(IEnumerable<string>? specs)
    {
        if (specs is null)
        {
            return;
        }

        var list = specs.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (list.Count > MaxSpecDimensionCount)
        {
            throw new ProductDomainException(
                $"规格维度数量不可超过 {MaxSpecDimensionCount}", "SPU_SPEC_DIMENSION_LIMIT");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ProductDomainException("操作原因不可为空", "SPU_REASON_EMPTY");
        }

        if (reason.Trim().Length > 200)
        {
            throw new ProductDomainException("操作原因长度不可超过 200 字符", "SPU_REASON_LENGTH");
        }
    }
}
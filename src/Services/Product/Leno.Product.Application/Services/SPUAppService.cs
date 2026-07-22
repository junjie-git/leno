using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Exceptions;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Application.Services;

/// <summary>
/// 商品发布与管理应用服务实现。
/// 编排卖家创建草稿、提交审核、运营审核上下架与编辑用例，事务边界由工作单元统一控制，
/// 状态流转产生的集成事件经发件箱与聚合变更同事务发布。
/// </summary>
public sealed class SPUAppService : ISPUAppService
{
    private readonly ISPURepository _spuRepository;
    private readonly IPriceHistoryRepository _priceHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductUniquenessChecker _uniquenessChecker;
    private readonly IValidator<CreateProductDto> _createValidator;
    private readonly IValidator<UpdateProductDto> _updateValidator;
    private readonly IValidator<AddSkuDto> _addSkuValidator;
    private readonly IValidator<ActionReasonDto> _actionReasonValidator;

    public SPUAppService(
        ISPURepository spuRepository,
        IPriceHistoryRepository priceHistoryRepository,
        IUnitOfWork unitOfWork,
        IProductUniquenessChecker uniquenessChecker,
        IValidator<CreateProductDto> createValidator,
        IValidator<UpdateProductDto> updateValidator,
        IValidator<AddSkuDto> addSkuValidator,
        IValidator<ActionReasonDto> actionReasonValidator)
    {
        _spuRepository = spuRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _unitOfWork = unitOfWork;
        _uniquenessChecker = uniquenessChecker;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addSkuValidator = addSkuValidator;
        _actionReasonValidator = actionReasonValidator;
    }

    /// <inheritdoc />
    public async Task<ProductDto> CreateAsync(Guid sellerId, Guid shopId, CreateProductDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_createValidator, dto, ct);
        EnsureNonEmptyUser(sellerId);

        if (shopId == Guid.Empty)
        {
            throw new ProductDomainException("店铺标识不可为空", "SPU_SHOP_EMPTY");
        }

        // 校验标题同店铺唯一
        if (!await _uniquenessChecker.IsTitleUniqueInShopAsync(dto.Title, shopId, ct: ct))
        {
            throw new ProductDomainException("Product title already exists in this shop", "SPU_TITLE_DUPLICATE");
        }

        var images = MapImages(dto.Images);

        var spu = SPU.Create(
            Guid.NewGuid(),
            shopId,
            sellerId,
            dto.Title,
            dto.MainImageUrl,
            dto.CategoryId,
            dto.Subtitle,
            dto.BrandId,
            dto.Specs,
            images);

        await _spuRepository.AddAsync(spu, ct);
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            sqlEx.Number == 2601) // 唯一约束违反
        {
            throw new ProductDomainException("商品标题在同店铺内已存在或 SKU 编码已存在",
                "SPU_UNIQUE_CONSTRAINT_VIOLATION");
        }

        return ToProductDto(spu);
    }

    /// <inheritdoc />
    public async Task<ProductDto> UpdateAsync(Guid sellerId, Guid spuId, UpdateProductDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateValidator, dto, ct);
        var spu = await RequireOwnedSpuAsync(sellerId, spuId, ct);

        // 校验标题同店铺唯一（排除当前商品）
        if (!await _uniquenessChecker.IsTitleUniqueInShopAsync(dto.Title, spu.ShopId, spuId, ct))
        {
            throw new ProductDomainException("Product title already exists in this shop", "SPU_TITLE_DUPLICATE");
        }

        spu.UpdateInfo(
            dto.Title,
            dto.MainImageUrl,
            dto.CategoryId,
            dto.Subtitle,
            dto.BrandId,
            MapImages(dto.Images));

        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToProductDto(spu);
    }

    /// <inheritdoc />
    public async Task<ProductDto> AddSkuAsync(Guid sellerId, Guid spuId, AddSkuDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_addSkuValidator, dto, ct);
        var spu = await RequireOwnedSpuAsync(sellerId, spuId, ct);

        // 校验 SKU 编码全局唯一（排除当前商品下的 SKU）
        if (!await _uniquenessChecker.IsSkuCodeUniqueAsync(dto.SkuCode, spuId, ct))
        {
            throw new ProductDomainException("SKU code already in use", "SPU_SKU_CODE_GLOBAL_DUPLICATE");
        }

        var specs = SkuSpec.Create(dto.SpecAttributes.Select(a => Leno.SharedKernel.ValueObjects.SpecAttribute.Create(a.Name, a.Value)));
        var price = Leno.SharedKernel.ValueObjects.Money.Create(dto.Price, dto.Currency);

        var sku = SKU.Create(Guid.NewGuid(), spuId, dto.SkuCode, price, dto.StockQty, specs, dto.ImageUrl);
        spu.AddSku(sku);

        await _spuRepository.UpdateAsync(spu, ct);
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            sqlEx.Number == 2601)
        {
            throw new ProductDomainException("SKU 编码全局已存在",
                "SPU_SKU_CODE_GLOBAL_DUPLICATE");
        }

        return ToProductDto(spu);
    }

    /// <inheritdoc />
    public async Task SubmitForReviewAsync(Guid sellerId, Guid spuId, CancellationToken ct = default)
    {
        var spu = await RequireOwnedSpuAsync(sellerId, spuId, ct);
        spu.SubmitForReview();
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task TakeDownAsync(Guid sellerId, Guid spuId, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        var spu = await RequireOwnedSpuAsync(sellerId, spuId, ct);
        spu.TakeDown(dto.Reason);
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RepublishAsync(Guid sellerId, Guid spuId, CancellationToken ct = default)
    {
        var spu = await RequireOwnedSpuAsync(sellerId, spuId, ct);
        spu.Republish();
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    [Obsolete("请使用 IQueryHandler<ProductDetailQuery, ProductDetailResult?>，将在 2026-08-01 移除")]
    public async Task<ProductDto> GetByIdAsync(Guid spuId, CancellationToken ct = default)
    {
        var spu = await RequireSpuAsync(spuId, ct);
        return ToProductDto(spu);
    }

    /// <inheritdoc />
    [Obsolete("请使用 IQueryHandler<ProductSearchQuery, ProductSearchResult>，将在 2026-08-01 移除")]
    public async Task<PageResult<ProductDto>> QueryProductsAsync(ProductQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ProductStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ProductStatus>(query.Status, ignoreCase: true, out var statusValue))
        {
            status = statusValue;
        }

        var (items, total) = await _spuRepository.QueryAsync(
            query.ShopId, query.SellerId, status, query.CategoryId, query.Keyword, query.Page, query.PageSize, ct);

        var dtos = items.Select(ToProductDto).ToList();

        var safePage = query.Page < 1 ? 1 : query.Page;
        var safePageSize = query.PageSize is <= 0 or > 100 ? 20 : query.PageSize;

        return new PageResult<ProductDto>(dtos, total, safePage, safePageSize);
    }

    /// <inheritdoc />
    public async Task ApproveAsync(Guid spuId, Guid reviewedBy, CancellationToken ct = default)
    {
        EnsureNonEmptyUser(reviewedBy);
        var spu = await RequireSpuAsync(spuId, ct);
        spu.Approve(reviewedBy);
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RejectAsync(Guid spuId, Guid reviewedBy, ActionReasonDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_actionReasonValidator, dto, ct);
        EnsureNonEmptyUser(reviewedBy);
        var spu = await RequireSpuAsync(spuId, ct);
        spu.Reject(reviewedBy, dto.Reason);
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task AdjustPriceAsync(Guid spuId, Guid skuId, AdjustPriceDto dto, string changedBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(changedBy))
        {
            throw new ProductDomainException("变更人标识不可为空", "SPU_CHANGED_BY_EMPTY");
        }

        if (dto.Price <= 0)
        {
            throw new ProductDomainException("价格须大于 0", "SPU_PRICE_INVALID");
        }

        var spu = await RequireSpuAsync(spuId, ct);
        var price = Leno.SharedKernel.ValueObjects.Money.Create(dto.Price, dto.Currency);
        var oldPrice = spu.AdjustPrice(skuId, price, changedBy);

        // 创建独立的 PriceHistory 聚合记录价格变更（从 SPU 拆分）
        // 修复审计 #13：透传 dto.Reason 与 changedBy，替代原 reason: null
        var history = PriceHistory.Create(spuId, skuId, oldPrice, dto.Price, dto.Reason, dto.Currency, changedBy);
        await _priceHistoryRepository.AddAsync(history, ct);

        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceChangeRecordDto>> GetPriceHistoryAsync(Guid spuId, Guid? skuId = null, CancellationToken ct = default)
    {
        // 验证 SPU 存在（保持原有契约：不存在的 SPU 抛 404）
        await RequireSpuAsync(spuId, ct);

        var histories = await _priceHistoryRepository.GetBySpuIdAsync(spuId, ct);

        if (skuId.HasValue)
        {
            histories = histories.Where(h => h.SkuId == skuId.Value).ToList();
        }

        return histories.Select(ToPriceChangeRecordDto).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateStockAsync(Guid spuId, Guid skuId, UpdateStockDto dto, string operatorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ProductDomainException("操作人标识不可为空", "SPU_OPERATOR_EMPTY");
        }

        var spu = await RequireSpuAsync(spuId, ct);
        spu.UpdateStock(skuId, dto.Delta, operatorId);
        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<SPU> RequireSpuAsync(Guid spuId, CancellationToken ct)
    {
        var spu = await _spuRepository.GetByIdAsync(spuId, ct);
        if (spu is null)
        {
            throw new ProductDomainException("商品不存在", "SPU_NOT_FOUND");
        }

        return spu;
    }

    private async Task<SPU> RequireOwnedSpuAsync(Guid sellerId, Guid spuId, CancellationToken ct)
    {
        EnsureNonEmptyUser(sellerId);
        var spu = await RequireSpuAsync(spuId, ct);
        if (spu.SellerId != sellerId)
        {
            throw new ProductDomainException("无权操作他人商品", "SPU_NOT_OWNED");
        }

        return spu;
    }

    private static void EnsureNonEmptyUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ProductDomainException("卖家账号标识不可为空", "SELLER_USER_EMPTY");
        }
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ProductValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static List<ProductImage> MapImages(IEnumerable<ProductImageDto> images)
        => images.Select(i => ProductImage.Create(i.Url, i.SortOrder, i.IsMain)).ToList();

    private static ProductDto ToProductDto(SPU spu)
        => new()
        {
            Id = spu.Id,
            ShopId = spu.ShopId,
            SellerId = spu.SellerId,
            Title = spu.Title,
            Subtitle = spu.Subtitle,
            MainImageUrl = spu.MainImageUrl,
            CategoryId = spu.CategoryId,
            BrandId = spu.BrandId,
            Status = spu.Status,
            Specs = spu.Specs.ToList(),
            Images = spu.Images.Select(i => new ProductImageDto
            {
                Url = i.Url,
                SortOrder = i.SortOrder,
                IsMain = i.IsMain
            }).ToList(),
            Skus = spu.SKUs.Select(s => new SkuDto
            {
                Id = s.Id,
                SkuCode = s.SkuCode,
                Price = s.Price.Amount,
                Currency = s.Price.Currency,
                StockQty = s.StockQty,
                SpecAttributes = s.SpecAttributes.Attributes.Select(a => new SpecAttributeDto
                {
                    Name = a.Name,
                    Value = a.Value
                }).ToList(),
                Status = s.Status,
                ImageUrl = s.ImageUrl
            }).ToList(),
            AuditHistory = spu.GetAuditHistory().Select(a => new AuditInfoDto
            {
                OperatorId = a.OperatorId,
                OperatorName = a.OperatorName,
                Result = a.Result,
                Reason = a.Reason,
                AuditedAt = a.AuditedAt
            }).ToList(),
            SuspendedByShop = spu.SuspendedByShop,
            ReviewedBy = spu.ReviewedBy,
            CreatedAt = spu.CreatedAt,
            UpdatedAt = spu.UpdatedAt
        };

    private static PriceChangeRecordDto ToPriceChangeRecordDto(PriceHistory history)
        => new()
        {
            SkuId = history.SkuId.ToString(),
            OldPrice = history.OldPrice,
            NewPrice = history.NewPrice,
            ChangedAt = history.ChangedAt,
            // 修复审计 #13：返回真实变更人，替代硬编码 string.Empty
            ChangedBy = history.ChangedBy,
            // 修复审计 #19：返回变更原因
            Reason = history.Reason
        };
}

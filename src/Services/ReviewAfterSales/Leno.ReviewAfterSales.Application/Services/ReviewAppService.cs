using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Application.Services;

/// <summary>
/// 评价应用服务实现，编排评价提交、卖家回复、运营审核与查询用例。
/// 提交前经 <see cref="IReviewEligibilityChecker"/> 校验订单完成且未评价。
/// 买家按订单行查询评价时通过 <see cref="IOrderStatusProvider"/> 反查订单归属，防止越权查询他人评价。
/// 卖家侧评价列表按 productName 过滤时经 <see cref="IProductInfoQueryService"/> 防腐层反查 SPU 名称映射。
/// </summary>
public sealed class ReviewAppService : IReviewAppService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IReviewEligibilityChecker _eligibilityChecker;
    private readonly IOrderStatusProvider _orderStatusProvider;
    private readonly IProductInfoQueryService _productInfoQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReviewAppService> _logger;

    public ReviewAppService(
        IReviewRepository reviewRepository,
        IReviewEligibilityChecker eligibilityChecker,
        IOrderStatusProvider orderStatusProvider,
        IProductInfoQueryService productInfoQueryService,
        IUnitOfWork unitOfWork,
        ILogger<ReviewAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(eligibilityChecker);
        ArgumentNullException.ThrowIfNull(orderStatusProvider);
        ArgumentNullException.ThrowIfNull(productInfoQueryService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _reviewRepository = reviewRepository;
        _eligibilityChecker = eligibilityChecker;
        _orderStatusProvider = orderStatusProvider;
        _productInfoQueryService = productInfoQueryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReviewDto> SubmitReviewAsync(Guid userId, SubmitReviewDto dto, CancellationToken ct = default)
    {
        // 资格校验器查询订单域并校验申请人归属与订单行存在性，返回携带真实 SpuId/SkuId 的订单行概要。
        // 忽略 dto.SpuId/dto.SkuId（客户端可伪造），仅使用订单域返回的真实商品标识创建评价。
        var lineItem = await _eligibilityChecker.EnsureEligibleAsync(dto.OrderId, dto.OrderLineId, userId, ct);

        var reviewId = Guid.NewGuid();
        var review = ReviewAggregate.Create(
            reviewId, dto.OrderId, dto.OrderLineId, lineItem.SpuId, lineItem.SkuId,
            userId, dto.Rating, dto.Content, dto.Images, lineItem.SellerId);

        await _reviewRepository.AddAsync(review, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("评价已提交 ReviewId={ReviewId} OrderId={OrderId} OrderLineId={OrderLineId}", reviewId, dto.OrderId, dto.OrderLineId);
        return ToDto(review);
    }

    /// <inheritdoc />
    public async Task SellerReplyAsync(Guid reviewId, Guid sellerId, string content, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
            ?? throw new InvalidOperationException($"评价不存在 ReviewId={reviewId}");

        review.SellerReply(sellerId, content);
        await _reviewRepository.UpdateAsync(review, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ApproveReviewAsync(Guid reviewId, Guid auditorId, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
            ?? throw new InvalidOperationException($"评价不存在 ReviewId={reviewId}");

        review.Approve(auditorId);
        await _reviewRepository.UpdateAsync(review, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task HideReviewAsync(Guid reviewId, Guid operatorId, string reason, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
            ?? throw new InvalidOperationException($"评价不存在 ReviewId={reviewId}");

        review.Hide(operatorId, reason);
        await _reviewRepository.UpdateAsync(review, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ReviewListResultDto> GetReviewsBySpuAsync(Guid spuId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _reviewRepository.QueryAsync(spuId, null, ReviewStatus.Approved, page, pageSize, ct);
        var total = await _reviewRepository.CountAsync(spuId, null, ReviewStatus.Approved, ct);
        return new ReviewListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task<ReviewDto?> GetReviewByOrderLineAsync(Guid orderLineId, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByOrderLineAsync(orderLineId, ct);
        return review is null ? null : ToDto(review);
    }

    /// <inheritdoc />
    public async Task<ReviewDto?> GetReviewByOrderLineForUserAsync(Guid orderLineId, Guid userId, CancellationToken ct = default)
    {
        // 先按订单行查询评价聚合，从评价聚合取得 OrderId，再反查订单域校验当前用户是否为订单归属买家。
        // 采用“评价仓储返回 OrderId 后调用 IOrderStatusProvider 校验归属”方案，避免修改订单域接口。
        var review = await _reviewRepository.GetByOrderLineAsync(orderLineId, ct);
        if (review is null)
        {
            return null;
        }

        var order = await _orderStatusProvider.GetOrderStatusAsync(review.OrderId, ct)
            ?? throw new InvalidOperationException($"订单不存在 OrderId={review.OrderId}");
        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权查询此评价", "REVIEW_FORBIDDEN");
        }

        return ToDto(review);
    }

    /// <inheritdoc />
    public async Task<ReviewListResultDto> GetReviewsByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _reviewRepository.QueryAsync(null, userId, null, page, pageSize, ct);
        var total = await _reviewRepository.CountAsync(null, userId, null, ct);
        return new ReviewListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task<ReviewListResultDto> QueryReviewsAsync(ReviewStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _reviewRepository.QueryAsync(null, null, status, page, pageSize, ct);
        var total = await _reviewRepository.CountAsync(null, null, status, ct);
        return new ReviewListResultDto { Items = items.ConvertAll(ToDto), Total = total, Page = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task<ReviewDto> AppendAdditionalReviewAsync(Guid reviewId, Guid userId, AppendReviewDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
            ?? throw new InvalidOperationException($"评价不存在 ReviewId={reviewId}");

        // 越权校验：仅评价人（买家）本人可追评，防止他人冒充追评
        if (review.UserId != userId)
        {
            throw new ReviewDomainException("无权追评此评价", "REVIEW_FORBIDDEN");
        }

        review.AppendAdditionalReview(dto.Content, dto.Images);
        await _reviewRepository.UpdateAsync(review, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("买家追评已提交 ReviewId={ReviewId} UserId={UserId}", reviewId, userId);
        return ToDto(review);
    }

    /// <inheritdoc />
    public async Task<ReviewListResultDto> GetBySellerAsync(
        Guid sellerId,
        int? rating,
        bool? replied,
        string? productName,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // productName 模糊搜索需经商品域 ACL 过滤 SpuId 列表：
        // 1. 查询本店铺已通过评价关联的去重 SpuId 列表（评价域内查询，无远程调用）
        // 2. 调用商品域 ACL 批量获取 SpuId → 商品名称映射
        // 3. 在内存中按 productName 模糊匹配过滤 SpuId 列表
        // 4. 将过滤后的 SpuId 列表传入仓储查询；若无匹配 SpuId，直接返回空结果避免全表扫描
        IReadOnlyList<Guid>? filteredSpuIds = null;
        if (!string.IsNullOrWhiteSpace(productName))
        {
            var sellerSpuIds = await _reviewRepository.GetDistinctSpuIdsBySellerAsync(sellerId, ct);
            if (sellerSpuIds.Count == 0)
            {
                return new ReviewListResultDto { Items = new List<ReviewDto>(), Total = 0, Page = page, PageSize = pageSize };
            }

            var productNameMap = await _productInfoQueryService.GetProductNamesBySpuIdsAsync(sellerSpuIds, ct);
            var keyword = productName.Trim();
            filteredSpuIds = productNameMap
                .Where(kv => kv.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();

            if (filteredSpuIds.Count == 0)
            {
                return new ReviewListResultDto { Items = new List<ReviewDto>(), Total = 0, Page = page, PageSize = pageSize };
            }
        }

        var items = await _reviewRepository.QueryBySellerAsync(
            sellerId, rating, replied, filteredSpuIds, startDate, endDate, page, pageSize, ct);
        var total = await _reviewRepository.CountBySellerAsync(
            sellerId, rating, replied, filteredSpuIds, startDate, endDate, ct);

        return new ReviewListResultDto
        {
            Items = items.ConvertAll(ToDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static ReviewDto ToDto(ReviewAggregate review)
    {
        return new ReviewDto
        {
            ReviewId = review.Id,
            OrderId = review.OrderId,
            OrderLineId = review.OrderLineId,
            SpuId = review.SpuId,
            SkuId = review.SkuId,
            UserId = review.UserId,
            SellerId = review.SellerId,
            Rating = review.Rating,
            Content = review.Content,
            Images = review.Images.ToList(),
            Status = review.Status,
            SellerReplyContent = review.SellerReplyContent,
            SellerReplyBy = review.SellerReplyBy,
            SellerReplyAt = review.SellerReplyAt,
            SubmittedAt = review.SubmittedAt,
            AuditedAt = review.AuditedAt,
            HiddenAt = review.HiddenAt,
            AppendContent = review.AppendContent,
            AppendImages = review.AppendImages.ToList(),
            AppendedAt = review.AppendedAt
        };
    }
}

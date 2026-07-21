using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.Aggregates;
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
/// </summary>
public sealed class ReviewAppService : IReviewAppService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IReviewEligibilityChecker _eligibilityChecker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReviewAppService> _logger;

    public ReviewAppService(
        IReviewRepository reviewRepository,
        IReviewEligibilityChecker eligibilityChecker,
        IUnitOfWork unitOfWork,
        ILogger<ReviewAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(eligibilityChecker);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _reviewRepository = reviewRepository;
        _eligibilityChecker = eligibilityChecker;
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
            Rating = review.Rating,
            Content = review.Content,
            Images = review.Images.ToList(),
            Status = review.Status,
            SellerReplyContent = review.SellerReplyContent,
            SellerReplyBy = review.SellerReplyBy,
            SellerReplyAt = review.SellerReplyAt,
            SubmittedAt = review.SubmittedAt,
            AuditedAt = review.AuditedAt,
            HiddenAt = review.HiddenAt
        };
    }
}

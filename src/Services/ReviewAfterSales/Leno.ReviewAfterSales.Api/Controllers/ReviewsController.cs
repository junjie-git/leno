using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ReviewAfterSales.Api.Controllers;

/// <summary>
/// 评价控制器。
/// 买家端：提交评价、查询商品评价、查询订单行评价、我的评价。
/// 卖家端：回复评价。
/// 运营端：审核通过/隐藏评价、分页查询。
/// </summary>
[ApiController]
public sealed class ReviewsController : ReviewControllerBase
{
    private readonly IReviewAppService _reviewAppService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileSignatureDetector _fileSignatureDetector;

    public ReviewsController(
        ICurrentUserContext currentUser,
        IReviewAppService reviewAppService,
        IFileStorageService fileStorage,
        IFileSignatureDetector fileSignatureDetector)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reviewAppService);
        ArgumentNullException.ThrowIfNull(fileStorage);
        ArgumentNullException.ThrowIfNull(fileSignatureDetector);
        _reviewAppService = reviewAppService;
        _fileStorage = fileStorage;
        _fileSignatureDetector = fileSignatureDetector;
    }

    // ========== 买家端 ==========

    /// <summary>买家提交评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitReviewAsync([FromBody] SubmitReviewDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.SubmitReviewAsync(userId, dto, ct);
        return CreatedAtAction("GetReviewByOrderLine", new { orderLineId = result.OrderLineId }, ApiResponse.Success(result));
    }

    /// <summary>按订单行查询评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/reviews/order-line/{orderLineId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewByOrderLineAsync(Guid orderLineId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.GetReviewByOrderLineForUserAsync(orderLineId, userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按 SPU 分页查询已通过评价（商品详情页）。</summary>
    [HttpGet("api/products/{spuId:guid}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewsBySpuAsync(Guid spuId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _reviewAppService.GetReviewsBySpuAsync(spuId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/reviews/mine")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviewsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.GetReviewsByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家上传评价图片。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/reviews/images")]
    [ProducesResponseType(typeof(ApiResponse<ImageUploadResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadReviewImagesAsync(IFormFileCollection files, CancellationToken ct)
    {
        const int maxFiles = 9;
        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        if (files == null || files.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "请至少上传一张图片"));
        }

        if (files.Count > maxFiles)
        {
            return BadRequest(ApiResponse.Fail(400, $"最多上传 {maxFiles} 张图片"));
        }

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return BadRequest(ApiResponse.Fail(400, "文件内容为空"));
            }

            if (file.Length > maxFileSize)
            {
                return BadRequest(ApiResponse.Fail(400, $"文件 {file.FileName} 超过 5MB 限制"));
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return BadRequest(ApiResponse.Fail(400, $"文件 {file.FileName} 格式不支持，仅支持 JPG/PNG/WebP"));
            }

            // 审计 3.10：上传图片流显式 using，避免依赖框架兜底造成句柄泄漏
            // 审计 3.11：扩展名校验后追加 Magic Number 校验，防止伪装扩展名上传非图片文件
            await using var stream = file.OpenReadStream();
            if (!_fileSignatureDetector.IsValidImageSignature(stream, extension))
            {
                return BadRequest(ApiResponse.Fail(400, $"文件 {file.FileName} 内容与扩展名不符，疑似伪装文件"));
            }

            var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "review", ct);
            urls.Add(result.Url);
        }

        return Ok(ApiResponse.Success(new ImageUploadResultDto { Urls = urls }));
    }

    // ========== 卖家端 ==========

    /// <summary>卖家回复评价。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/reviews/{id:guid}/reply")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerReplyAsync(Guid id, [FromBody] SellerReplyDto dto, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        await _reviewAppService.SellerReplyAsync(id, sellerId, dto.Content, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 运营端 ==========

    /// <summary>运营审核通过评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/reviews/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveReviewAsync(Guid id, CancellationToken ct)
    {
        var auditorId = GetCurrentUserId();
        await _reviewAppService.ApproveReviewAsync(id, auditorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营隐藏违规评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/reviews/{id:guid}/hide")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HideReviewAsync(Guid id, [FromBody] ModerateReviewDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _reviewAppService.HideReviewAsync(id, operatorId, dto.Reason ?? string.Empty, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营分页查询评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryReviewsAsync(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _reviewAppService.QueryReviewsAsync(status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}

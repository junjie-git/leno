using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Review.Api.Controllers;

/// <summary>
/// 买家评价控制器（评价 BC 独立维护）。
/// 端点：提交评价、按订单行查、我的评价、追评、上传图片。
/// 全部端点需 Buyer 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Buyer")]
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

    /// <summary>买家提交评价。</summary>
    [HttpPost("api/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitReviewAsync([FromBody] SubmitReviewDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.SubmitReviewAsync(userId, dto, ct);
        return CreatedAtAction("GetReviewByOrderLine", new { orderLineId = result.OrderLineId }, ApiResponse.Success(result));
    }

    /// <summary>按订单行查询评价。</summary>
    [HttpGet("api/reviews/order-line/{orderLineId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewByOrderLineAsync(Guid orderLineId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.GetReviewByOrderLineForUserAsync(orderLineId, userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的评价。</summary>
    [HttpGet("api/reviews/mine")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviewsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.GetReviewsByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 买家追评，仅已通过（Approved）态评价可追评一次。
    /// 通过 JWT 注入 userId 与评价聚合 UserId 比对进行归属校验，防止越权追评他人评价。
    /// </summary>
    [HttpPost("api/reviews/{id:guid}/append")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AppendAdditionalReviewAsync(Guid id, [FromBody] AppendReviewDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.AppendAdditionalReviewAsync(id, userId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家上传评价图片。</summary>
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
}

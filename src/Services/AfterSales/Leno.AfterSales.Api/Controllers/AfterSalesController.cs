using Leno.AfterSales.Application;
using Leno.AfterSales.Application.DTOs;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.AfterSales.Api.Controllers;

/// <summary>
/// 买家售后控制器（售后 BC 独立维护）。
/// 端点：申请售后、退货、撤销、按订单查、我的售后、上传凭证。
/// 全部端点需 Buyer 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Buyer")]
public sealed class AfterSalesController : AfterSalesControllerBase
{
    private readonly IAfterSalesAppService _afterSalesAppService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileSignatureDetector _fileSignatureDetector;

    public AfterSalesController(
        ICurrentUserContext currentUser,
        IAfterSalesAppService afterSalesAppService,
        IFileStorageService fileStorage,
        IFileSignatureDetector fileSignatureDetector)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(afterSalesAppService);
        ArgumentNullException.ThrowIfNull(fileStorage);
        ArgumentNullException.ThrowIfNull(fileSignatureDetector);
        _afterSalesAppService = afterSalesAppService;
        _fileStorage = fileStorage;
        _fileSignatureDetector = fileSignatureDetector;
    }

    /// <summary>买家提交售后申请。</summary>
    [HttpPost("api/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitAfterSalesAsync([FromBody] SubmitAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.SubmitAfterSalesAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetAfterSalesByOrder), new { orderId = result.OrderId }, ApiResponse.Success(result));
    }

    /// <summary>买家退货填写物流单号。</summary>
    [HttpPost("api/after-sales/{id:guid}/return-goods")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReturnGoodsAsync(Guid id, [FromBody] ReturnGoodsDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _afterSalesAppService.ReturnGoodsAsync(id, userId, dto.TrackingNo, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家撤销售后申请。</summary>
    [HttpPost("api/after-sales/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAfterSalesAsync(Guid id, [FromBody] CancelAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _afterSalesAppService.CancelAfterSalesAsync(id, userId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家按订单查询售后单。</summary>
    [HttpGet("api/after-sales/order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<AfterSalesDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAfterSalesByOrder(Guid orderId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetByOrderIdForUserAsync(orderId, userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的售后单。</summary>
    [HttpGet("api/after-sales/mine")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAfterSalesAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家上传售后凭证图片。</summary>
    [HttpPost("api/after-sales/images")]
    [ProducesResponseType(typeof(ApiResponse<ImageUploadResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadAfterSalesImagesAsync(IFormFileCollection files, CancellationToken ct)
    {
        const int maxFiles = 5;
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

            var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "aftersales", ct);
            urls.Add(result.Url);
        }

        return Ok(ApiResponse.Success(new ImageUploadResultDto { Urls = urls }));
    }
}

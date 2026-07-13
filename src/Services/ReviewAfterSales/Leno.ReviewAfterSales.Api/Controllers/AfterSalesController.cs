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
/// 售后控制器。
/// 买家端：提交售后申请、退货、撤销、查询我的售后。
/// 卖家端：审核同意/驳回、确认收货、查询收到的售后单。
/// 运营端：审核通过/驳回、分页查询全平台售后单。
/// </summary>
[ApiController]
public sealed class AfterSalesController : ReviewControllerBase
{
    private readonly IAfterSalesAppService _afterSalesAppService;
    private readonly IFileStorageService _fileStorage;

    public AfterSalesController(ICurrentUserContext currentUser, IAfterSalesAppService afterSalesAppService, IFileStorageService fileStorage)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(afterSalesAppService);
        ArgumentNullException.ThrowIfNull(fileStorage);
        _afterSalesAppService = afterSalesAppService;
        _fileStorage = fileStorage;
    }

    // ========== 买家端 ==========

    /// <summary>买家提交售后申请。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitAfterSalesAsync([FromBody] SubmitAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.SubmitAfterSalesAsync(userId, dto, ct);
        return CreatedAtAction("GetAfterSalesByOrder", new { orderId = result.OrderId }, ApiResponse.Success(result));
    }

    /// <summary>买家退货填写物流单号。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/after-sales/{id:guid}/return-goods")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReturnGoodsAsync(Guid id, [FromBody] ReturnGoodsDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _afterSalesAppService.ReturnGoodsAsync(id, userId, dto.TrackingNo, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家撤销售后申请。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/after-sales/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAfterSalesAsync(Guid id, [FromBody] CancelAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _afterSalesAppService.CancelAfterSalesAsync(id, userId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家按订单查询售后单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/after-sales/order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<AfterSalesDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAfterSalesByOrderAsync(Guid orderId, CancellationToken ct)
    {
        var result = await _afterSalesAppService.GetByOrderIdAsync(orderId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的售后单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/after-sales/mine")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAfterSalesAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家上传售后凭证图片。</summary>
    [Authorize(Roles = "Buyer")]
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

            var result = await _fileStorage.UploadAsync(file.OpenReadStream(), file.FileName, file.ContentType, "aftersales", ct);
            urls.Add(result.Url);
        }

        return Ok(ApiResponse.Success(new ImageUploadResultDto { Urls = urls }));
    }

    // ========== 卖家端 ==========

    /// <summary>卖家查询收到的售后单。</summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("api/seller/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSellerAfterSalesAsync(
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var sellerId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetBySellerAsync(sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>卖家审核同意售后。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/seller/after-sales/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerApproveAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>卖家驳回售后。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/seller/after-sales/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerRejectAfterSalesAsync(Guid id, [FromBody] RejectAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.RejectAfterSalesAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>卖家确认收到退货。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/seller/after-sales/{id:guid}/confirm-return")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerConfirmReturnAsync(Guid id, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ConfirmReturnAsync(id, operatorId, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 运营端 ==========

    /// <summary>运营审核通过售后。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/after-sales/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminApproveAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.AdminApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营驳回售后。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/after-sales/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminRejectAfterSalesAsync(Guid id, [FromBody] RejectAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.AdminRejectAfterSalesAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营分页查询全平台售后单。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAfterSalesAsync(
        [FromQuery] Guid? orderId,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? sellerId,
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _afterSalesAppService.QueryAsync(orderId, userId, sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}

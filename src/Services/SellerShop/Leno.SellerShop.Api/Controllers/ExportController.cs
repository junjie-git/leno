using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.Dtos;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 数据导出控制器，提供创建导出任务、查询任务列表、下载文件端点。
/// 全部端点需认证，数据范围限定为当前卖家自己的店铺。
/// 实际文件生成由 <c>ExportBackgroundService</c> 异步完成，Controller 仅负责派发与下载。
/// </summary>
[Authorize]
[ApiController]
[Route("api/seller/export")]
public sealed class ExportController : SellerShopControllerBase
{
    private readonly IExportAppService _exportAppService;
    private readonly IFileStorageService _fileStorageService;

    public ExportController(
        ICurrentUserContext currentUser,
        IExportAppService exportAppService,
        IFileStorageService fileStorageService)
        : base(currentUser)
    {
        _exportAppService = exportAppService ?? throw new ArgumentNullException(nameof(exportAppService));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
    }

    /// <summary>创建导出任务（状态 Processing，后台异步生成文件）。</summary>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(ApiResponse<ExportTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTaskAsync([FromBody] CreateExportTaskDto dto, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var task = await _exportAppService.CreateTaskAsync(sellerId, dto, ct);
        return Ok(ApiResponse.Success(task));
    }

    /// <summary>查询当前卖家的导出任务列表（分页）。</summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ExportTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTasksAsync([FromQuery] ExportTaskQueryParams queryParams, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var result = await _exportAppService.ListTasksAsync(sellerId, queryParams, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>下载导出文件（任务状态须为 Completed）。</summary>
    [HttpGet("tasks/{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var download = await _exportAppService.GetDownloadAsync(sellerId, id, ct);
        if (download is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "文件不存在或任务未完成"));
        }

        var stream = await _fileStorageService.DownloadAsync(download.Value.FilePath, ct);
        return File(stream, download.Value.ContentType, download.Value.FileName);
    }
}

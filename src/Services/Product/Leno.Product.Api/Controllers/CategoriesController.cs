using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 分类控制器，提供分类树查询（公开）与运营端分类管理端点。
/// 查询端点对所有认证用户开放；管理端点仅 Admin/Operator 可访问。
/// </summary>
[ApiController]
public sealed class CategoriesController : ProductControllerBase
{
    private readonly ICategoryAppService _categoryAppService;

    public CategoriesController(ICurrentUserContext currentUser, ICategoryAppService categoryAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(categoryAppService);
        _categoryAppService = categoryAppService;
    }

    /// <summary>查询分类树（仅启用分类，按层级与排序组装）。</summary>
    [Authorize]
    [HttpGet("api/categories/tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreeAsync(CancellationToken ct)
    {
        var tree = await _categoryAppService.GetTreeAsync(ct);
        return Ok(ApiResponse.Success(tree));
    }

    /// <summary>按标识查询分类详情。</summary>
    [Authorize]
    [HttpGet("api/categories/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var category = await _categoryAppService.GetByIdAsync(id, ct);
        return Ok(ApiResponse.Success(category));
    }

    /// <summary>运营创建分类。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/categories")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCategoryDto dto, CancellationToken ct)
    {
        var category = await _categoryAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = category.Id }, ApiResponse.Success(category));
    }

    /// <summary>运营更新分类。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("api/admin/categories/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateCategoryDto dto, CancellationToken ct)
    {
        var category = await _categoryAppService.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse.Success(category));
    }

    /// <summary>运营启用分类。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/categories/{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        await _categoryAppService.EnableAsync(id, ct);
        return Ok(ApiResponse.Success("分类已启用"));
    }

    /// <summary>运营停用分类。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/categories/{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        await _categoryAppService.DisableAsync(id, ct);
        return Ok(ApiResponse.Success("分类已停用"));
    }
}

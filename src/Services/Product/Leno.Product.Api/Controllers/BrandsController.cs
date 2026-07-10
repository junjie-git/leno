using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 品牌控制器，提供品牌分页查询（公开）与运营端品牌管理端点。
/// 查询端点对所有认证用户开放；管理端点仅 Admin/Operator 可访问。
/// </summary>
[ApiController]
public sealed class BrandsController : ProductControllerBase
{
    private readonly IBrandAppService _brandAppService;

    public BrandsController(ICurrentUserContext currentUser, IBrandAppService brandAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(brandAppService);
        _brandAppService = brandAppService;
    }

    /// <summary>分页查询品牌列表。</summary>
    [Authorize]
    [HttpGet("api/brands")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<BrandDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync([FromQuery] BrandQueryDto query, CancellationToken ct)
    {
        var result = await _brandAppService.QueryAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按标识查询品牌详情。</summary>
    [Authorize]
    [HttpGet("api/brands/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var brand = await _brandAppService.GetByIdAsync(id, ct);
        return Ok(ApiResponse.Success(brand));
    }

    /// <summary>运营创建品牌。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/brands")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateBrandDto dto, CancellationToken ct)
    {
        var brand = await _brandAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = brand.Id }, ApiResponse.Success(brand));
    }

    /// <summary>运营更新品牌。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("api/admin/brands/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateBrandDto dto, CancellationToken ct)
    {
        var brand = await _brandAppService.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse.Success(brand));
    }

    /// <summary>运营启用品牌。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/brands/{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        await _brandAppService.EnableAsync(id, ct);
        return Ok(ApiResponse.Success("品牌已启用"));
    }

    /// <summary>运营停用品牌。</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("api/admin/brands/{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        await _brandAppService.DisableAsync(id, ct);
        return Ok(ApiResponse.Success("品牌已停用"));
    }
}

using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 物流公司控制器。
/// 运营端（/api/admin/logistics-companies）：物流公司 CRUD、启停、分页查询，需 Operator/Admin 角色。
/// </summary>
[ApiController]
public sealed class LogisticsCompaniesController : OrderControllerBase
{
    private readonly ILogisticsCompanyAppService _logisticsCompanyAppService;

    public LogisticsCompaniesController(ICurrentUserContext currentUser, ILogisticsCompanyAppService logisticsCompanyAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(logisticsCompanyAppService);
        _logisticsCompanyAppService = logisticsCompanyAppService;
    }

    /// <summary>创建物流公司。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/logistics-companies")]
    [ProducesResponseType(typeof(ApiResponse<LogisticsCompanyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateLogisticsCompanyDto dto, CancellationToken ct)
    {
        var company = await _logisticsCompanyAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(company));
    }

    /// <summary>更新物流公司可编辑字段。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/logistics-companies/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LogisticsCompanyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateLogisticsCompanyDto dto, CancellationToken ct)
    {
        var company = await _logisticsCompanyAppService.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse.Success(company));
    }

    /// <summary>启用物流公司。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/logistics-companies/{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        await _logisticsCompanyAppService.EnableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用物流公司。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/logistics-companies/{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        await _logisticsCompanyAppService.DisableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>分页查询物流公司列表。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/logistics-companies")]
    [ProducesResponseType(typeof(ApiResponse<List<LogisticsCompanyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var companies = await _logisticsCompanyAppService.ListAsync(page, pageSize, ct);
        return Ok(ApiResponse.Success(companies));
    }
}

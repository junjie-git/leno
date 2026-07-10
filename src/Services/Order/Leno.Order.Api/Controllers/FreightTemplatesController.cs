using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 运费模板控制器。
/// 卖家/管理端（/api/seller/freight-templates）：运费模板 CRUD、区域规则更新、启停、查询，需 Seller/Admin 角色。
/// 另含物流轨迹查询端点（/api/orders/{id}/logistics），需 Buyer/Seller/Admin 角色。
/// </summary>
[ApiController]
public sealed class FreightTemplatesController : OrderControllerBase
{
    private readonly IFreightTemplateAppService _freightTemplateAppService;
    private readonly IOrderAppService _orderAppService;
    private readonly ILogisticsTrackingService _logisticsTrackingService;

    public FreightTemplatesController(
        ICurrentUserContext currentUser,
        IFreightTemplateAppService freightTemplateAppService,
        IOrderAppService orderAppService,
        ILogisticsTrackingService logisticsTrackingService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(freightTemplateAppService);
        ArgumentNullException.ThrowIfNull(orderAppService);
        ArgumentNullException.ThrowIfNull(logisticsTrackingService);
        _freightTemplateAppService = freightTemplateAppService;
        _orderAppService = orderAppService;
        _logisticsTrackingService = logisticsTrackingService;
    }

    // ========== 卖家/管理端：运费模板 ==========

    /// <summary>创建运费模板（含区域规则）。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost("api/seller/freight-templates")]
    [ProducesResponseType(typeof(ApiResponse<FreightTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateFreightTemplateDto dto, CancellationToken ct)
    {
        var template = await _freightTemplateAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(template));
    }

    /// <summary>更新运费模板区域规则（整体替换）。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("api/seller/freight-templates/{id:guid}/rules")]
    [ProducesResponseType(typeof(ApiResponse<FreightTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRulesAsync(Guid id, [FromBody] UpdateFreightTemplateRulesDto dto, CancellationToken ct)
    {
        var template = await _freightTemplateAppService.UpdateRulesAsync(id, dto, ct);
        return Ok(ApiResponse.Success(template));
    }

    /// <summary>启用运费模板。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost("api/seller/freight-templates/{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        await _freightTemplateAppService.EnableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用运费模板。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost("api/seller/freight-templates/{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        await _freightTemplateAppService.DisableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>分页查询运费模板列表。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpGet("api/seller/freight-templates")]
    [ProducesResponseType(typeof(ApiResponse<List<FreightTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var templates = await _freightTemplateAppService.ListAsync(page, pageSize, ct);
        return Ok(ApiResponse.Success(templates));
    }

    /// <summary>查询当前卖家的运费模板。</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpGet("api/seller/freight-templates/mine")]
    [ProducesResponseType(typeof(ApiResponse<FreightTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsync(CancellationToken ct)
    {
        var template = await _freightTemplateAppService.GetBySellerIdAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(template));
    }

    // ========== 物流轨迹查询 ==========

    /// <summary>查询订单物流轨迹。订单未发货（LogisticsNo 为空）时返回空数据。</summary>
    [Authorize(Roles = "Buyer,Seller,Admin")]
    [HttpGet("api/orders/{id:guid}/logistics")]
    [ProducesResponseType(typeof(ApiResponse<LogisticsTrackingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogisticsTrackingAsync(Guid id, CancellationToken ct)
    {
        var order = await _orderAppService.GetByIdAsync(id, ct);

        if (string.IsNullOrEmpty(order.LogisticsNo))
        {
            return Ok(ApiResponse.Success(new LogisticsTrackingDto()));
        }

        var tracking = await _logisticsTrackingService.GetTrackingAsync(order.LogisticsNo, ct);
        return Ok(ApiResponse.Success(tracking));
    }
}

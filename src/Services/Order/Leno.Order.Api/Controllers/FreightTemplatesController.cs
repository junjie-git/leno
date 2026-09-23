using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Queries;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Order.Api.Controllers;

/// <summary>
/// 运费模板控制器。
/// 卖家/管理端（/api/seller/freight-templates）：运费模板 CRUD、区域规则更新、启停、查询，需 Seller/Admin 角色。
/// 另含物流轨迹查询端点（/api/orders/{id}/logistics-trace），需 Buyer/Seller/Admin 角色。
/// 物流轨迹读路径已迁移到 CQRS <see cref="IQueryHandler{TQuery,TResult}"/>（双轨下线 E1，2026-09-21）。
/// </summary>
[ApiController]
public sealed class FreightTemplatesController : OrderControllerBase
{
    private readonly IFreightTemplateAppService _freightTemplateAppService;
    private readonly IQueryHandler<LogisticsTraceQuery, LogisticsTraceResult?> _logisticsTraceQueryHandler;

    public FreightTemplatesController(
        ICurrentUserContext currentUser,
        IFreightTemplateAppService freightTemplateAppService,
        IQueryHandler<LogisticsTraceQuery, LogisticsTraceResult?> logisticsTraceQueryHandler)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(freightTemplateAppService);
        ArgumentNullException.ThrowIfNull(logisticsTraceQueryHandler);
        _freightTemplateAppService = freightTemplateAppService;
        _logisticsTraceQueryHandler = logisticsTraceQueryHandler;
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

    /// <summary>
    /// 查询订单物流轨迹，验证物流公司支持轨迹查询，失败时返回缓存数据并带警告标识。
    /// <para>
    /// 读路径已迁移到 CQRS <see cref="LogisticsTraceQueryHandler"/>；响应仍映射为
    /// <see cref="LogisticsTrackingDto"/> 以**保全既有 API 契约**（字段名与语义不变）。
    /// 订单不存在时返回 404，与原 <c>OrderAppService</c> 抛 <c>ORDER_NOT_FOUND</c> 的行为一致。
    /// </para>
    /// </summary>
    [Authorize(Roles = "Buyer,Seller,Admin")]
    [HttpGet("api/orders/{id:guid}/logistics-trace")]
    [ProducesResponseType(typeof(ApiResponse<LogisticsTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogisticsTraceAsync(Guid id, CancellationToken ct)
    {
        var result = await _logisticsTraceQueryHandler
            .HandleAsync(new LogisticsTraceQuery { OrderId = id }, ct)
            .ConfigureAwait(false);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "订单不存在"));
        }

        var tracking = new LogisticsTrackingDto
        {
            LogisticsNo = result.TrackingNo ?? string.Empty,
            CompanyCode = result.LogisticsCompany ?? string.Empty,
            Nodes = result.Nodes.Select(n => new LogisticsTrackingNode
            {
                Description = n.Description,
                OccurredAt = n.Time,
                Location = n.Location ?? string.Empty
            }).ToList(),
            IsFromCache = result.IsFromCache,
            HasWarning = result.HasWarning
        };

        return Ok(ApiResponse.Success(tracking));
    }
}

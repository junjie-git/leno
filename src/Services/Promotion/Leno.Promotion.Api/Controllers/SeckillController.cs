using Leno.Infrastructure.Auth;
using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Promotion.Api.Controllers;

/// <summary>
/// 秒杀控制器。
/// 运营端（/api/admin/seckill）：创建/激活/关闭秒杀活动、分页查询，需 Operator/Admin 角色。
/// 买家端（/api/seckill）：活动列表、详情、秒杀下单，需 Buyer 角色。
/// </summary>
[ApiController]
public sealed class SeckillController : PromotionControllerBase
{
    private readonly ISeckillAppService _seckillAppService;

    public SeckillController(ICurrentUserContext currentUser, ISeckillAppService seckillAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(seckillAppService);
        _seckillAppService = seckillAppService;
    }

    // ========== 运营端 ==========

    /// <summary>创建秒杀活动（待生效态，需激活后初始化 Redis 库存）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/seckill/activities")]
    [ProducesResponseType(typeof(ApiResponse<SeckillActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSeckillActivityDto dto, CancellationToken ct)
    {
        var activity = await _seckillAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(activity));
    }

    /// <summary>激活秒杀活动（初始化 Redis 多 SKU 库存）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/seckill/activities/{activityId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAsync(Guid activityId, CancellationToken ct)
    {
        await _seckillAppService.ActivateAsync(activityId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>关闭秒杀活动（含 Redis 库存回写 DB）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/seckill/activities/{activityId:guid}/close")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseAsync(Guid activityId, CancellationToken ct)
    {
        await _seckillAppService.CloseActivityWithStockWriteBackAsync(activityId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// 分页查询秒杀活动，支持按名称模糊、状态精确可选过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词。</param>
    /// <param name="status">活动状态精确匹配。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页秒杀活动列表与总记录数。</returns>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/seckill/activities")]
    [ProducesResponseType(typeof(ApiResponse<SeckillListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? name,
        [FromQuery] SeckillStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _seckillAppService.QueryAsync(name, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 买家端 ==========

    /// <summary>查询当前进行中的秒杀活动列表。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/seckill/activities")]
    [ProducesResponseType(typeof(ApiResponse<List<SeckillActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAsync(CancellationToken ct)
    {
        var activities = await _seckillAppService.GetActiveAsync(ct);
        return Ok(ApiResponse.Success(activities));
    }

    /// <summary>获取秒杀活动详情（含 Redis 实时库存）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/seckill/activities/{activityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SeckillActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await _seckillAppService.GetByIdAsync(activityId, ct);
        return Ok(ApiResponse.Success(activity));
    }

    /// <summary>
    /// 秒杀下单（异步模式）：Redis 原子预扣库存 + 限购校验 → 发布 SeckillOrderCreatedEvent。
    /// 前端凭返回的 OrderId 轮询订单域获取结果。
    /// 支持通过 skuId 指定具体 SKU 下单。
    /// </summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/seckill/activities/{activityId:guid}/place")]
    [ProducesResponseType(typeof(ApiResponse<SeckillPlaceOrderResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PlaceOrderAsync(Guid activityId, [FromBody] SeckillPlaceOrderDto dto, CancellationToken ct)
    {
        var result = await _seckillAppService.PlaceOrderAsync(activityId, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 秒杀下单（带 skuId 参数），支持多 SKU 秒杀场景。
    /// </summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/seckill/{activityId:guid}/order")]
    [ProducesResponseType(typeof(ApiResponse<SeckillPlaceOrderResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PlaceOrderWithSkuIdAsync(Guid activityId, [FromBody] SeckillPlaceOrderDto dto, CancellationToken ct)
    {
        var result = await _seckillAppService.PlaceOrderAsync(activityId, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(result));
    }
}
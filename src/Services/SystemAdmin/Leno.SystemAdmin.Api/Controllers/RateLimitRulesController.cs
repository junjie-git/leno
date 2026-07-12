using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 限流规则管理控制器（CRUD + 启用/停用）。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class RateLimitRulesController : SystemAdminControllerBase
{
    private readonly IRateLimitRuleAppService _appService;

    public RateLimitRulesController(ICurrentUserContext currentUser, IRateLimitRuleAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>分页查询限流规则，支持 API 路径与启用状态过滤。</summary>
    [HttpGet("api/admin/rate-limit-rules")]
    [ProducesResponseType(typeof(ApiResponse<RateLimitRuleListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? targetApi,
        [FromQuery] bool? enabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _appService.QueryAsync(targetApi, enabled, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建限流规则。</summary>
    [HttpPost("api/admin/rate-limit-rules")]
    [ProducesResponseType(typeof(ApiResponse<RateLimitRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveRateLimitRuleDto dto, CancellationToken ct)
    {
        var result = await _appService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = result.RuleId }, ApiResponse.Success(result));
    }

    /// <summary>按标识获取限流规则详情。</summary>
    [HttpGet("api/admin/rate-limit-rules/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RateLimitRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var rule = await _appService.GetByIdAsync(id, ct);
        if (rule is null)
        {
            return NotFound(ApiResponse.Fail(404, "限流规则不存在"));
        }

        return Ok(ApiResponse.Success(rule));
    }

    /// <summary>更新限流规则（乐观并发控制）。</summary>
    [HttpPut("api/admin/rate-limit-rules/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RateLimitRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] SaveRateLimitRuleDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _appService.UpdateAsync(id, dto, ct);
            return Ok(ApiResponse.Success(result));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("不存在"))
        {
            return NotFound(ApiResponse.Fail(404, ex.Message));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse.Fail(409, "数据已被其他用户修改，请刷新后重试"));
        }
    }

    /// <summary>启用限流规则。</summary>
    [HttpPost("api/admin/rate-limit-rules/{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _appService.EnableAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("不存在"))
        {
            return NotFound(ApiResponse.Fail(404, ex.Message));
        }
    }

    /// <summary>停用限流规则。</summary>
    [HttpPost("api/admin/rate-limit-rules/{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _appService.DisableAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("不存在"))
        {
            return NotFound(ApiResponse.Fail(404, ex.Message));
        }
    }
}
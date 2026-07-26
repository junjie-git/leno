using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 告警静默规则控制器，提供静默规则的创建、查询与删除能力。
/// 静默规则由 Alertmanager 维护，匹配的告警在静默期内不再通知。
/// </summary>
[ApiController]
[Route("api/admin/alerts/silences")]
[Authorize(Roles = "Admin")]
public sealed class AlertSilencesController : SystemAdminControllerBase
{
    private readonly IAlertSilenceAppService _appService;

    public AlertSilencesController(ICurrentUserContext currentUser, IAlertSilenceAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>
    /// 创建静默规则。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 4 行。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AlertSilenceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAlertSilenceDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var createdBy = GetCurrentOperatorId().ToString();
        try
        {
            var result = await _appService.CreateAsync(dto, createdBy, ct);
            return CreatedAtAction(nameof(GetListAsync), null, ApiResponse.Success(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// 查询静默规则列表。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 5 行。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AlertSilenceListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetListAsync(CancellationToken ct)
    {
        var result = await _appService.QueryAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 按 ID 删除静默规则。
    /// 设计文档 04-runtime-ops/alert-management.md §3 主要 API 第 6 行。
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _appService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(400, ex.Message));
        }
    }
}

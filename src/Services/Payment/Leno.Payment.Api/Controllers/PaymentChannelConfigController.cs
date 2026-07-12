using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 支付渠道配置管理控制器。
/// GET /api/admin/payment-channels：列出所有渠道配置
/// GET /api/admin/payment-channels/{id}：获取单个配置
/// PUT /api/admin/payment-channels/{id}：更新配置项值
/// POST /api/admin/payment-channels/{id}/enable：启用配置
/// POST /api/admin/payment-channels/{id}/disable：禁用配置
/// 需 Admin/Operator 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Admin,Operator")]
[Route("api/admin/payment-channels")]
public sealed class PaymentChannelConfigController : PaymentControllerBase
{
    private readonly IPaymentChannelConfigAppService _appService;

    public PaymentChannelConfigController(
        ICurrentUserContext currentUser,
        IPaymentChannelConfigAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>获取所有渠道配置项列表。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentChannelConfigDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var result = await _appService.GetAllAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按标识获取配置项详情。</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentChannelConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _appService.GetByIdAsync(id, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "配置项不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新配置项值。</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentChannelConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdatePaymentChannelConfigDto dto, CancellationToken ct)
    {
        var result = await _appService.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用配置项。</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        await _appService.EnableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>禁用配置项。</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid id, CancellationToken ct)
    {
        await _appService.DisableAsync(id, ct);
        return Ok(ApiResponse.Success());
    }
}
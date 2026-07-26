using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserCenter.Application;
using Leno.UserCenter.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserCenter.Api.Controllers;

/// <summary>
/// 收货地址控制器，提供地址增删改查与默认地址切换端点。
/// 全部端点需认证，地址归属校验在应用层执行。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// 端点契约对齐 docs/design-prompts/buyer-app/13-profile/addresses.md。
/// </summary>
[Authorize]
[ApiController]
[Route("api/users/me/addresses")]
public sealed class AddressesController : UserCenterControllerBase
{
    private readonly IAddressAppService _addressAppService;

    public AddressesController(ICurrentUserContext currentUser, IAddressAppService addressAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(addressAppService);
        _addressAppService = addressAppService;
    }

    /// <summary>查询当前用户地址列表（默认地址优先）。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AddressDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var addresses = await _addressAppService.ListAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(addresses));
    }

    /// <summary>新增收货地址。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveAddressDto dto, CancellationToken ct)
    {
        var address = await _addressAppService.CreateAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(address));
    }

    /// <summary>修改收货地址。</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] SaveAddressDto dto, CancellationToken ct)
    {
        var address = await _addressAppService.UpdateAsync(GetCurrentUserId(), id, dto, ct);
        return Ok(ApiResponse.Success(address));
    }

    /// <summary>软删除收货地址。</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await _addressAppService.DeleteAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success("地址已删除"));
    }

    /// <summary>将指定地址设为默认。</summary>
    [HttpPost("{id:guid}/default")]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetDefaultAsync(Guid id, CancellationToken ct)
    {
        var address = await _addressAppService.SetDefaultAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success(address));
    }
}

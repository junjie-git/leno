using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 卖家端店铺控制器，提供入驻申请提交与当前店铺资料查询、维护端点。
/// 全部端点需认证。
/// </summary>
[Authorize]
[ApiController]
[Route("api/shops")]
public sealed class ShopsController : SellerShopControllerBase
{
    private readonly IShopAppService _shopAppService;

    public ShopsController(ICurrentUserContext currentUser, IShopAppService shopAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(shopAppService);
        _shopAppService = shopAppService;
    }

    /// <summary>卖家提交入驻申请（创建店铺与卖家档案并置待审核）。</summary>
    [HttpPost("application")]
    [ProducesResponseType(typeof(ApiResponse<ShopDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitApplicationAsync([FromBody] SubmitShopApplicationDto dto, CancellationToken ct)
    {
        var shop = await _shopAppService.SubmitShopApplicationAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(shop));
    }

    /// <summary>查询当前卖家的店铺资料。</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<ShopDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyShopAsync(CancellationToken ct)
    {
        var shop = await _shopAppService.GetMyShopAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(shop));
    }

    /// <summary>更新当前卖家的店铺基础信息、Logo 与联系方式。</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<ShopDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyShopAsync([FromBody] UpdateShopInfoDto dto, CancellationToken ct)
    {
        var updated = await _shopAppService.UpdateMyShopInfoAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>卖家上传店铺资质。</summary>
    [HttpPost("me/qualifications")]
    [ProducesResponseType(typeof(ApiResponse<QualificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitQualificationAsync(
        [FromForm] SubmitQualificationDto dto,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "资质文件不可为空"));
        }

        using var stream = file.OpenReadStream();
        var qualification = await _shopAppService.SubmitMyQualificationAsync(
            GetCurrentUserId(), dto, stream, file.FileName, file.ContentType, ct);
        return Ok(ApiResponse.Success(qualification));
    }
}

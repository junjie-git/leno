using Leno.Promotion.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Promotion.Api.Controllers;

/// <summary>
/// 促销域内部接口控制器，供订单域等服务间调用。
/// 路由前缀 <c>internal/</c> 由 <c>InternalApiKeyMiddleware</c> 校验 <c>X-Internal-Key</c> 请求头。
/// </summary>
[ApiController]
public sealed class InternalPromotionsController : ControllerBase
{
    private readonly IPromotionCalculateAppService _calculateService;

    public InternalPromotionsController(IPromotionCalculateAppService calculateService)
    {
        ArgumentNullException.ThrowIfNull(calculateService);
        _calculateService = calculateService;
    }

    /// <summary>试算用户当前订单可用的优惠总金额。</summary>
    [HttpPost("internal/promotions/calculate")]
    [ProducesResponseType(typeof(ApiResponse<DiscountResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateAsync([FromBody] CalculateDiscountDto input, CancellationToken ct)
    {
        var result = await _calculateService.CalculateDiscountAsync(input, ct);
        return Ok(ApiResponse.Success(result));
    }
}

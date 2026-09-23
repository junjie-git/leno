using Leno.Promotion.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 促销折扣试算服务实现 —— **规则引擎唯一路径**（双轨下线 D3，2026-09-23）。
/// <para>
/// 旧硬编码路径（满减 + 优惠券各自按原价计算后直接相加）与开关 <c>Promotion:UseRuleEngine</c> 已删除：
/// 试算经 <see cref="PromotionRuleContext"/> 映射后交由 <see cref="IRuleEngine"/> 编排全部规则
/// （满减 <c>FullReductionRule</c> / 优惠券 <c>CouponRule</c> / 秒杀 <c>SeckillDiscountRule</c>），
/// 叠加语义由规则定义的 Stacking 策略决定（默认 <c>Stackable</c>，逐级按剩余金额评估）。
/// </para>
/// <para>
/// 语义基准（差分对照测试固化，见 <c>RuleEngineDiscountRegressionTests</c>）：
/// 券门槛按<b>前序规则扣减后的剩余金额</b>判定 —— 与旧路径按原价判定的差异为有意变更。
/// </para>
/// </summary>
public sealed class PromotionCalculateAppService : IPromotionCalculateAppService
{
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogger<PromotionCalculateAppService> _logger;

    public PromotionCalculateAppService(
        IRuleEngine ruleEngine,
        ILogger<PromotionCalculateAppService> logger)
    {
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DiscountResultDto> CalculateDiscountAsync(CalculateDiscountDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(input));
        }

        var context = MapToRuleContext(input);
        var result = await _ruleEngine.EvaluateAsync(context, ct).ConfigureAwait(false);

        _logger.LogDebug(
            "规则引擎试算完成 UserId={UserId} SubTotal={SubTotal} Discount={Discount}",
            input.UserId, context.SubTotal, result.TotalDiscountAmount);

        return new DiscountResultDto
        {
            TotalDiscountAmount = result.TotalDiscountAmount,
            Currency = result.Currency
        };
    }

    /// <summary>
    /// 将对外 DTO <see cref="CalculateDiscountDto"/> 映射为规则引擎上下文 <see cref="PromotionRuleContext"/>。
    /// 映射规则：
    /// <list type="bullet">
    /// <item>UserId：<see cref="CalculateDiscountDto.UserId"/>（Guid）放入 <see cref="PromotionRuleContext.Attributes"/>["UserGuid"]，供 <c>CouponRule</c> 解析；</item>
    /// <item>Items：每行 <see cref="DiscountItemInput.Subtotal"/> 映射为 Quantity=1、UnitPrice=Subtotal 的 <see cref="CartItemContext"/>，保留行小计；</item>
    /// <item>SubTotal：所有行小计之和；</item>
    /// <item>SellerId：0（多卖家聚合，DTO 未携带卖家维度）；</item>
    /// <item>CouponCode/SeckillActivityId：null（DTO 未携带）。</item>
    /// </list>
    /// </summary>
    private static PromotionRuleContext MapToRuleContext(CalculateDiscountDto input)
    {
        var items = (input.Items ?? Enumerable.Empty<DiscountItemInput>())
            .Select(i => new CartItemContext
            {
                SkuId = i.SkuId,
                Quantity = 1,
                UnitPrice = i.Subtotal,
                CategoryCode = null
            })
            .ToList();

        var subTotal = items.Sum(i => i.Subtotal);

        var attributes = new Dictionary<string, string>
        {
            ["UserGuid"] = input.UserId.ToString()
        };

        return new PromotionRuleContext
        {
            UserId = 0,
            SellerId = 0,
            Items = items,
            SubTotal = subTotal,
            CouponCode = null,
            SeckillActivityId = null,
            Attributes = attributes
        };
    }
}

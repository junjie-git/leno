namespace Leno.Promotion.Application;

/// <summary>
/// 促销折扣试算服务，供订单域调用以计算用户适用的优惠总金额。
/// </summary>
public interface IPromotionCalculateAppService
{
    Task<DiscountResultDto> CalculateDiscountAsync(CalculateDiscountDto input, CancellationToken ct = default);
}

/// <summary>折扣试算输入。</summary>
public sealed class CalculateDiscountDto
{
    public Guid UserId { get; set; }
    public List<DiscountItemInput> Items { get; set; } = [];
}

/// <summary>试算订单行输入。</summary>
public sealed class DiscountItemInput
{
    public Guid SkuId { get; set; }
    public decimal Subtotal { get; set; }
}

/// <summary>折扣试算结果。</summary>
public sealed class DiscountResultDto
{
    public decimal TotalDiscountAmount { get; set; }
    public string Currency { get; set; } = "CNY";
}

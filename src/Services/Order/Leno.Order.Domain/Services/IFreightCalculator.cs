namespace Leno.Order.Domain.Services;

/// <summary>
/// 运费计算器接口，按卖家与区域计算订单运费。
/// 实现位于基础设施层，加载卖家 <see cref="Aggregates.FreightTemplate"/> 并委托其 <c>CalculateFreight</c> 计价。
/// </summary>
public interface IFreightCalculator
{
    /// <summary>
    /// 计算运费。
    /// </summary>
    /// <param name="sellerId">卖家标识。</param>
    /// <param name="regionCode">收货区域编码。</param>
    /// <param name="quantity">计价数量（件数或重量）。</param>
    /// <param name="orderAmount">订单金额，用于判断包邮。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>运费金额。</returns>
    Task<decimal> CalculateAsync(Guid sellerId, string regionCode, int quantity, decimal orderAmount, CancellationToken ct = default);
}

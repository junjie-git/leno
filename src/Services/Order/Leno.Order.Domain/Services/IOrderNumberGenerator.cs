namespace Leno.Order.Domain.Services;

/// <summary>
/// 订单号生成器接口，生成全局唯一且业务可读的订单编号。
/// 实现位于基础设施层，基于时间戳与机器位/序列位保证唯一性与可读性。
/// </summary>
public interface IOrderNumberGenerator
{
    /// <summary>
    /// 生成订单编号。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>订单编号字符串。</returns>
    Task<string> GenerateAsync(CancellationToken ct = default);
}

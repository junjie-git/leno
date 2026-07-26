namespace Leno.Payment.Application.Services;

/// <summary>
/// 订单支付上下文防腐层查询结果。
/// 由 <see cref="IPaymentOrderAntiCorruptionService.GetOrderPaymentContextAsync"/> 返回，
/// 提供发起支付所需的最小订单视图：归属、可支付状态、应付金额与币种。
/// 应用层据此校验订单存在性、买家归属、可支付状态与金额一致性（INV-PAY-01）。
/// </summary>
public sealed class OrderPaymentContext
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>订单归属买家标识，用于与当前登录用户比对防止越权发起他人订单支付。</summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// 订单是否处于可发起支付的待支付态。
    /// 由防腐层按订单域 OrderStatus 枚举判定：仅 <c>PendingPayment</c> 视为可支付。
    /// </summary>
    public bool IsPayable { get; init; }

    /// <summary>订单应付总额（元），用于校验支付单金额与订单应付一致（INV-PAY-01）。</summary>
    public decimal Amount { get; init; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";
}

/// <summary>
/// 订单支付上下文防腐层接口。
/// 屏蔽订单域的具体实现（gRPC / HttpClient），应用层仅依赖此抽象。
/// 实现由基础设施层提供（如 <c>GrpcPaymentOrderAntiCorruptionService</c>），
/// 通过订单域 OrderInternalService 获取订单状态、归属与应付金额，供 POST /api/payments 同步发起支付校验。
/// </summary>
/// <remarks>
/// 防腐层职责：
/// <list type="bullet">
/// <item>调用订单域内部查询接口获取订单状态、归属用户、应付金额；</item>
/// <item>将订单域的 OrderStatus 枚举映射为本上下文的 <see cref="IsPayable"/> 布尔值，避免跨域枚举依赖；</item>
/// <item>网络故障或订单域返回异常时抛 <c>AntiCorruptionException</c>，由全局异常中间件映射为 503。</item>
/// </list>
/// </remarks>
public interface IPaymentOrderAntiCorruptionService
{
    /// <summary>
    /// 按订单标识查询订单支付上下文。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// 订单支付上下文；订单不存在时返回 <c>null</c>（由应用层映射为 404）。
    /// 订单域远程调用失败时抛 <c>AntiCorruptionException</c>（由全局异常中间件映射为 503）。
    /// </returns>
    Task<OrderPaymentContext?> GetOrderPaymentContextAsync(Guid orderId, CancellationToken ct = default);
}

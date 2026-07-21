using Leno.SharedKernel.Exceptions;

namespace Leno.Cart.Domain.Exceptions;

/// <summary>
/// 购物车域基础设施故障异常（Redis / 数据库等不可用）。
/// 与 <see cref="CartDomainException"/> 区分业务异常不同，本异常表达基础设施层故障，
/// 携带错误码（如 <c>CART_REDIS_UNAVAILABLE</c>）由全局异常中间件映射为 HTTP 503。
/// </summary>
public sealed class CartInfrastructureException : DomainException
{
    public CartInfrastructureException(string message, string errorCode = "CART_INFRA_UNAVAILABLE")
        : base(message, errorCode)
    {
    }

    public CartInfrastructureException(string message, Exception innerException, string errorCode = "CART_INFRA_UNAVAILABLE")
        : base(message, innerException, errorCode)
    {
    }
}

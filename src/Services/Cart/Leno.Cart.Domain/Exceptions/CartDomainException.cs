using Leno.SharedKernel.Exceptions;

namespace Leno.Cart.Domain.Exceptions;

/// <summary>
/// 购物车域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class CartDomainException : DomainException
{
    public CartDomainException(string message, string errorCode = "CART_ERROR")
        : base(message, errorCode)
    {
    }
}

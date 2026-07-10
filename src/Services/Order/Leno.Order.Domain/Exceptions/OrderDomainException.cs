using Leno.SharedKernel.Exceptions;

namespace Leno.Order.Domain.Exceptions;

/// <summary>
/// 订单域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class OrderDomainException : DomainException
{
    public OrderDomainException(string message, string errorCode = "ORDER_ERROR", int httpStatusCode = 400)
        : base(message, errorCode, httpStatusCode)
    {
    }
}

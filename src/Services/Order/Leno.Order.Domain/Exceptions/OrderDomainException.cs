using Leno.SharedKernel.Exceptions;

namespace Leno.Order.Domain.Exceptions;

/// <summary>
/// 订单域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class OrderDomainException : DomainException
{
    public OrderDomainException(string message, string errorCode = "ORDER_ERROR")
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// 包装远程/底层异常，保留原始堆栈用于排障，错误码与 HTTP 状态码由调用方指定。
    /// </summary>
    public OrderDomainException(string message, Exception innerException, string errorCode = "ORDER_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}

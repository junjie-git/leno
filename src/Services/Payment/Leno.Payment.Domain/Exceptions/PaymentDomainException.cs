using Leno.SharedKernel.Exceptions;

namespace Leno.Payment.Domain.Exceptions;

/// <summary>
/// 支付域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class PaymentDomainException : DomainException
{
    public PaymentDomainException(string message, string errorCode = "PAYMENT_ERROR")
        : base(message, errorCode)
    {
    }
}

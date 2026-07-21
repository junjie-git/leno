using Leno.SharedKernel.Exceptions;

namespace Leno.Promotion.Domain.Exceptions;

/// <summary>
/// 促销域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class PromotionDomainException : DomainException
{
    public PromotionDomainException(string message, string errorCode = "PROMOTION_ERROR")
        : base(message, errorCode)
    {
    }

    public PromotionDomainException(string message, string errorCode, Exception innerException)
        : base(message, innerException, errorCode)
    {
    }
}

using Leno.SharedKernel.Exceptions;

namespace Leno.ReviewAfterSales.Domain.Exceptions;

/// <summary>
/// 评价与售后域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class ReviewDomainException : DomainException
{
    public ReviewDomainException(string message, string errorCode = "REVIEW_ERROR")
        : base(message, errorCode)
    {
    }
}

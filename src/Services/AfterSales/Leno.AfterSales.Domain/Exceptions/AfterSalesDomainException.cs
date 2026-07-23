using Leno.SharedKernel.Exceptions;

namespace Leno.AfterSales.Domain.Exceptions;

/// <summary>
/// 售后域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class AfterSalesDomainException : DomainException
{
    public AfterSalesDomainException(string message, string errorCode = "AFTERSALES_ERROR")
        : base(message, errorCode)
    {
    }
}

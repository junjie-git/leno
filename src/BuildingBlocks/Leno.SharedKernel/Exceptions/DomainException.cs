namespace Leno.SharedKernel.Exceptions;

/// <summary>
/// 领域异常基类，携带业务错误码与映射的 HTTP 状态码。
/// 业务校验失败应抛出继承此类的异常，由全局异常中间件转换为标准响应。
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>业务错误码，便于前端识别与处理。</summary>
    public string ErrorCode { get; }

    /// <summary>映射到的 HTTP 状态码（默认 400 Bad Request）。</summary>
    public int HttpStatusCode { get; }

    protected DomainException(string message, string errorCode = "DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }

    protected DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}

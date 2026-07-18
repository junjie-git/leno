namespace Leno.SharedKernel.Exceptions;

/// <summary>
/// 领域异常基类，携带业务错误码。
/// 业务校验失败应抛出继承此类的异常，由全局异常中间件根据 ErrorCode 映射为 HTTP 状态码后转换为标准响应。
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>业务错误码，便于前端识别与处理。</summary>
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

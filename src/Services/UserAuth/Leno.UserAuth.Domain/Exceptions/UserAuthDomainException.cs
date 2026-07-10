using Leno.SharedKernel.Exceptions;

namespace Leno.UserAuth.Domain.Exceptions;

/// <summary>
/// 用户与认证授权域领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class UserAuthDomainException : DomainException
{
    public UserAuthDomainException(string message, string errorCode = "USER_AUTH_DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message, errorCode, httpStatusCode)
    {
    }

    public UserAuthDomainException(string message, Exception innerException, string errorCode = "USER_AUTH_DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message, innerException, errorCode, httpStatusCode)
    {
    }
}

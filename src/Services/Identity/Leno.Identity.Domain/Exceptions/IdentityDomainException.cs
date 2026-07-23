using Leno.SharedKernel.Exceptions;

namespace Leno.Identity.Domain.Exceptions;

/// <summary>
/// Identity BC 领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class IdentityDomainException : DomainException
{
    public IdentityDomainException(string message, string errorCode = "IDENTITY_DOMAIN_ERROR")
        : base(message, errorCode)
    {
    }

    public IdentityDomainException(string message, Exception innerException, string errorCode = "IDENTITY_DOMAIN_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}

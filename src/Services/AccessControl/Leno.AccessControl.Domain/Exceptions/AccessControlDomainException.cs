using Leno.SharedKernel.Exceptions;

namespace Leno.AccessControl.Domain.Exceptions;

/// <summary>
/// AccessControl BC 领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class AccessControlDomainException : DomainException
{
    public AccessControlDomainException(string message, string errorCode = "ACCESS_CONTROL_DOMAIN_ERROR")
        : base(message, errorCode)
    {
    }

    public AccessControlDomainException(string message, Exception innerException, string errorCode = "ACCESS_CONTROL_DOMAIN_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}

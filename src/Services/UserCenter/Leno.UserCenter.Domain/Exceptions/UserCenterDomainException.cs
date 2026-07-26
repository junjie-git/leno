using Leno.SharedKernel.Exceptions;

namespace Leno.UserCenter.Domain.Exceptions;

/// <summary>
/// 用户中心域领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class UserCenterDomainException : DomainException
{
    public UserCenterDomainException(string message, string errorCode = "USER_CENTER_DOMAIN_ERROR")
        : base(message, errorCode)
    {
    }

    public UserCenterDomainException(string message, Exception innerException, string errorCode = "USER_CENTER_DOMAIN_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}

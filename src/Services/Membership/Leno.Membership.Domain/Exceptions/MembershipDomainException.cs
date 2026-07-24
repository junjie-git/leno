using Leno.SharedKernel.Exceptions;

namespace Leno.Membership.Domain.Exceptions;

/// <summary>
/// 会员域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class MembershipDomainException : DomainException
{
    public MembershipDomainException(string message, string errorCode = "MEMBERSHIP_ERROR")
        : base(message, errorCode)
    {
    }
}

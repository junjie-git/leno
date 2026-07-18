using Leno.SharedKernel.Exceptions;

namespace Leno.UserAuth.Application.Exceptions;

/// <summary>
/// 应用层输入校验异常，继承领域异常基类以经全局异常中间件统一映射为 400。
/// </summary>
public sealed class UserAuthValidationException : DomainException
{
    public UserAuthValidationException(string message)
        : base(message, "USER_AUTH_VALIDATION_ERROR")
    {
    }

    public UserAuthValidationException(IEnumerable<string> errors)
        : base(string.Join(" | ", errors), "USER_AUTH_VALIDATION_ERROR")
    {
    }
}

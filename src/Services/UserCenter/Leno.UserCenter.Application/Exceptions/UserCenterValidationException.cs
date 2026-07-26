using Leno.SharedKernel.Exceptions;

namespace Leno.UserCenter.Application.Exceptions;

/// <summary>
/// 应用层输入校验异常，继承领域异常基类以经全局异常中间件统一映射为 400。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class UserCenterValidationException : DomainException
{
    public UserCenterValidationException(string message)
        : base(message, "USER_CENTER_VALIDATION_ERROR")
    {
    }

    public UserCenterValidationException(IEnumerable<string> errors)
        : base(string.Join(" | ", errors), "USER_CENTER_VALIDATION_ERROR")
    {
    }
}

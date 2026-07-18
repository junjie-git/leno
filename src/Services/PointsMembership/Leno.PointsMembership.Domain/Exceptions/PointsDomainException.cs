using Leno.SharedKernel.Exceptions;

namespace Leno.PointsMembership.Domain.Exceptions;

/// <summary>
/// 积分会员域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
public sealed class PointsDomainException : DomainException
{
    public PointsDomainException(string message, string errorCode = "POINTS_ERROR")
        : base(message, errorCode)
    {
    }
}

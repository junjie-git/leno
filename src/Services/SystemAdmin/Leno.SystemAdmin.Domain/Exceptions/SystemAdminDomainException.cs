using Leno.SharedKernel.Exceptions;

namespace Leno.SystemAdmin.Domain.Exceptions;

/// <summary>
/// 系统管理域领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class SystemAdminDomainException : DomainException
{
    /// <summary>
    /// 初始化领域异常，默认 HTTP 状态码 400。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <param name="errorCode">业务错误码。</param>
    public SystemAdminDomainException(string message, string errorCode)
        : base(message, errorCode)
    {
    }
}

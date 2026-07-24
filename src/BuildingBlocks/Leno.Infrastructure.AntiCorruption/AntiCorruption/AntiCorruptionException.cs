using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层远程调用异常（M4.1）。
/// 由 <see cref="AntiCorruptionBase"/> 在网络故障、超时、非成功状态码等场景统一抛出，
/// 携带业务错误码（如 <c>{SERVICE}_UNAVAILABLE</c>、<c>{SERVICE}_REMOTE_FAILED</c>），
/// 由全局异常中间件根据 ErrorCode 映射为 HTTP 状态码后转换为标准响应。
/// </summary>
public sealed class AntiCorruptionException : DomainException
{
    public AntiCorruptionException(string message, string errorCode = "ANTICORRUPTION_ERROR")
        : base(message, errorCode)
    {
    }

    public AntiCorruptionException(string message, Exception innerException, string errorCode = "ANTICORRUPTION_ERROR")
        : base(message, innerException, errorCode)
    {
    }
}

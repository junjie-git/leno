using Leno.SharedKernel.Exceptions;

namespace Leno.PointsMembership.Domain.Exceptions;

/// <summary>
/// 积分会员域业务异常，携带错误码与 HTTP 状态码，由全局异常中间件转换为标准响应。
/// </summary>
/// <remarks>
/// 双轨期弃用标记：此类型所属的 PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。
/// 新代码请使用 <c>Leno.Points.Domain.Exceptions.PointsDomainException</c> 或
/// <c>Leno.Membership.Domain.Exceptions.MembershipDomainException</c>。
/// 双轨期 8 周后下线整个 PointsMembership BC。
/// </remarks>
[Obsolete("PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。双轨期 8 周后下线。新代码请使用 Leno.Points 或 Leno.Membership 命名空间。", DiagnosticId = "LENO_PM_BC_SPLIT")]
public sealed class PointsDomainException : DomainException
{
    public PointsDomainException(string message, string errorCode = "POINTS_ERROR")
        : base(message, errorCode)
    {
    }
}

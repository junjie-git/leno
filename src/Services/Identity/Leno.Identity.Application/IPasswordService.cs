using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 密码管理应用服务接口（Identity BC，Task A2 补齐）。
/// 承载忘记密码与重置密码用例，供 A3 AuthController 消费。
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// 忘记密码：根据账号（邮箱或手机号）发送重置链接/验证码。
    /// 不暴露用户是否存在，账号不存在时静默返回。
    /// </summary>
    /// <param name="email">账号（邮箱或手机号）。</param>
    /// <param name="ct">取消令牌。</param>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 重置密码：校验重置令牌并设置新密码。
    /// 成功后撤销该用户所有刷新令牌，防止旧令牌继续使用。
    /// </summary>
    /// <param name="request">重置密码请求（含令牌与新密码）。</param>
    /// <param name="ct">取消令牌。</param>
    Task ResetPasswordAsync(ResetPasswordDto request, CancellationToken ct = default);
}

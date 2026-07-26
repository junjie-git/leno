using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 双因子认证（TOTP）应用服务接口（Identity BC，Task A2 补齐）。
/// 承载 TOTP 2FA 的启用、确认、禁用与登录二次验证用例，供 A3 AuthController / UsersController 消费。
/// <para>
/// 注意：本接口面向基于 TOTP 共享密钥的认证器 App 模式（Google Authenticator 等），
/// 与 <see cref="Services.TwoFactorAppService"/> 面向短信/邮件验证码模式不同。
/// </para>
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// 双因子认证二次验证（登录流程），验证 TOTP 码。
    /// </summary>
    /// <param name="userId">待验证用户标识。</param>
    /// <param name="code">用户输入的 6 位 TOTP 验证码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>验证通过返回 true。</returns>
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default);

    /// <summary>
    /// 启用双因子认证，生成 TOTP 密钥与 QR 码 URI。
    /// 调用后 2FA 处于待确认状态，需 <see cref="ConfirmTwoFactorAsync"/> 验证后才会真正启用。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含 Base32 密钥与 QR 码 URI 的响应。</returns>
    Task<TwoFactorEnableResponseDto> EnableTwoFactorAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 确认双因子认证，验证 TOTP 码后正式启用。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="code">用户输入的 6 位 TOTP 验证码。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmTwoFactorAsync(Guid userId, string code, CancellationToken ct = default);

    /// <summary>
    /// 禁用双因子认证，清除密钥与启用状态。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task DisableTwoFactorAsync(Guid userId, CancellationToken ct = default);
}

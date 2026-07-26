namespace Leno.Identity.Application;

/// <summary>
/// 外部登录绑定应用服务接口（Identity BC，Task A2 补齐）。
/// 承载外部登录的绑定与解绑用例，供 A3 AccountController 消费。
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// 绑定外部登录到已有账户。
    /// 若同 provider + providerUserId 已被其他用户绑定则抛异常。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="provider">OAuth2 提供方标识（google / wechat / alipay）。</param>
    /// <param name="providerUserId">第三方平台用户唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task BindAsync(Guid userId, string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// 解绑指定提供方的外部登录。
    /// OAuth 用户须至少保留一个外部登录绑定。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task UnbindAsync(Guid userId, string provider, CancellationToken ct = default);
}

using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// OAuth2 第三方授权登录应用服务接口（Identity BC，Task A2 补齐）。
/// 承载获取授权登录 URL 与处理回调用例，供 A3 AuthController 消费。
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// 获取 OAuth2 第三方授权登录 URL。
    /// 流程：校验 redirectUri 白名单 → 查找 OAuthClient 配置 → 生成 CSRF state → 存储 state → 构造授权 URL。
    /// </summary>
    /// <param name="provider">OAuth2 提供方标识（如 google / wechat）。</param>
    /// <param name="redirectUri">回调完成后跳转的业务地址，可空时使用 OAuthClient 默认配置。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>第三方授权页面 URL，前端跳转目标。</returns>
    Task<string> GetLoginUrlAsync(string provider, string? redirectUri, CancellationToken ct = default);

    /// <summary>
    /// 处理 OAuth2 回调，完成登录或注册并签发令牌。
    /// 流程：消费 state 校验 CSRF → 获取 redirectUri → 委托 AuthenticationAppService 完成授权码交换与用户绑定/创建 → 签发令牌。
    /// </summary>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="code">回调返回的授权码。</param>
    /// <param name="state">CSRF 防护 state 参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> HandleCallbackAsync(string provider, string code, string? state, CancellationToken ct = default);
}

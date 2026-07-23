using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 认证应用服务接口，编排登录、刷新与登出用例（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 所有方法在事务边界内提交聚合变更与领域事件（经 Outbox 持久化）。
/// 角色信息由 <see cref="Services.JwtTokenService"/> 通过 AccessControl BC <c>GetUserRoles</c> RPC 获取，
/// Identity BC 本身不再持久化角色数据。
/// </summary>
public interface IAuthenticationAppService
{
    /// <summary>
    /// 账号密码登录。
    /// 流程：按用户名/邮箱查找用户 → 校验账户可登录 → 验证密码 → 重置失败计数 →
    /// 签发刷新令牌 → 发布 <c>UserAuthenticatedEvent</c> → 生成访问令牌（含角色 claims）。
    /// </summary>
    /// <param name="dto">登录请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>
    /// 刷新令牌轮换。
    /// 流程：校验刷新令牌有效 → 撤销旧令牌并记录替换关系 → 签发新刷新令牌 → 生成新访问令牌。
    /// 遵循 RFC 6749 §10.4 的令牌轮换安全实践，防止重放。
    /// </summary>
    /// <param name="dto">刷新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含新访问与新刷新令牌的响应。</returns>
    Task<TokenDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default);

    /// <summary>
    /// 登出，吊销指定用户的所有活跃刷新令牌。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
}

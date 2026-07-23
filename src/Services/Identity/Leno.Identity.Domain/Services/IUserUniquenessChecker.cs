namespace Leno.Identity.Domain.Services;

/// <summary>
/// 用户唯一性校验领域服务抽象，校验用户名/邮箱/手机号全局唯一。
/// 实现位于基础设施层，查询数据库并支持排除自身标识（更新场景）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IUserUniquenessChecker
{
    /// <summary>校验用户名是否唯一。</summary>
    /// <param name="username">待校验用户名。</param>
    /// <param name="excludeUserId">排除的用户标识（更新时传当前用户 ID），注册时传 null。</param>
    /// <param name="ct">取消令牌。</param>
    Task<bool> IsUsernameUniqueAsync(string username, Guid? excludeUserId = null, CancellationToken ct = default);

    /// <summary>校验邮箱是否唯一。</summary>
    Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default);

    /// <summary>校验手机号是否唯一。</summary>
    Task<bool> IsPhoneUniqueAsync(string phone, Guid? excludeUserId = null, CancellationToken ct = default);
}

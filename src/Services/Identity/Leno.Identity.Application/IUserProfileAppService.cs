using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 用户资料应用服务接口（Identity BC，Task A2 补齐）。
/// 承载查询资料、修改资料与修改密码用例，供 A3 UsersController 消费。
/// </summary>
public interface IUserProfileAppService
{
    /// <summary>查询当前用户资料。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>用户资料 DTO。</returns>
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>修改当前用户资料（昵称、头像）。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="request">更新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>更新后的用户资料 DTO。</returns>
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto request, CancellationToken ct = default);

    /// <summary>
    /// 修改当前用户密码。
    /// 成功后撤销该用户所有刷新令牌，强制重新登录。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="request">修改密码请求（含旧密码与新密码）。</param>
    /// <param name="ct">取消令牌。</param>
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken ct = default);
}

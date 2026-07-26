using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 用户域内部查询服务接口（Identity BC，Task A2 补齐）。
/// 供其他微服务获取用户联系方式，供 A4 InternalUsersController 消费。
/// 默认返回脱敏 DTO（<see cref="UserContactsMaskedDto"/>），
/// 完整 PII（<see cref="UserContactsDto"/>）需调用方具备 <c>internal-pii-read</c> 权限。
/// </summary>
public interface IUserInternalAppService
{
    /// <summary>返回脱敏后的联系方式（默认安全）。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>脱敏联系方式 DTO；用户不存在抛异常。</returns>
    Task<UserContactsMaskedDto> GetContactsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>返回未脱敏的联系方式，调用方须自行校验 <c>internal-pii-read</c> 权限。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>完整联系方式 DTO；用户不存在抛异常。</returns>
    Task<UserContactsDto> GetFullContactsAsync(Guid userId, CancellationToken ct = default);
}

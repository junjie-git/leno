namespace Leno.UserAuth.Application;

/// <summary>
/// 用户域内部查询服务，供其他微服务获取用户联系方式。
/// 默认返回脱敏 DTO（<see cref="UserContactsMaskedDto"/>），
/// 完整 PII（<see cref="UserContactsDto"/>）需调用方具备 <c>internal-pii-read</c> 权限。
/// </summary>
public interface IUserInternalQueryService
{
    /// <summary>返回脱敏后的联系方式（默认安全）。</summary>
    Task<UserContactsMaskedDto?> GetMaskedContactsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>返回未脱敏的联系方式，调用方须自行校验 <c>internal-pii-read</c> 权限。</summary>
    Task<UserContactsDto?> GetContactsAsync(Guid userId, CancellationToken ct = default);
}

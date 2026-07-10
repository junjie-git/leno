namespace Leno.UserAuth.Application;

/// <summary>
/// 用户域内部查询服务，供其他微服务获取用户联系方式（未脱敏）。
/// </summary>
public interface IUserInternalQueryService
{
    Task<UserContactsDto?> GetContactsAsync(Guid userId, CancellationToken ct = default);
}

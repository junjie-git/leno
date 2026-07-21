using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 用户域内部查询服务实现，供其他微服务获取用户联系方式。
/// 默认返回脱敏 DTO（<see cref="UserContactsMaskedDto"/>），
/// 完整 PII 由 <see cref="GetContactsAsync"/> 提供，控制器层负责校验 <c>internal-pii-read</c> 权限。
/// </summary>
public sealed class UserInternalQueryService : IUserInternalQueryService
{
    private readonly IUserRepository _userRepository;

    public UserInternalQueryService(IUserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<UserContactsMaskedDto?> GetMaskedContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        return UserContactsMaskedDto.FromContacts(user.Id, user.PhoneNumber, user.Email);
    }

    /// <inheritdoc />
    public async Task<UserContactsDto?> GetContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        return new UserContactsDto
        {
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email
        };
    }
}

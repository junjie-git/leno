using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 用户域内部查询服务实现，供其他微服务获取用户联系方式（未脱敏）。
/// 直接读取聚合根的原始字段，不做脱敏处理。
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
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            Email = user.Email ?? string.Empty
        };
    }
}

using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 用户域内部查询应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// 供其他微服务获取用户联系方式（手机号 / 邮箱），用于订单通知、消息推送、跨域用户解析等场景。
/// 默认返回脱敏 DTO（<see cref="UserContactsMaskedDto"/>），完整 PII（<see cref="UserContactsDto"/>）
/// 需调用方具备 <c>internal-pii-read</c> 权限（由调用方 Controller 层校验，本服务不重复校验）。
/// </para>
/// <para>
/// <b>安全设计：</b>即使内部 API 中间件配置错误，默认响应也不泄露完整 PII，
/// 避免因网关路由错误或权限配置疏漏导致用户隐私数据外泄。
/// </para>
/// </summary>
public sealed class UserInternalAppService : IUserInternalAppService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserInternalAppService> _logger;

    public UserInternalAppService(
        IUserRepository userRepository,
        ILogger<UserInternalAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<UserContactsMaskedDto> GetContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        _logger.LogDebug("内部查询脱敏联系方式，UserId={UserId}", userId);

        return UserContactsMaskedDto.FromContacts(user.Id, user.PhoneNumber, user.Email);
    }

    /// <inheritdoc />
    public async Task<UserContactsDto> GetFullContactsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        // 完整 PII 查询需调用方具备 internal-pii-read 权限（由 Controller 层 [Authorize(Policy = "internal-pii-read")] 校验）
        // 本服务仅负责返回数据，不重复校验权限，避免权限逻辑分散。
        _logger.LogInformation("内部查询完整联系方式，UserId={UserId}", userId);

        return new UserContactsDto
        {
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email
        };
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        var user = await _userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            throw new IdentityDomainException("用户不存在", "USER_NOT_FOUND");
        }

        return user;
    }
}

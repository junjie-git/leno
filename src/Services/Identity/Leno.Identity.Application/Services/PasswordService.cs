using Leno.Identity.Application.Abstractions;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 密码管理应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// ForgotPasswordAsync：按账号查找用户 → 签发一次性重置令牌 → 发布忘记密码领域事件（触发重置邮件/短信）。
/// 不暴露用户是否存在，账号不存在时静默返回，防止账号枚举。
/// </para>
/// <para>
/// ResetPasswordAsync：校验并消费重置令牌 → 查找用户 → 重置密码 → 撤销该用户所有刷新令牌。
/// </para>
/// </summary>
public sealed class PasswordService : IPasswordService
{
    /// <summary>密码重置令牌默认有效期：10 分钟。</summary>
    private static readonly TimeSpan ResetTokenTtl = TimeSpan.FromMinutes(10);

    /// <summary>撤销原因：密码重置。</summary>
    private const string RevokeReasonPasswordReset = "PasswordReset";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordResetTokenStore _passwordResetTokenStore;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordResetTokenStore passwordResetTokenStore,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<PasswordService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _passwordResetTokenStore = passwordResetTokenStore ?? throw new ArgumentNullException(nameof(passwordResetTokenStore));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityDomainException("账号不可为空", "USER_ACCOUNT_EMPTY");
        }

        var account = email.Trim();
        var user = await FindByAccountAsync(account, ct).ConfigureAwait(false);

        // 不暴露用户是否存在，统一返回成功，防止账号枚举
        if (user is null)
        {
            _logger.LogInformation("忘记密码请求：账号未找到，静默返回，Account={Account}", account);
            return;
        }

        if (user.Status == AccountStatus.Disabled)
        {
            // 已禁用账户不发送重置令牌，但仍静默返回
            _logger.LogInformation("忘记密码请求：账户已禁用，静默返回，UserId={UserId}", user.Id);
            return;
        }

        // 生成一次性重置令牌（令牌由存储抽象内部生成，TTL 10 分钟）
        var resetToken = await _passwordResetTokenStore.IssueAsync(user.Id, ResetTokenTtl, ct)
            .ConfigureAwait(false);

        // 发布领域事件，触发重置链接/验证码下发
        user.PublishForgotPasswordRequested(resetToken);

        // 显式 Attach 聚合变更，确保 EF ChangeTracker 与领域事件 Outbox 收集
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("忘记密码请求处理完成，已签发重置令牌，UserId={UserId}", user.Id);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(ResetPasswordDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new IdentityDomainException("重置令牌不可为空", "USER_RESET_TOKEN_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new IdentityDomainException("新密码不可为空", "USER_NEW_PASSWORD_EMPTY");
        }

        // 校验并消费重置令牌（原子 GETDEL 防止重放）
        var userId = await _passwordResetTokenStore.ValidateAndConsumeAsync(request.Token, ct)
            .ConfigureAwait(false);

        if (!userId.HasValue)
        {
            throw new IdentityDomainException("重置令牌无效或已过期", "USER_RESET_TOKEN_INVALID");
        }

        var user = await RequireUserAsync(userId.Value, ct).ConfigureAwait(false);

        if (user.Status == AccountStatus.Disabled)
        {
            throw new IdentityDomainException("账户已被禁用", "USER_DISABLED");
        }

        // 重置密码（令牌已通过邮箱/短信验证，无需旧密码验证）
        user.ResetPassword(_passwordHasher.Hash(request.NewPassword), _passwordHasher);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 撤销该用户所有 RefreshToken，防止旧令牌继续使用
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, RevokeReasonPasswordReset, ct)
            .ConfigureAwait(false);

        _logger.LogInformation("密码重置成功，UserId={UserId}", user.Id);
    }

    private async Task<User?> FindByAccountAsync(string account, CancellationToken ct)
    {
        if (account.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(account, ct).ConfigureAwait(false);
        }

        if (account.StartsWith('+'))
        {
            return await _userRepository.GetByPhoneAsync(account, ct).ConfigureAwait(false);
        }

        return await _userRepository.GetByUsernameAsync(account, ct).ConfigureAwait(false);
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            throw new IdentityDomainException("用户不存在", "USER_NOT_FOUND");
        }

        return user;
    }
}

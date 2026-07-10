using System.Text.RegularExpressions;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Events;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 用户聚合根，账户身份中枢，封装凭证、角色与账户状态机。
/// 所有变更通过行为意图明确的方法完成，禁止外部直接 set 字段。
/// </summary>
public sealed partial class User : AggregateRoot
{
    /// <summary>连续登录失败达此阈值触发账户锁定。</summary>
    public const int MaxFailedLoginCount = 5;

    /// <summary>登录失败锁定的默认时长。</summary>
    public static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(30);

    private readonly List<UserRole> _roles = new();

    /// <summary>用户名，全局唯一，登录账号之一。</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>邮箱，全局唯一（OAuth 注册可空）。</summary>
    public string? Email { get; private set; }

    /// <summary>手机号（E.164），全局唯一（OAuth 注册可空）。</summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>密码哈希（bcrypt），纯 OAuth 用户可空。</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>昵称，1–32 字符。</summary>
    public string Nickname { get; private set; } = string.Empty;

    /// <summary>头像 URL（HTTPS），可空。</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>账户状态。</summary>
    public AccountStatus Status { get; private set; }

    /// <summary>角色集合，至少 1 个。</summary>
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    /// <summary>默认收货地址标识，可空。</summary>
    public Guid? DefaultAddressId { get; private set; }

    /// <summary>连续登录失败次数。</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>锁定截止时间（UTC），锁定状态非空。</summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private User() { }

    private User(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建处于 Active 状态的账户，初始角色为 Buyer，附加 <see cref="UserRegisteredEvent"/>。
    /// 密码哈希由应用层在调用前生成。
    /// </summary>
    public static User Create(
        Guid id,
        string username,
        string? email,
        string? phoneNumber,
        string? passwordHash,
        string nickname,
        string? avatarUrl = null)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "USER_ID_EMPTY");
        }

        ValidateUsername(username);
        ValidateEmail(email);
        ValidatePhone(phoneNumber);
        ValidateNickname(nickname);
        ValidateAvatarUrl(avatarUrl);

        // 至少提供邮箱、手机号或密码哈希之一，否则无登录方式
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phoneNumber) && string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new UserAuthDomainException("必须提供邮箱、手机号或密码之一", "USER_NO_LOGIN_METHOD", 400);
        }

        var user = new User(id)
        {
            Username = username.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            PasswordHash = passwordHash,
            Nickname = nickname.Trim(),
            AvatarUrl = avatarUrl,
            Status = AccountStatus.Active,
            FailedLoginCount = 0
        };
        user._roles.Add(new UserRole(RoleType.Buyer));

        user.AddDomainEvent(new UserRegisteredEvent(user.Id, user.Username, user.Email, user.PhoneNumber));

        return user;
    }

    /// <summary>
    /// 校验密码，失败时累加 <see cref="FailedLoginCount"/>，达阈值调用 <see cref="Lock"/>。
    /// </summary>
    public bool VerifyPassword(string plainPassword, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);

        if (string.IsNullOrEmpty(PasswordHash))
        {
            return false;
        }

        var ok = hasher.Verify(plainPassword, PasswordHash);
        if (!ok)
        {
            FailedLoginCount++;
            if (FailedLoginCount >= MaxFailedLoginCount)
            {
                Lock("连续登录失败达阈值", DefaultLockDuration);
            }
        }

        return ok;
    }

    /// <summary>
    /// 修改密码：校验旧密码后写入新哈希，附加 <see cref="UserPasswordChangedEvent"/>。
    /// </summary>
    public void ChangePassword(string oldPlainPassword, string newPlainPassword, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);

        if (string.IsNullOrEmpty(PasswordHash))
        {
            throw new UserAuthDomainException("当前账户未设置密码，无法修改", "USER_NO_PASSWORD", 409);
        }

        if (!hasher.Verify(oldPlainPassword, PasswordHash))
        {
            throw new UserAuthDomainException("旧密码不正确", "USER_OLD_PASSWORD_INVALID", 401);
        }

        ValidatePasswordStrength(newPlainPassword);

        if (hasher.Verify(newPlainPassword, PasswordHash))
        {
            throw new UserAuthDomainException("新密码不可与旧密码相同", "USER_PASSWORD_SAME", 400);
        }

        PasswordHash = hasher.Hash(newPlainPassword);

        AddDomainEvent(new UserPasswordChangedEvent(Id));
    }

    /// <summary>登录成功后清零失败计数。</summary>
    public void RecordLogin()
    {
        FailedLoginCount = 0;
    }

    /// <summary>
    /// 锁定账户，置 Status 为 Locked 并设置 <see cref="LockedUntil"/>，附加 <see cref="UserSuspendedEvent"/>。
    /// </summary>
    public void Lock(string reason, TimeSpan duration)
    {
        if (Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("已禁用的账户不可锁定", "USER_DISABLED", 409);
        }

        Status = AccountStatus.Locked;
        LockedUntil = DateTime.UtcNow.Add(duration);
        FailedLoginCount = 0;

        AddDomainEvent(new UserSuspendedEvent(Id, reason, nameof(AccountStatus.Locked)));
    }

    /// <summary>解锁账户，置 Status 为 Active 并清零失败计数与锁定截止时间。</summary>
    public void Unlock()
    {
        if (Status != AccountStatus.Locked)
        {
            throw new UserAuthDomainException("仅锁定状态的账户可解锁", "USER_NOT_LOCKED", 409);
        }

        Status = AccountStatus.Active;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// 禁用账户（终态），附加 <see cref="UserSuspendedEvent"/>。
    /// 禁止管理员禁用自身账户（INV-13）。
    /// </summary>
    public void Disable(string reason, Guid? operatorId = null)
    {
        if (operatorId.HasValue && operatorId.Value == Id)
        {
            throw new UserAuthDomainException("禁止禁用自身账户", "USER_DISABLE_SELF", 409);
        }

        Status = AccountStatus.Disabled;
        LockedUntil = null;

        AddDomainEvent(new UserSuspendedEvent(Id, reason, nameof(AccountStatus.Disabled)));
    }

    /// <summary>恢复账户为 Active 状态（管理员操作）。</summary>
    public void Activate()
    {
        if (Status != AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("仅禁用状态的账户可恢复", "USER_NOT_DISABLED", 409);
        }

        Status = AccountStatus.Active;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// 分配角色，若角色已存在则忽略，附加 <see cref="UserRoleAssignedEvent"/>。
    /// </summary>
    public void AssignRole(RoleType role, Guid? operatorId = null)
    {
        if (_roles.Any(r => r.Value == role))
        {
            return;
        }

        _roles.Add(new UserRole(role));

        AddDomainEvent(new UserRoleAssignedEvent(Id, role.ToString(), operatorId));
    }

    /// <summary>
    /// 撤销角色。禁止移除最后一个角色（INV-12），禁止管理员撤销自身 Admin 角色（INV-13）。
    /// </summary>
    public void RevokeRole(RoleType role, Guid? operatorId = null)
    {
        if (operatorId.HasValue && operatorId.Value == Id && role == RoleType.Admin)
        {
            throw new UserAuthDomainException("禁止撤销自身管理员角色", "USER_REVOKE_ADMIN_SELF", 409);
        }

        var existing = _roles.FirstOrDefault(r => r.Value == role);
        if (existing is null)
        {
            return;
        }

        if (_roles.Count <= 1)
        {
            throw new UserAuthDomainException("至少保留一个角色", "USER_LAST_ROLE", 409);
        }

        _roles.Remove(existing);
    }

    /// <summary>更新昵称与头像。</summary>
    public void UpdateProfile(string nickname, string? avatarUrl)
    {
        ValidateNickname(nickname);
        ValidateAvatarUrl(avatarUrl);

        Nickname = nickname.Trim();
        AvatarUrl = avatarUrl;
    }

    /// <summary>更新默认收货地址引用。</summary>
    public void SetDefaultAddress(Guid? addressId)
    {
        DefaultAddressId = addressId;
    }

    /// <summary>更新邮箱（需经单独验证流程，此处仅写入）。</summary>
    public void UpdateEmail(string? email)
    {
        ValidateEmail(email);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    /// <summary>更新手机号（需经单独验证流程，此处仅写入）。</summary>
    public void UpdatePhoneNumber(string? phoneNumber)
    {
        ValidatePhone(phoneNumber);
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }

    /// <summary>判断账户当前是否可登录（未禁用且未在锁定期）。</summary>
    public bool CanLogin()
    {
        if (Status == AccountStatus.Disabled)
        {
            return false;
        }

        if (Status == AccountStatus.Locked && LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new UserAuthDomainException("用户名不可为空", "USER_USERNAME_EMPTY");
        }

        var trimmed = username.Trim();
        if (trimmed.Length is < 3 or > 32)
        {
            throw new UserAuthDomainException("用户名长度须为 3-32 字符", "USER_USERNAME_LENGTH");
        }

        if (!ValidUsernamePattern().IsMatch(trimmed))
        {
            throw new UserAuthDomainException("用户名仅允许字母、数字与下划线", "USER_USERNAME_FORMAT");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (!ValidEmailPattern().IsMatch(email.Trim()))
        {
            throw new UserAuthDomainException("邮箱格式不正确", "USER_EMAIL_FORMAT");
        }
    }

    private static void ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        if (!ValidPhonePattern().IsMatch(phone.Trim()))
        {
            throw new UserAuthDomainException("手机号须为 E.164 格式（如 +8613800138000）", "USER_PHONE_FORMAT");
        }
    }

    private static void ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            throw new UserAuthDomainException("昵称不可为空", "USER_NICKNAME_EMPTY");
        }

        if (nickname.Trim().Length is < 1 or > 32)
        {
            throw new UserAuthDomainException("昵称长度须为 1-32 字符", "USER_NICKNAME_LENGTH");
        }
    }

    private static void ValidateAvatarUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new UserAuthDomainException("头像 URL 必须为 HTTPS", "USER_AVATAR_FORMAT");
        }
    }

    private static void ValidatePasswordStrength(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
        {
            throw new UserAuthDomainException("密码不可为空", "USER_PASSWORD_EMPTY");
        }

        if (plainPassword.Length is < 8 or > 64)
        {
            throw new UserAuthDomainException("密码长度须为 8-64 位", "USER_PASSWORD_LENGTH");
        }

        var hasLetter = false;
        var hasDigit = false;
        foreach (var c in plainPassword)
        {
            if (char.IsLetter(c))
            {
                hasLetter = true;
            }
            else if (char.IsDigit(c))
            {
                hasDigit = true;
            }
        }

        if (!hasLetter || !hasDigit)
        {
            throw new UserAuthDomainException("密码须至少包含字母与数字", "USER_PASSWORD_STRENGTH");
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]{3,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidUsernamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ValidEmailPattern();

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPhonePattern();
}

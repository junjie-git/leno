using System.Security.Cryptography;
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
    private readonly List<ExternalLogin> _externalLogins = new();

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

    /// <summary>外部登录绑定集合，OAuth 用户至少 1 个。</summary>
    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

    /// <summary>默认收货地址标识，可空。</summary>
    public Guid? DefaultAddressId { get; private set; }

    /// <summary>连续登录失败次数。</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>锁定截止时间（UTC），锁定状态非空。</summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>是否已启用双因子认证。</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>双因子认证 TOTP 共享密钥（Base32）。为空表示未设置。</summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>EF Core 乐观并发控制版本号（shadow property 配合 IsRowVersion）。</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>EF Core 无参构造。</summary>
    private User() { }

    private User(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建处于 Active 状态的账户，初始角色为 Buyer，附加 <see cref="UserRegisteredDomainEvent"/>。
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
            throw new UserAuthDomainException("必须提供邮箱、手机号或密码之一", "USER_NO_LOGIN_METHOD");
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

        user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Username, user.Email, user.PhoneNumber));
        return user;
    }

    /// <summary>
    /// 工厂方法，创建 OAuth2 用户。校验密码，失败时累加 <see cref="FailedLoginCount"/>，达阈值调用 <see cref="Lock"/>。
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
    /// Disabled 状态不允许修改密码；Locked 状态允许（视为解锁流程的一部分）。
    /// </summary>
    public void ChangePassword(string oldPlainPassword, string newPlainPassword, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);

        if (Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("账户已禁用，不可修改密码", "USER_DISABLED");
        }

        if (string.IsNullOrEmpty(PasswordHash))
        {
            throw new UserAuthDomainException("当前账户未设置密码，无法修改", "USER_NO_PASSWORD");
        }

        if (!hasher.Verify(oldPlainPassword, PasswordHash))
        {
            throw new UserAuthDomainException("旧密码不正确", "USER_OLD_PASSWORD_INVALID");
        }

        ValidatePasswordStrength(newPlainPassword);

        if (hasher.Verify(newPlainPassword, PasswordHash))
        {
            throw new UserAuthDomainException("新密码不可与旧密码相同", "USER_PASSWORD_SAME");
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
            throw new UserAuthDomainException("已禁用的账户不可锁定", "USER_DISABLED");
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
            throw new UserAuthDomainException("仅锁定状态的账户可解锁", "USER_NOT_LOCKED");
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
            throw new UserAuthDomainException("禁止禁用自身账户", "USER_DISABLE_SELF");
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
            throw new UserAuthDomainException("仅禁用状态的账户可恢复", "USER_NOT_DISABLED");
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
            throw new UserAuthDomainException("禁止撤销自身管理员角色", "USER_REVOKE_ADMIN_SELF");
        }

        var existing = _roles.FirstOrDefault(r => r.Value == role);
        if (existing is null)
        {
            return;
        }

        if (_roles.Count <= 1)
        {
            throw new UserAuthDomainException("至少保留一个角色", "USER_LAST_ROLE");
        }

        _roles.Remove(existing);
    }

    /// <summary>更新昵称与头像。Disabled 状态不允许更新资料。</summary>
    public void UpdateProfile(string nickname, string? avatarUrl)
    {
        if (Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("账户已禁用，不可更新资料", "USER_DISABLED");
        }

        ValidateNickname(nickname);
        ValidateAvatarUrl(avatarUrl);

        Nickname = nickname.Trim();
        AvatarUrl = avatarUrl;
    }

    /// <summary>
    /// 重命名用户名（OAuth 注册时由应用层在用户名冲突后调用），内部复用 <see cref="ValidateUsername"/> 校验。
    /// 不允许外部直接 set Username 字段，所有用户名变更须经此方法。
    /// </summary>
    /// <param name="newUsername">新用户名，需满足 3-32 字符且仅包含字母、数字与下划线。</param>
    public void Rename(string newUsername)
    {
        ValidateUsername(newUsername);
        Username = newUsername.Trim();
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

    /// <summary>
    /// 绑定外部登录，若同 Provider 已绑定则抛出异常。
    /// 附加 <see cref="ExternalLoginLinkedEvent"/>。
    /// </summary>
    public void LinkExternalLogin(string provider, string providerUserId, string? email, string? name, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new UserAuthDomainException("第三方用户标识不可为空", "OAUTH_PROVIDER_USER_ID_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        if (_externalLogins.Any(el => el.Provider == normalizedProvider))
        {
            throw new UserAuthDomainException(
                $"已绑定 {provider} 登录，不可重复绑定", "EXTERNAL_LOGIN_ALREADY_LINKED");
        }

        _externalLogins.Add(new ExternalLogin(normalizedProvider, providerUserId.Trim(), email, name, avatarUrl));

        AddDomainEvent(new ExternalLoginLinkedEvent(Id, normalizedProvider, providerUserId.Trim()));
    }

    /// <summary>
    /// 解绑指定提供方的外部登录。若未绑定则忽略。
    /// OAuth 用户须至少保留一个外部登录绑定。
    /// 附加 <see cref="ExternalLoginUnlinkedEvent"/>。
    /// </summary>
    public void UnlinkExternalLogin(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var existing = _externalLogins.FirstOrDefault(el => el.Provider == normalizedProvider);
        if (existing is null)
        {
            return;
        }

        // 仅 OAuth 用户（无密码、无手机号）须至少保留一个外部登录
        if (string.IsNullOrEmpty(PasswordHash) && string.IsNullOrEmpty(PhoneNumber) && _externalLogins.Count <= 1)
        {
            throw new UserAuthDomainException("至少保留一个外部登录绑定", "EXTERNAL_LOGIN_LAST");
        }

        _externalLogins.Remove(existing);

        AddDomainEvent(new ExternalLoginUnlinkedEvent(Id, normalizedProvider, existing.ProviderUserId));
    }

    /// <summary>
    /// 从外部登录信息创建 OAuth 用户（无密码、无手机号）。
    /// 用户名从邮箱前缀生成，初始角色为 Buyer，附加 <see cref="UserRegisteredDomainEvent"/>。
    /// </summary>
    public static User CreateFromExternal(Guid id, ExternalLoginInfo info)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "USER_ID_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(info);

        var username = GenerateUsernameFromEmail(info.Email);
        var nickname = info.Name;

        // 昵称可能为空，使用邮箱前缀兜底
        if (string.IsNullOrWhiteSpace(nickname))
        {
            nickname = username;
        }

        var user = new User(id)
        {
            Username = username,
            Email = info.Email,
            PhoneNumber = null,
            PasswordHash = null,
            Nickname = nickname.Trim(),
            AvatarUrl = info.AvatarUrl,
            Status = AccountStatus.Active,
            FailedLoginCount = 0
        };
        user._roles.Add(new UserRole(RoleType.Buyer));
        user._externalLogins.Add(new ExternalLogin(info.Provider, info.ProviderUserId, info.Email, info.Name, info.AvatarUrl));

        user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Username, user.Email, user.PhoneNumber));

        return user;
    }

    /// <summary>
    /// 用户名保留字集合，生成的用户名命中保留字时追加随机后缀避免冲突或冒充。
    /// </summary>
    private static readonly HashSet<string> UsernameReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "root", "system", "administrator", "leno", "support", "null", "undefined",
        "api", "test", "guest", "user", "moderator", "superuser", "operator"
    };

    /// <summary>
    /// 从邮箱前缀生成用户名，若冲突由应用层重试时追加后缀。
    /// 处理 null 邮箱（微信/支付宝）、保留字冲突、空前缀、短前缀与超长前缀。
    /// </summary>
    private static string GenerateUsernameFromEmail(string? email)
    {
        // 微信/支付宝不返回邮箱，email 为 null，直接使用 GUID 前缀兜底
        if (string.IsNullOrEmpty(email))
        {
            return $"u{Guid.NewGuid().ToString("N")[..7]}";
        }

        var atIndex = email.IndexOf('@');
        var prefix = atIndex > 0 ? email[..atIndex] : email;
        var sanitized = new string(prefix.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        // sanitize 后为空（如邮箱前缀全是特殊字符），使用 GUID 前 8 字符兜底
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = $"u{Guid.NewGuid().ToString("N")[..7]}";
        }

        if (sanitized.Length < 3)
        {
            sanitized = sanitized.PadRight(3, '0');
        }

        if (sanitized.Length > 32)
        {
            sanitized = sanitized[..32];
        }

        // 保留字追加随机后缀避免冒充系统账号
        if (UsernameReservedWords.Contains(sanitized))
        {
            var suffix = RandomNumberGenerator.GetInt32(1000, 9999)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            var candidate = $"{sanitized}_{suffix}";
            sanitized = candidate.Length <= 32 ? candidate : candidate[..32];
        }

        return sanitized;
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

    /// <summary>
    /// 启用双因子认证：生成 TOTP 密钥，置 TwoFactorEnabled=false 待确认。
    /// 调用 <see cref="ConfirmTwoFactor"/> 完成验证后才会真正启用。
    /// </summary>
    /// <param name="tokenVerifier">TOTP 令牌验证器。</param>
    /// <returns>生成的 QR 码 URI（Base32 密钥可通过 TwoFactorSecret 属性获取）。</returns>
    public string EnableTwoFactor(ITokenVerifier tokenVerifier)
    {
        ArgumentNullException.ThrowIfNull(tokenVerifier);

        if (TwoFactorEnabled)
        {
            throw new UserAuthDomainException("双因子认证已启用，请先禁用后再重新设置", "USER_2FA_ALREADY_ENABLED");
        }

        TwoFactorSecret = tokenVerifier.GenerateSecret();
        TwoFactorEnabled = false;

        var accountName = !string.IsNullOrWhiteSpace(Email) ? Email : Username;
        return tokenVerifier.GenerateQrCodeUri(accountName, TwoFactorSecret);
    }

    /// <summary>
    /// 确认双因子认证：验证 TOTP 码，通过后置 TwoFactorEnabled=true。
    /// </summary>
    /// <param name="totpCode">用户输入的 6 位 TOTP 验证码。</param>
    /// <param name="tokenVerifier">TOTP 令牌验证器。</param>
    public void ConfirmTwoFactor(string totpCode, ITokenVerifier tokenVerifier)
    {
        ArgumentNullException.ThrowIfNull(tokenVerifier);

        if (TwoFactorEnabled)
        {
            throw new UserAuthDomainException("双因子认证已确认，无需重复确认", "USER_2FA_ALREADY_CONFIRMED");
        }

        if (string.IsNullOrWhiteSpace(TwoFactorSecret))
        {
            throw new UserAuthDomainException("请先启用双因子认证", "USER_2FA_NOT_INITIATED");
        }

        if (string.IsNullOrWhiteSpace(totpCode))
        {
            throw new UserAuthDomainException("验证码不可为空", "USER_2FA_CODE_EMPTY");
        }

        if (!tokenVerifier.Verify(TwoFactorSecret, totpCode.Trim()))
        {
            throw new UserAuthDomainException("验证码无效或已过期", "USER_2FA_CODE_INVALID");
        }

        TwoFactorEnabled = true;
    }

    /// <summary>
    /// 禁用双因子认证：清除密钥与启用状态。
    /// 需重新确认当前身份（如密码验证）后方可调用。
    /// </summary>
    public void DisableTwoFactor()
    {
        if (!TwoFactorEnabled)
        {
            throw new UserAuthDomainException("双因子认证未启用", "USER_2FA_NOT_ENABLED");
        }

        TwoFactorEnabled = false;
        TwoFactorSecret = null;
    }

    /// <summary>
    /// 验证 TOTP 码（用于登录第二因子验证），不改变任何状态。
    /// </summary>
    /// <param name="totpCode">用户输入的 6 位 TOTP 验证码。</param>
    /// <param name="tokenVerifier">TOTP 令牌验证器。</param>
    /// <returns>验证通过返回 true。</returns>
    public bool VerifyTwoFactorCode(string totpCode, ITokenVerifier tokenVerifier)
    {
        ArgumentNullException.ThrowIfNull(tokenVerifier);

        if (!TwoFactorEnabled)
        {
            throw new UserAuthDomainException("双因子认证未启用", "USER_2FA_NOT_ENABLED");
        }

        if (string.IsNullOrWhiteSpace(TwoFactorSecret))
        {
            throw new UserAuthDomainException("双因子认证密钥缺失", "USER_2FA_SECRET_MISSING");
        }

        if (string.IsNullOrWhiteSpace(totpCode))
        {
            return false;
        }

        return tokenVerifier.Verify(TwoFactorSecret, totpCode.Trim());
    }

    /// <summary>
    /// 重置密码（密码找回流程），直接设置新密码哈希，附加 <see cref="UserPasswordChangedEvent"/>。
    /// </summary>
    public void ResetPassword(string newPasswordHash, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);

        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new UserAuthDomainException("新密码哈希不可为空", "USER_PASSWORD_HASH_EMPTY");
        }

        PasswordHash = newPasswordHash;

        AddDomainEvent(new UserPasswordChangedEvent(Id));
    }

    /// <summary>
    /// 发布忘记密码请求事件，附加 <see cref="ForgotPasswordRequestedEvent"/>。
    /// </summary>
    public void PublishForgotPasswordRequested(string resetToken)
    {
        if (string.IsNullOrWhiteSpace(resetToken))
        {
            throw new UserAuthDomainException("重置令牌不可为空", "USER_RESET_TOKEN_EMPTY");
        }

        AddDomainEvent(new ForgotPasswordRequestedEvent(Id, Email, PhoneNumber, resetToken));
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

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 用户管理后台应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// 编排用户分页查询、详情查询、角色分配（跨域 HTTP 调 AccessControl BC）与账户状态管理。
/// 角色变更与账户锁定/恢复后撤销该用户所有 RefreshToken，确保特权变更与封禁立即生效。
/// </para>
/// <para>
/// <b>跨域通信：</b><see cref="AssignRolesAsync"/> 通过 HTTP POST 调 AccessControl BC
/// <c>api/admin/users/{userId}/roles</c> 端点（Spec §4.3.2 推荐方案），Identity BC 本身不持久化角色数据。
/// HttpClient 由 <c>IHttpClientFactory</c> 在 Infrastructure 层注册（命名客户端 <c>AccessControl</c>），
/// 调用方需配置 BaseAddress 与 <c>X-Internal-Key</c> 头。
/// </para>
/// </summary>
public sealed class UserAdminAppService : IUserAdminAppService
{
    /// <summary>AccessControl BC 角色分配端点路径模板。</summary>
    private const string AssignRolesEndpointTemplate = "api/admin/users/{0}/roles";

    /// <summary>撤销原因：管理员分配角色。</summary>
    private const string RevokeReasonRoleAssign = "RoleAssign";

    /// <summary>撤销原因：管理员锁定账户。</summary>
    private const string RevokeReasonSuspend = "AdminSuspend";

    /// <summary>每页最大条数（与旧域 UserAuth 保持一致）。</summary>
    private const int MaxPageSize = 100;

    /// <summary>默认每页条数。</summary>
    private const int DefaultPageSize = 20;

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly HttpClient _accessControlClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserAdminAppService> _logger;

    /// <summary>
    /// 初始化 <see cref="UserAdminAppService"/> 的新实例。
    /// </summary>
    /// <param name="userRepository">用户仓储。</param>
    /// <param name="refreshTokenRepository">刷新令牌仓储（角色变更/封禁后撤销令牌）。</param>
    /// <param name="accessControlClient">AccessControl BC HTTP 客户端（由 HttpClientFactory 注入）。</param>
    /// <param name="unitOfWork">工作单元。</param>
    /// <param name="logger">日志。</param>
    public UserAdminAppService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        HttpClient accessControlClient,
        IUnitOfWork unitOfWork,
        ILogger<UserAdminAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _accessControlClient = accessControlClient ?? throw new ArgumentNullException(nameof(accessControlClient));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminUserDto>> QueryUsersAsync(
        AdminUserQueryDto query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);

        var (items, total) = await _userRepository.QueryAsync(
            query.Keyword,
            query.Status,
            normalizedPage,
            normalizedPageSize,
            ct).ConfigureAwait(false);

        var dtos = items.Select(ToAdminUserDto).ToList();

        return PagedResult.Create(dtos, total, normalizedPage, normalizedPageSize);
    }

    /// <inheritdoc />
    public async Task<AdminUserDto> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);
        return ToAdminUserDto(user);
    }

    /// <inheritdoc />
    public async Task AssignRolesAsync(Guid userId, List<Guid> roleIds, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(roleIds);
        if (roleIds.Count == 0)
        {
            throw new IdentityDomainException("待分配角色列表不可为空", "USER_ROLES_EMPTY");
        }

        // 校验用户存在（角色数据归属 AccessControl BC，此处仅做存在性校验）
        await RequireUserAsync(userId, ct).ConfigureAwait(false);

        // 调 AccessControl BC HTTP 端点（Spec §4.3.2 推荐方案）
        var endpoint = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            AssignRolesEndpointTemplate, userId.ToString("D"));

        var payload = new AssignRolesRequestDto(roleIds);

        HttpResponseMessage response;
        try
        {
            response = await _accessControlClient.PostAsJsonAsync(endpoint, payload, ct)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "调用 AccessControl BC 角色分配端点失败，UserId={UserId}, Endpoint={Endpoint}",
                userId, endpoint);
            throw new IdentityDomainException(
                "角色分配服务暂时不可用，请稍后重试", ex, "ACCESS_CONTROL_UNAVAILABLE");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogError(
                "AccessControl BC 角色分配返回失败状态，UserId={UserId}, StatusCode={StatusCode}, Body={Body}",
                userId, response.StatusCode, body);
            throw new IdentityDomainException(
                $"角色分配失败：AccessControl 返回 {response.StatusCode}", "ACCESS_CONTROL_ASSIGN_FAILED");
        }

        // 角色变更后撤销该用户所有 RefreshToken，强制下次登录/刷新重新签发带新角色声明的令牌
        await _refreshTokenRepository.RevokeAllByUserAsync(userId, RevokeReasonRoleAssign, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "用户角色分配成功，UserId={UserId}, RoleCount={RoleCount}",
            userId, roleIds.Count);
    }

    /// <inheritdoc />
    public async Task SuspendAsync(Guid userId, SuspendUserDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new IdentityDomainException("锁定原因不可为空", "USER_SUSPEND_REASON_EMPTY");
        }

        if (request.DurationMinutes is <= 0 or > 1440)
        {
            throw new IdentityDomainException("锁定时长须为 1-1440 分钟", "USER_SUSPEND_DURATION_INVALID");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        // Disabled 是终态，不可再锁定
        if (user.Status == AccountStatus.Disabled)
        {
            throw new IdentityDomainException("已禁用的账户不可锁定", "USER_DISABLED");
        }

        user.Lock(request.Reason, TimeSpan.FromMinutes(request.DurationMinutes));

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 撤销该用户所有 RefreshToken，封禁立即生效
        await _refreshTokenRepository.RevokeAllByUserAsync(userId, RevokeReasonSuspend, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "用户账户已锁定，UserId={UserId}, Reason={Reason}, DurationMinutes={DurationMinutes}",
            userId, request.Reason, request.DurationMinutes);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        // 按当前状态选择恢复路径：Locked → Unlock；Disabled → Activate；Active 直接返回成功
        if (user.Status == AccountStatus.Locked)
        {
            user.Unlock();
        }
        else if (user.Status == AccountStatus.Disabled)
        {
            user.Activate();
        }
        else if (user.Status == AccountStatus.Active)
        {
            _logger.LogInformation("用户账户已处于 Active 状态，无需恢复，UserId={UserId}", userId);
            return;
        }
        else
        {
            throw new IdentityDomainException(
                $"不支持的账户状态：{user.Status}", "USER_STATUS_INVALID");
        }

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("用户账户已恢复为 Active，UserId={UserId}", userId);
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

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize <= 0 ? DefaultPageSize
         : pageSize > MaxPageSize ? MaxPageSize
         : pageSize;

    private static AdminUserDto ToAdminUserDto(User user)
        => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Nickname = user.Nickname,
            Status = user.Status,
            // Identity BC 不持久化角色，默认空集合，由 Controller 层调 AccessControl RPC 填充
            Roles = Array.Empty<string>(),
            FailedLoginCount = user.FailedLoginCount,
            LockedUntil = user.LockedUntil,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

    /// <summary>
    /// AccessControl BC 角色分配端点请求体。
    /// </summary>
    private sealed class AssignRolesRequestDto
    {
        [JsonPropertyName("roleIds")]
        public List<Guid> RoleIds { get; }

        public AssignRolesRequestDto(List<Guid> roleIds)
        {
            RoleIds = roleIds ?? throw new ArgumentNullException(nameof(roleIds));
        }
    }
}

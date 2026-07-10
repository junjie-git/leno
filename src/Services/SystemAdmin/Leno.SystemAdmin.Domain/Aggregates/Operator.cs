using System.Diagnostics.CodeAnalysis;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 系统运营人员聚合根，链接用户域账号，管理角色、权限码集合与启停状态。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>OperatorId</c>。
/// 一个用户域账号至多对应一个运营人员（由仓储以 UserId 唯一约束保证）。
/// </summary>
[SuppressMessage("Design", "CA1716:Identifiers should not match keywords", Justification = "本解决方案仅使用 C#，跨语言关键字冲突不适用；'Operator' 为系统管理域核心聚合名称，被仓储、应用层与 API 契约广泛引用。")]
public sealed class Operator : AggregateRoot
{
    private const int MaxDisplayNameLength = 100;

    private List<string> _permissions = [];

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid OperatorId => Id;

    /// <summary>关联用户域账号标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>显示名称，≤100 字。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>运营角色。</summary>
    public OperatorRole Role { get; private set; }

    /// <summary>
    /// 权限码集合，经聚合根维护去重。
    /// 持久化为聚合子集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<string> Permissions { get => _permissions; private set => _permissions = value ?? []; }

    /// <summary>启停状态。</summary>
    public OperatorStatus Status { get; private set; }

    /// <summary>最近登录时间（UTC），可空。</summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Operator() { }

    private Operator(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建启用态运营人员，权限码去重去空白。
    /// </summary>
    /// <param name="operatorId">运营人员标识，由应用层生成。</param>
    /// <param name="userId">关联用户域账号标识。</param>
    /// <param name="displayName">显示名称。</param>
    /// <param name="role">运营角色。</param>
    /// <param name="permissions">权限码集合。</param>
    public static Operator Create(Guid operatorId, Guid userId, string displayName, OperatorRole role, List<string> permissions)
    {
        if (operatorId == Guid.Empty)
        {
            throw new SystemAdminDomainException("运营人员标识不可为空", "OPERATOR_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new SystemAdminDomainException("用户账号标识不可为空", "OPERATOR_USER_ID_EMPTY");
        }

        ValidateDisplayName(displayName);
        ValidateRole(role);
        ArgumentNullException.ThrowIfNull(permissions);

        return new Operator(operatorId)
        {
            UserId = userId,
            DisplayName = displayName.Trim(),
            Role = role,
            Permissions = NormalizePermissions(permissions),
            Status = OperatorStatus.Active,
            LastLoginAt = null
        };
    }

    /// <summary>
    /// 合并新增权限码，去重并忽略空白项。
    /// </summary>
    /// <param name="permissions">待合并权限码集合。</param>
    public void AssignPermissions(List<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        foreach (var raw in permissions)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = raw.Trim();
            if (!_permissions.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                _permissions.Add(normalized);
            }
        }
    }

    /// <summary>
    /// 移除匹配的权限码，不存在的忽略。
    /// </summary>
    /// <param name="permissions">待移除权限码集合。</param>
    public void RevokePermissions(List<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in permissions)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                toRemove.Add(raw.Trim());
            }
        }

        _permissions.RemoveAll(p => toRemove.Contains(p));
    }

    /// <summary>启用运营人员。</summary>
    public void Activate()
    {
        Status = OperatorStatus.Active;
    }

    /// <summary>停用运营人员。</summary>
    public void Deactivate()
    {
        Status = OperatorStatus.Inactive;
    }

    /// <summary>
    /// 记录最近登录时间。
    /// </summary>
    /// <param name="loginAt">登录时间（UTC）。</param>
    public void RecordLogin(DateTime loginAt)
    {
        if (loginAt == default)
        {
            throw new SystemAdminDomainException("登录时间不可为空", "OPERATOR_LOGIN_AT_EMPTY");
        }

        LastLoginAt = loginAt;
    }

    private static List<string> NormalizePermissions(List<string> permissions)
    {
        var result = new List<string>();
        foreach (var raw in permissions)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = raw.Trim();
            if (!result.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new SystemAdminDomainException("显示名称不可为空", "OPERATOR_DISPLAY_NAME_EMPTY");
        }

        if (displayName.Trim().Length > MaxDisplayNameLength)
        {
            throw new SystemAdminDomainException($"显示名称长度不可超过 {MaxDisplayNameLength} 字符", "OPERATOR_DISPLAY_NAME_LENGTH");
        }
    }

    private static void ValidateRole(OperatorRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new SystemAdminDomainException("运营角色取值非法", "OPERATOR_ROLE_INVALID");
        }
    }
}

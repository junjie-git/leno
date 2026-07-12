using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 角色聚合根，承载 RBAC 权限集合。
/// 内置角色（Buyer/Seller/Operator/Admin）不可删除。
/// </summary>
public sealed class Role : AggregateRoot
{
    private readonly List<PermissionVO> _permissions = new();

    /// <summary>角色名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>角色描述。</summary>
    public string? Description { get; private set; }

    /// <summary>权限集合。</summary>
    public IReadOnlyCollection<PermissionVO> Permissions => _permissions.AsReadOnly();

    /// <summary>是否为内置角色（不可删除）。</summary>
    public bool IsBuiltIn { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Role() { }

    private Role(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建自定义角色。
    /// </summary>
    public static Role Create(Guid id, string name, string? description, bool isBuiltIn = false)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("角色标识不可为空", "ROLE_ID_EMPTY");
        }

        ValidateName(name);

        return new Role(id)
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            IsBuiltIn = isBuiltIn
        };
    }

    /// <summary>更新角色名称与描述。</summary>
    public void Update(string name, string? description)
    {
        ValidateName(name);
        Name = name.Trim();
        Description = description?.Trim();
    }

    /// <summary>设置权限集合（全量替换）。</summary>
    public void SetPermissions(IEnumerable<PermissionVO> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions.Clear();
        _permissions.AddRange(permissions);
    }

    /// <summary>添加单个权限（幂等：已存在则忽略）。</summary>
    public void AddPermission(PermissionVO permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (_permissions.Any(p => p.ResourceKey == permission.ResourceKey))
        {
            return;
        }
        _permissions.Add(permission);
    }

    /// <summary>移除单个权限。</summary>
    public void RemovePermission(string resourceKey)
    {
        var existing = _permissions.FirstOrDefault(p => p.ResourceKey == resourceKey);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }

    /// <summary>判断是否拥有指定权限。</summary>
    public bool HasPermission(string resourceKey)
    {
        return _permissions.Any(p => p.ResourceKey == resourceKey);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new UserAuthDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        if (name.Trim().Length is < 2 or > 64)
        {
            throw new UserAuthDomainException("角色名称长度须为 2-64 字符", "ROLE_NAME_LENGTH");
        }
    }
}
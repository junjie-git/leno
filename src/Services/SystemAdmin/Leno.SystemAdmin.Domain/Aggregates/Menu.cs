using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 菜单聚合根：树形结构，支持 Directory / Menu / Button 三类节点。
/// 排序通过同级 Sort 字段控制；删除时由仓储递归处理子节点。
/// </summary>
public sealed class Menu : AggregateRoot
{
    private const int MaxNameLength = 32;
    private const int MaxPathLength = 256;
    private const int MaxComponentLength = 256;
    private const int MaxIconLength = 64;
    private const int MaxPermissionLength = 64;
    private const int MaxRolesJsonLength = 256;

    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MenuType Type { get; private set; }
    public string? Path { get; private set; }
    public string? Component { get; private set; }
    public string? Icon { get; private set; }
    public int Sort { get; private set; }
    public string? Permission { get; private set; }
    public List<string> Roles { get; private set; } = [];
    public bool Visible { get; private set; } = true;
    public bool Cache { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Menu() { }

    private Menu(Guid id) : base(id) { }

    /// <summary>创建根节点（ParentId = null）。</summary>
    public static Menu CreateRoot(
        Guid id,
        string name,
        MenuType type,
        string? path = null,
        string? icon = null,
        string? component = null,
        string? permission = null,
        int sort = 0,
        List<string>? roles = null,
        bool visible = true,
        bool cache = false)
    {
        return Create(id, null, name, type, path, icon, component, permission, sort, roles, visible, cache);
    }

    /// <summary>创建子节点。</summary>
    public static Menu CreateChild(
        Guid id,
        Guid parentId,
        string name,
        MenuType type,
        string? path = null,
        string? component = null,
        string? icon = null,
        string? permission = null,
        int sort = 0,
        List<string>? roles = null,
        bool visible = true,
        bool cache = false)
    {
        if (parentId == Guid.Empty)
        {
            throw new SystemAdminDomainException("父菜单标识不可为空", "MENU_PARENT_EMPTY");
        }
        return Create(id, parentId, name, type, path, icon, component, permission, sort, roles, visible, cache);
    }

    private static Menu Create(
        Guid id,
        Guid? parentId,
        string name,
        MenuType type,
        string? path,
        string? icon,
        string? component,
        string? permission,
        int sort,
        List<string>? roles,
        bool visible,
        bool cache)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("菜单标识不可为空", "MENU_ID_EMPTY");
        }

        ValidateName(name);
        ValidateTypeAndPath(type, path);
        ValidateTypeAndComponent(type, component);
        ValidateIcon(icon);
        ValidatePermission(permission);
        ValidateSort(sort);
        ValidateRoles(roles);

        return new Menu(id)
        {
            ParentId = parentId,
            Name = name.Trim(),
            Type = type,
            Path = NormalizeNullable(path),
            Icon = NormalizeNullable(icon),
            Component = NormalizeNullable(component),
            Permission = NormalizeNullable(permission),
            Sort = sort,
            Roles = roles?.ToList() ?? new List<string>(),
            Visible = visible,
            Cache = cache,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string newName)
    {
        ValidateName(newName);
        Name = newName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePath(string? newPath)
    {
        ValidateTypeAndPath(Type, newPath);
        Path = NormalizeNullable(newPath);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeSort(int newSort)
    {
        ValidateSort(newSort);
        Sort = newSort;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveTo(Guid? newParentId)
    {
        if (newParentId.HasValue && newParentId.Value == Guid.Empty)
        {
            throw new SystemAdminDomainException("父菜单标识不可为空", "MENU_PARENT_EMPTY");
        }
        ParentId = newParentId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ToggleVisible()
    {
        Visible = !Visible;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ToggleCache()
    {
        Cache = !Cache;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignRoles(List<string> roles)
    {
        ValidateRoles(roles);
        Roles = roles.ToList();
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("菜单名称不可为空", "MENU_NAME_EMPTY");
        }
        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"菜单名称长度不可超过 {MaxNameLength} 字符", "MENU_NAME_LENGTH");
        }
    }

    private static void ValidateTypeAndPath(MenuType type, string? path)
    {
        if (type == MenuType.Button && !string.IsNullOrWhiteSpace(path))
        {
            throw new SystemAdminDomainException("按钮类型菜单不可设置 Path", "MENU_BUTTON_PATH_FORBIDDEN");
        }
        if (!string.IsNullOrWhiteSpace(path) && path.Trim().Length > MaxPathLength)
        {
            throw new SystemAdminDomainException($"Path 长度不可超过 {MaxPathLength} 字符", "MENU_PATH_LENGTH");
        }
    }

    private static void ValidateTypeAndComponent(MenuType type, string? component)
    {
        if (type == MenuType.Menu && string.IsNullOrWhiteSpace(component))
        {
            throw new SystemAdminDomainException("菜单类型必须填写 Component", "MENU_COMPONENT_REQUIRED");
        }
        if (!string.IsNullOrWhiteSpace(component) && component.Trim().Length > MaxComponentLength)
        {
            throw new SystemAdminDomainException($"Component 长度不可超过 {MaxComponentLength} 字符", "MENU_COMPONENT_LENGTH");
        }
    }

    private static void ValidateIcon(string? icon)
    {
        if (!string.IsNullOrWhiteSpace(icon) && icon.Trim().Length > MaxIconLength)
        {
            throw new SystemAdminDomainException($"Icon 长度不可超过 {MaxIconLength} 字符", "MENU_ICON_LENGTH");
        }
    }

    private static void ValidatePermission(string? permission)
    {
        if (!string.IsNullOrWhiteSpace(permission) && permission.Trim().Length > MaxPermissionLength)
        {
            throw new SystemAdminDomainException($"Permission 长度不可超过 {MaxPermissionLength} 字符", "MENU_PERMISSION_LENGTH");
        }
    }

    private static void ValidateSort(int sort)
    {
        if (sort < 0)
        {
            throw new SystemAdminDomainException("Sort 不可为负数", "MENU_SORT_NEGATIVE");
        }
    }

    private static void ValidateRoles(List<string>? roles)
    {
        if (roles is null) return;
        if (roles.Count > 10)
        {
            throw new SystemAdminDomainException("角色数量不可超过 10", "MENU_ROLES_TOO_MANY");
        }
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

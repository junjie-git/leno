using System.ComponentModel.DataAnnotations;
using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 菜单节点 DTO，对应前端 spec §3.3。
/// 树形结构通过 <see cref="Children"/> 表达；叶子节点 Children 为空列表。
/// </summary>
public sealed class MenuDto
{
    /// <summary>菜单标识（Guid 序列化为字符串）。</summary>
    public Guid Id { get; set; }

    /// <summary>父菜单标识，根节点为 null。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>菜单名称（1-32 字符）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>菜单类型：Directory / Menu / Button。</summary>
    public MenuType Type { get; set; }

    /// <summary>路由路径，Button 类型必须为 null。</summary>
    public string? Path { get; set; }

    /// <summary>前端组件路径，Menu 类型必填。</summary>
    public string? Component { get; set; }

    /// <summary>图标标识。</summary>
    public string? Icon { get; set; }

    /// <summary>同级排序，≥ 0。</summary>
    public int Sort { get; set; }

    /// <summary>权限标识。</summary>
    public string? Permission { get; set; }

    /// <summary>可见角色列表。</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>是否可见。</summary>
    public bool Visible { get; set; }

    /// <summary>是否启用路由缓存。</summary>
    public bool Cache { get; set; }

    /// <summary>子菜单列表。</summary>
    public List<MenuDto> Children { get; set; } = [];
}

/// <summary>
/// 创建菜单请求 DTO。
/// Type=Menu 时 Component 必填；Type=Button 时 Path 必须为 null。
/// </summary>
public sealed class CreateMenuDto
{
    public Guid? ParentId { get; set; }

    /// <summary>菜单名称（1-32 字符，对应 Menu 聚合根 ValidateName 约束）。</summary>
    [Required(ErrorMessage = "菜单名称不可为空")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "菜单名称长度需在 1-32 字符之间")]
    public string Name { get; set; } = string.Empty;

    public MenuType Type { get; set; }

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Icon { get; set; }

    public int Sort { get; set; }

    public string? Permission { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool Visible { get; set; } = true;

    public bool Cache { get; set; }
}

/// <summary>
/// 更新菜单请求 DTO，所有字段可选（部分更新）。
/// </summary>
public sealed class UpdateMenuDto
{
    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Icon { get; set; }

    public int? Sort { get; set; }

    public string? Permission { get; set; }

    public List<string>? Roles { get; set; }

    public bool? Visible { get; set; }

    public bool? Cache { get; set; }

    public Guid? ParentId { get; set; }
}

/// <summary>
/// 菜单排序项 DTO，用于批量更新同级菜单 Sort 字段。
/// </summary>
public sealed class MenuSortItemDto
{
    /// <summary>菜单标识。</summary>
    public Guid Id { get; set; }

    /// <summary>新的排序值。</summary>
    public int Sort { get; set; }
}

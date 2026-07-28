using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 菜单管理应用服务接口。
/// 提供菜单树查询、创建、更新、删除（递归）与同级排序能力。
/// </summary>
public interface IMenuAppService
{
    /// <summary>获取完整菜单树（一次性载入全部并按 ParentId 组装树形结构）。</summary>
    Task<List<MenuDto>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>创建菜单节点。重复 path 抛 SystemAdminDomainException(code MENU_PATH_DUPLICATE)。</summary>
    Task<MenuDto> CreateAsync(CreateMenuDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>更新菜单节点（部分更新）。菜单不存在抛 SystemAdminDomainException(code MENU_NOT_FOUND)。</summary>
    Task<MenuDto> UpdateAsync(Guid id, UpdateMenuDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>删除菜单节点（递归删除子树）。带子菜单时抛 SystemAdminDomainException(code MENU_HAS_CHILDREN)。</summary>
    Task DeleteAsync(Guid id, Guid operatorId, CancellationToken ct = default);

    /// <summary>批量更新同级菜单排序。</summary>
    Task SortAsync(List<MenuSortItemDto> items, Guid operatorId, CancellationToken ct = default);
}

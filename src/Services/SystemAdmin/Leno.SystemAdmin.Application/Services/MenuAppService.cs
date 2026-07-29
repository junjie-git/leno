using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 菜单管理应用服务实现。
/// 编排 Menu 聚合根与 IMenuRepository：树形组装在应用层完成（菜单总数 ≤ 100，全量载入可接受）。
/// 删除带子菜单的节点由应用层先调 CountChildrenAsync 校验后抛业务异常（code MENU_HAS_CHILDREN）。
/// </summary>
public sealed class MenuAppService : IMenuAppService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MenuAppService> _logger;

    public MenuAppService(
        IMenuRepository menuRepository,
        IUnitOfWork unitOfWork,
        ILogger<MenuAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(menuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _menuRepository = menuRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<MenuDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _menuRepository.GetAllAsync(ct);
        var dtos = all.Select(ToDto).ToList();
        return BuildTree(dtos);
    }

    /// <inheritdoc />
    public async Task<MenuDto> CreateAsync(CreateMenuDto dto, Guid operatorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!string.IsNullOrWhiteSpace(dto.Path))
        {
            var existing = await _menuRepository.GetByPathAsync(dto.Path, ct);
            if (existing is not null)
            {
                throw new SystemAdminDomainException($"菜单路径已存在：{dto.Path}", "MENU_PATH_DUPLICATE");
            }
        }

        var id = Guid.NewGuid();
        Menu menu = dto.ParentId.HasValue
            ? Menu.CreateChild(id, dto.ParentId.Value, dto.Name, dto.Type, dto.Path, dto.Component, dto.Icon,
                dto.Permission, dto.Sort, dto.Roles, dto.Visible, dto.Cache)
            : Menu.CreateRoot(id, dto.Name, dto.Type, dto.Path, dto.Icon, dto.Component, dto.Permission,
                dto.Sort, dto.Roles, dto.Visible, dto.Cache);

        menu.AssignRoles(dto.Roles);
        await _menuRepository.AddAsync(menu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("菜单已创建 Id={MenuId} Name={Name} Operator={OperatorId}", menu.Id, menu.Name, operatorId);
        return ToDto(menu);
    }

    /// <inheritdoc />
    public async Task<MenuDto> UpdateAsync(Guid id, UpdateMenuDto dto, Guid operatorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var menu = await _menuRepository.GetByIdAsync(id, ct);
        if (menu is null)
        {
            throw new SystemAdminDomainException($"菜单不存在：{id}", "MENU_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            menu.Rename(dto.Name);
        }
        if (dto.Path is not null)
        {
            menu.ChangePath(dto.Path);
        }
        if (dto.Component is not null)
        {
            menu.ChangeComponent(dto.Component);
        }
        if (dto.Icon is not null)
        {
            menu.ChangeIcon(dto.Icon);
        }
        if (dto.Sort.HasValue)
        {
            menu.ChangeSort(dto.Sort.Value);
        }
        if (dto.Permission is not null)
        {
            menu.ChangePermission(dto.Permission);
        }
        if (dto.Roles is not null)
        {
            menu.AssignRoles(dto.Roles);
        }
        if (dto.Visible.HasValue)
        {
            if (dto.Visible.Value != menu.Visible)
            {
                menu.ToggleVisible();
            }
        }
        if (dto.Cache.HasValue)
        {
            if (dto.Cache.Value != menu.Cache)
            {
                menu.ToggleCache();
            }
        }
        if (dto.ParentId.HasValue && dto.ParentId.Value != menu.ParentId)
        {
            menu.MoveTo(dto.ParentId.Value);
        }

        await _menuRepository.UpdateAsync(menu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("菜单已更新 Id={MenuId} Operator={OperatorId}", menu.Id, operatorId);
        return ToDto(menu);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, Guid operatorId, CancellationToken ct = default)
    {
        var childCount = await _menuRepository.CountChildrenAsync(id, ct);
        if (childCount > 0)
        {
            throw new SystemAdminDomainException($"存在 {childCount} 个子菜单，无法删除", "MENU_HAS_CHILDREN");
        }

        await _menuRepository.DeleteAsync(id, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("菜单已删除 Id={MenuId} Operator={OperatorId}", id, operatorId);
    }

    /// <inheritdoc />
    public async Task SortAsync(List<MenuSortItemDto> items, Guid operatorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            var menu = await _menuRepository.GetByIdAsync(item.Id, ct);
            if (menu is null)
            {
                _logger.LogWarning("排序跳过不存在的菜单 Id={MenuId}", item.Id);
                continue;
            }

            // 跨父拖拽：parentId 变化时先调用 MoveTo（含环引用等领域校验）
            if (item.ParentId != menu.ParentId)
            {
                menu.MoveTo(item.ParentId);
            }

            menu.ChangeSort(item.Sort);
            await _menuRepository.UpdateAsync(menu, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("菜单批量排序完成 Count={Count} Operator={OperatorId}", items.Count, operatorId);
    }

    private static MenuDto ToDto(Menu entity)
        => new()
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            Name = entity.Name,
            Type = entity.Type,
            Path = entity.Path,
            Component = entity.Component,
            Icon = entity.Icon,
            Sort = entity.Sort,
            Permission = entity.Permission,
            Roles = entity.Roles.ToList(),
            Visible = entity.Visible,
            Cache = entity.Cache
        };

    /// <summary>
    /// 将扁平 DTO 列表组装为树形结构：按 ParentId 分组，根节点 ParentId 为 null。
    /// </summary>
    private static List<MenuDto> BuildTree(List<MenuDto> all)
    {
        var lookup = all.ToLookup(d => d.ParentId);
        foreach (var node in all)
        {
            node.Children = lookup[node.Id].OrderBy(d => d.Sort).ThenBy(d => d.Name).ToList();
        }
        return lookup[null].OrderBy(d => d.Sort).ThenBy(d => d.Name).ToList();
    }
}

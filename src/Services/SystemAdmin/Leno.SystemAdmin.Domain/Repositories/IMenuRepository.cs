using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>菜单仓储接口。</summary>
public interface IMenuRepository
{
    Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Menu>> GetAllAsync(CancellationToken ct = default);
    Task<List<Menu>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<Menu?> GetByPathAsync(string path, CancellationToken ct = default);
    Task<List<Menu>> GetByRoleAsync(string role, CancellationToken ct = default);
    Task AddAsync(Menu menu, CancellationToken ct = default);
    Task UpdateAsync(Menu menu, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct = default);
}

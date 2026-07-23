using Leno.AccessControl.Domain.Aggregates;
using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.AccessControl.Infrastructure;

/// <summary>
/// AccessControl BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 Role 与 UserRoleAssignment 聚合的 DbSet。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class AccessControlDbContext : BaseDbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options) : base(options)
    {
    }

    /// <summary>角色权限聚合根。</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>用户角色分配聚合根。</summary>
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
}

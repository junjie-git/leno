using System.Linq.Expressions;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 基础 DbContext，统一应用 IEntityTypeConfiguration 配置、审计字段自动填充与软删除全局查询过滤器。
/// 业务上下文 DbContext 继承此类，按需声明 DbSet 并添加 EF Core 拦截器。
/// </summary>
public abstract class BaseDbContext : DbContext
{
    /// <summary>
    /// 发件箱消息集合，由基类统一暴露，各 BC 无需重复声明。
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// 当前用户上下文，子类通过构造函数注入并覆盖此属性以填充审计字段 CreatedBy/UpdatedBy。
    /// 为 null 时（如后台迁移工具、无 HttpContext 的控制台任务），审计字段填 "system"。
    /// 此属性由 <see cref="AuditableEntityInterceptor"/> 在 <see cref="OnConfiguring"/> 中通过访问器捕获，
    /// 拦截器在 SavingChanges 时解析，避免构造时序问题。
    /// </summary>
    protected virtual ICurrentUserContext? CurrentUserContext => null;

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext()
    {
    }

    /// <summary>
    /// 注册审计字段拦截器，自动填充 CreatedAt/UpdatedAt/CreatedBy/UpdatedBy。
    /// 拦截器通过访问器延迟解析 <see cref="CurrentUserContext"/>，确保子类构造完成后能正确获取用户上下文。
    /// 所有继承 <see cref="BaseDbContext"/> 的 DbContext 自动获得审计字段填充能力，无需各 BC 重复注册。
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.AddInterceptors(new AuditableEntityInterceptor(() => CurrentUserContext));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // 基类统一应用 OutboxMessage 配置（先于子 assembly 配置，确保子类如存在同类型配置可覆盖）
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // 统一配置乐观锁 shadow property（避免领域层 Entity 携带持久化细节）
        // 所有继承 Entity 的实体自动获得名为 "Version" 的 rowversion shadow property
        // 跳过 owned type（由 OwnsOne/OwnsMany 持有的实体）以避免 "cannot be configured as non-owned" 异常
        // 跳过已显式声明 rowversion 列的实体（如 OrderConfiguration 中显式声明的 row_version 列），
        // 因 SQL Server 单表仅允许一个 rowversion 列，否则迁移会失败
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType) && !entityType.IsOwned())
            {
                var hasExplicitRowVersion = entityType.GetProperties()
                    .Any(p => p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate);

                if (!hasExplicitRowVersion)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property<byte[]>("Version")
                        .HasColumnName("version")
                        .IsRowVersion();
                }
            }
        }

        ApplySoftDeleteQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// 对所有实现 <see cref="ISoftDeletable"/> 的实体应用全局查询过滤器，
    /// 自动排除已软删除记录（<c>IsDeleted == false</c>）。
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // 跳过 owned type，避免对其重复配置触发 "cannot be configured as non-owned" 异常
            if (entityType.IsOwned())
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var propertyAccess = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Not(propertyAccess);
            var lambda = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}

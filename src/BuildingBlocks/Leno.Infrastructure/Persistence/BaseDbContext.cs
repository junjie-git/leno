using System.Linq.Expressions;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

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
    /// </summary>
    protected virtual ICurrentUserContext? CurrentUserContext => null;

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext()
    {
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
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType) && !entityType.IsOwned())
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<byte[]>("Version")
                    .HasColumnName("version")
                    .IsRowVersion();
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

    /// <summary>
    /// 保存变更前统一填充审计字段（CreatedAt/UpdatedAt 与 CreatedBy/UpdatedBy）。
    /// 时间戳始终填充；用户标识由 <see cref="CurrentUserContext"/> 解析，未认证或缺失时填 "system"。
    /// </summary>
    public override int SaveChanges()
    {
        FillAuditableFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        FillAuditableFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void FillAuditableFields()
    {
        var now = DateTime.UtcNow;
        var userIdentifier = ResolveUserIdentifier();

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy = userIdentifier;
                    entry.Entity.UpdatedBy = userIdentifier;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userIdentifier;
                    // CreatedBy/CreatedAt 在修改时不应被覆盖，保留原始创建者信息
                    break;
            }
        }
    }

    /// <summary>
    /// 解析当前用户标识符，用于审计字段 CreatedBy/UpdatedBy。
    /// 已认证用户返回 UserId.ToString()；未认证、UserId 为 null 或无用户上下文时返回 "system"。
    /// </summary>
    private string ResolveUserIdentifier()
    {
        var userContext = CurrentUserContext;
        if (userContext is null || !userContext.IsAuthenticated || userContext.UserId is null)
        {
            return "system";
        }
        return userContext.UserId.Value.ToString();
    }
}

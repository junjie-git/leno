using System.Linq.Expressions;
using Leno.Infrastructure.Abstractions.MultiTenancy;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.MultiTenancy;
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

    /// <summary>
    /// 当前租户上下文（多租户预留扩展位，4.7）。
    /// <para>
    /// 子类通过构造函数注入 <see cref="ITenantContext"/> 并覆盖此属性以激活多租户全局查询过滤器。
    /// 为 <c>null</c> 时（如后台迁移工具、未注册多租户服务的场景），<see cref="CurrentTenantId"/> 返回 <c>null</c>，
    /// 全局查询过滤器退化为"返回所有数据"（单租户模式），默认行为不变。
    /// </para>
    /// <para>
    /// 此属性由 <see cref="TenantQueryFilterInterceptor"/> 在 <see cref="OnConfiguring"/> 中通过访问器捕获，
    /// 拦截器在 SavingChanges 时解析，避免构造时序问题。
    /// </para>
    /// </summary>
    protected virtual ITenantContext? TenantContext => null;

    /// <summary>
    /// 当前租户 ID 的安全访问器（多租户预留扩展位，4.7）。
    /// <para>
    /// 供全局查询过滤器在表达式树中引用 —— <see cref="TenantContext"/> 为 <c>null</c> 时返回 <c>null</c>（单租户模式）。
    /// 需为 <c>public</c> 以便 <c>Expression.Property</c> 在构建查询过滤器表达式树时能通过反射访问。
    /// </para>
    /// </summary>
    public Guid? CurrentTenantId => TenantContext?.CurrentTenantId;

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext()
    {
    }

    /// <summary>
    /// 注册审计字段拦截器与租户拦截器。
    /// <para>
    /// <see cref="AuditableEntityInterceptor"/> 自动填充 CreatedAt/UpdatedAt/CreatedBy/UpdatedBy，
    /// <see cref="TenantQueryFilterInterceptor"/> 在保存时为新实体填充 <c>TenantId</c>（仅当 <see cref="TenantContext"/> 非 null 且当前租户 ID 已设置时）。
    /// 拦截器通过访问器延迟解析上下文，确保子类构造完成后能正确获取。
    /// 所有继承 <see cref="BaseDbContext"/> 的 DbContext 自动获得审计字段填充与租户 ID 填充能力，无需各 BC 重复注册。
    /// </para>
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(() => CurrentUserContext),
            new TenantQueryFilterInterceptor(() => TenantContext));
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
        ApplyTenantQueryFilters(modelBuilder);

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
    /// 对所有实现 <see cref="ITenantEntity"/> 的实体应用多租户扩展位配置（4.7）。
    /// <para>
    /// 1. 声明 <c>tenant_id</c> 列（nullable，snake_case），当前阶段默认 <c>null</c> = 全局数据。
    /// 2. 应用全局查询过滤器：<c>e.TenantId == null || CurrentTenantId == null || e.TenantId == CurrentTenantId</c>。
    /// </para>
    /// <para>
    /// 语义说明：
    /// <list type="bullet">
    /// <item><see cref="CurrentTenantId"/> 为 <c>null</c>（单租户模式）→ 过滤器恒真，返回所有数据，默认行为不变。</item>
    /// <item><see cref="CurrentTenantId"/> 非 <c>null</c>（多租户模式）→ 返回全局数据（<c>TenantId == null</c>）+ 当前租户数据。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 表达式树中引用 <see cref="CurrentTenantId"/>（DbContext 属性），EF Core 在查询时求值并作为参数传入 SQL，
    /// 确保每次查询都使用最新的租户上下文值。
    /// </para>
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // 跳过 owned type，避免对其重复配置触发 "cannot be configured as non-owned" 异常
            if (entityType.IsOwned())
            {
                continue;
            }

            // 声明 tenant_id 列（nullable，DG-7 通过后改为 required）
            // 基类统一应用默认列配置；各 BC 的 IEntityTypeConfiguration 可在此基础上追加索引等扩展，
            // 重复声明同名列配置在 EF Core 中幂等（后调用覆盖前者，值一致无副作用）。
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(ITenantEntity.TenantId))
                .HasColumnName("tenant_id")
                .IsRequired(false);

            // 构建查询过滤器表达式：e => e.TenantId == null || this.CurrentTenantId == null || e.TenantId == this.CurrentTenantId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdAccess = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var nullGuid = Expression.Constant(null, typeof(Guid?));
            var isTenantIdNull = Expression.Equal(tenantIdAccess, nullGuid);

            var thisExpr = Expression.Constant(this);
            var currentTenantIdAccess = Expression.Property(thisExpr, nameof(CurrentTenantId));
            var isCurrentTenantNull = Expression.Equal(currentTenantIdAccess, nullGuid);
            var tenantIdEqualsCurrent = Expression.Equal(tenantIdAccess, currentTenantIdAccess);

            var body = Expression.OrElse(
                isTenantIdNull,
                Expression.OrElse(isCurrentTenantNull, tenantIdEqualsCurrent));
            var lambda = Expression.Lambda(body, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}

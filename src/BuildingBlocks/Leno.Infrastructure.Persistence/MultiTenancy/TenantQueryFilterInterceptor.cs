using Leno.Infrastructure.Abstractions.MultiTenancy;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leno.Infrastructure.MultiTenancy;

/// <summary>
/// 租户拦截器，在 <see cref="SaveChanges"/> 时自动为新实体填充 <see cref="ITenantEntity.TenantId"/>。
/// <para>
/// 配合 <see cref="BaseDbContext"/> 的全局查询过滤器实现多租户数据隔离：
/// <list type="bullet">
/// <item>查询过滤器（<c>HasQueryFilter</c>）在查询时按 <c>TenantId</c> 过滤，仅返回当前租户 + 全局数据。</item>
/// <item>本拦截器在保存时将 <see cref="ITenantContext.CurrentTenantId"/> 写入新实体的 <c>TenantId</c> 字段。</item>
/// </list>
/// </para>
/// <para>
/// 当前阶段（DG-7 未通过）：<see cref="ITenantContext.CurrentTenantId"/> 默认 <c>null</c>，
/// 拦截器不设置 <c>TenantId</c>，实体保持 <c>TenantId = null</c>（全局数据），默认行为不变。
/// </para>
/// <para>
/// DG-7 通过后：由 <c>TenantMiddleware</c> 设置 <see cref="ITenantContext.CurrentTenantId"/>，
/// 拦截器自动将新实体的 <c>TenantId</c> 填充为当前租户 ID，实现多租户隔离。
/// </para>
/// </summary>
public sealed class TenantQueryFilterInterceptor : SaveChangesInterceptor
{
    private readonly Func<ITenantContext?>? _tenantContextAccessor;

    /// <summary>
    /// 创建租户拦截器实例。
    /// </summary>
    /// <param name="tenantContextAccessor">
    /// 租户上下文访问器，用于解析当前租户 ID。
    /// 为 <c>null</c> 时（如后台迁移工具、无 HttpContext 的控制台任务），不填充 <c>TenantId</c>。
    /// 访问器在 <see cref="SavingChanges"/> 时才解析，避免构造时序问题。
    /// </param>
    public TenantQueryFilterInterceptor(Func<ITenantContext?>? tenantContextAccessor = null)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        FillTenantId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FillTenantId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// 为新增的 <see cref="ITenantEntity"/> 实体填充 <c>TenantId</c>。
    /// 仅当 <see cref="ITenantContext.CurrentTenantId"/> 非 <c>null</c> 且实体 <c>TenantId</c> 尚未设置时填充。
    /// </summary>
    private void FillTenantId(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var tenantContext = _tenantContextAccessor?.Invoke();
        var currentTenantId = tenantContext?.CurrentTenantId;
        if (currentTenantId is null)
        {
            // 单租户模式：不设置 TenantId，实体保持 null（全局数据），默认行为不变
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId is null)
            {
                entry.Entity.TenantId = currentTenantId;
            }
        }
    }
}

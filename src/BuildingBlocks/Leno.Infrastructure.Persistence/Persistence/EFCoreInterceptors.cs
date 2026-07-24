using Leno.Infrastructure.Auth;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 审计字段拦截器，在保存变更前自动填充 CreatedAt/UpdatedAt/CreatedBy/UpdatedBy。
/// 通过 <c>optionsBuilder.AddInterceptors(new AuditableEntityInterceptor())</c> 启用，
/// <see cref="BaseDbContext"/> 已在 <see cref="DbContext.OnConfiguring(DbContextOptionsBuilder)"/> 中统一注册。
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly Func<ICurrentUserContext?>? _currentUserContextAccessor;

    /// <summary>
    /// 创建审计字段拦截器实例。
    /// </summary>
    /// <param name="currentUserContextAccessor">
    /// 当前用户上下文访问器，用于解析 CreatedBy/UpdatedBy。
    /// 为 null 时（如后台迁移工具、无 HttpContext 的控制台任务），审计字段填 "system"。
    /// 访问器在 <see cref="SavingChanges"/> 时才解析，避免构造时序问题。
    /// </param>
    public AuditableEntityInterceptor(Func<ICurrentUserContext?>? currentUserContextAccessor = null)
    {
        _currentUserContextAccessor = currentUserContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        FillAuditableFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FillAuditableFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void FillAuditableFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var userIdentifier = ResolveUserIdentifier();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
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
        var userContext = _currentUserContextAccessor?.Invoke();
        if (userContext is null || !userContext.IsAuthenticated || userContext.UserId is null)
        {
            return "system";
        }
        return userContext.UserId.Value.ToString();
    }
}

/// <summary>
/// 软删除拦截器，将 <see cref="EntityState.Deleted"/> 转为 <see cref="EntityState.Modified"/> 并置位
/// <see cref="ISoftDeletable.IsDeleted"/> 与 <see cref="ISoftDeletable.DeletedAt"/>，
/// 配合全局查询过滤器实现软删除。
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ConvertSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ConvertSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ConvertSoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }
    }
}

/// <summary>
/// 乐观锁拦截器，捕获 <see cref="DbUpdateConcurrencyException"/>（Version 字段并发冲突）并转换为业务可读异常。
/// </summary>
public sealed class OptimisticLockInterceptor : SaveChangesInterceptor
{
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ThrowIfConcurrencyConflict(eventData.Exception);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencyConflict(eventData.Exception);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void ThrowIfConcurrencyConflict(Exception? exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("并发冲突：数据已被其他事务修改，请刷新后重试", exception);
        }
    }
}

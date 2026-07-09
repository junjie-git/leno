using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 审计字段拦截器，在保存变更前自动填充 CreatedAt/UpdatedAt。
/// 与 <see cref="BaseDbContext.SaveChangesAsync(CancellationToken)"/> 中的填充逻辑二选一使用，
/// 避免重复填充。派生 DbContext 可通过 <c>optionsBuilder.AddInterceptors(new AuditableEntityInterceptor())</c> 启用。
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
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

    private static void FillAuditableFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
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

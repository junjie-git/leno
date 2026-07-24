namespace Leno.Infrastructure.Abstractions.MultiTenancy;

/// <summary>
/// 租户上下文抽象：解析当前请求的租户 ID。
/// <para>
/// 当前阶段默认 <c>null</c>（单租户模式），DG-7 决策门通过后由
/// <c>TenantMiddleware</c> 从请求头 <c>X-Tenant-Id</c> 或 JWT claim 中解析并设置。
/// </para>
/// <para>
/// 实现应基于 <see cref="AsyncLocal{T}"/> 以支持异步上下文流转，
/// 注册为 Singleton（<c>AsyncLocal</c> 在单例中仍按请求隔离）。
/// </para>
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// 当前租户 ID。<c>null</c> 表示单租户模式（全局数据，不做租户隔离）。
    /// </summary>
    Guid? CurrentTenantId { get; }

    /// <summary>
    /// 设置当前租户 ID。由 <c>TenantMiddleware</c> 在请求开始时调用。
    /// </summary>
    /// <param name="tenantId">租户 ID，<c>null</c> 表示单租户模式。</param>
    void SetTenant(Guid? tenantId);
}

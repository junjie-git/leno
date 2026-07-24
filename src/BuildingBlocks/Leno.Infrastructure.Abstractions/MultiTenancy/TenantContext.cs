namespace Leno.Infrastructure.Abstractions.MultiTenancy;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的租户上下文实现，确保异步上下文流转。
/// <para>
/// 注册为 Singleton —— <see cref="AsyncLocal{T}"/> 的值按逻辑调用上下文隔离，
/// 即使单例实例在多请求间共享，每个请求仍有独立的 <see cref="CurrentTenantId"/> 值。
/// </para>
/// <para>
/// 当前阶段 <see cref="CurrentTenantId"/> 默认 <c>null</c>（单租户模式），
/// 由 <c>MultiTenancyExtensions.AddMultiTenancy</c> 注册到 DI 容器。
/// DG-7 通过后由 <c>TenantMiddleware</c> 在请求管道中调用 <see cref="SetTenant"/>。
/// </para>
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly AsyncLocal<Guid?> _currentTenant = new();

    /// <inheritdoc />
    public Guid? CurrentTenantId => _currentTenant.Value;

    /// <inheritdoc />
    public void SetTenant(Guid? tenantId) => _currentTenant.Value = tenantId;
}

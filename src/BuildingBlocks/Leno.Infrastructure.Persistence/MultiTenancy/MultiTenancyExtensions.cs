using Leno.Infrastructure.Abstractions.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.Infrastructure.MultiTenancy;

/// <summary>
/// 多租户扩展位 DI 注册（4.7 多租户预留）。
/// <para>
/// 各 BC 在 <c>AddXxxInfrastructure</c> 中调用 <see cref="AddMultiTenancy"/> 注册 <see cref="ITenantContext"/>。
/// 当前阶段仅注册扩展位，不改变现有行为（<see cref="ITenantContext.CurrentTenantId"/> 默认 <c>null</c> = 单租户模式）。
/// </para>
/// <para>
/// DG-7 决策门通过后，由 <c>TenantMiddleware</c> 在请求管道中调用 <see cref="ITenantContext.SetTenant"/>
/// 设置当前租户 ID，激活多租户隔离。
/// </para>
/// </summary>
public static class MultiTenancyExtensions
{
    /// <summary>
    /// 注册多租户扩展位服务：<see cref="ITenantContext"/> 单例（<see cref="AsyncLocal{T}"/> 按请求隔离）。
    /// <para>
    /// 当前阶段 <see cref="ITenantContext.CurrentTenantId"/> 默认 <c>null</c>，全局查询过滤器返回所有数据，
    /// 行为与未启用多租户一致。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合（链式调用）。</returns>
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITenantContext, TenantContext>();
        return services;
    }
}

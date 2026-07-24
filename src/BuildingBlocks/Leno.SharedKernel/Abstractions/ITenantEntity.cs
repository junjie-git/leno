namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 支持多租户的实体标记接口（预留扩展位，DG-7 通过后实际启用）。
/// <para>
/// 实现此接口的实体将自动获得 <c>tenant_id</c> 列与全局查询过滤器
/// （由 <c>BaseDbContext</c> 在 <c>OnModelCreating</c> 中统一配置）。
/// </para>
/// <para>
/// 当前阶段 <see cref="TenantId"/> 默认 <c>null</c>（单租户模式），
/// 全局查询过滤器在 <c>TenantId == null</c> 时返回所有数据，行为与未启用多租户一致。
/// DG-7 决策门通过后，由 <c>TenantQueryFilterInterceptor</c> 在保存时自动填充当前租户 ID，
/// 切换为多租户隔离模式。
/// </para>
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// 租户 ID。当前阶段 nullable（默认 <c>null</c> = 单租户模式 / 全局数据），
    /// DG-7 通过后由拦截器自动填充当前租户 ID。
    /// </summary>
    Guid? TenantId { get; set; }
}

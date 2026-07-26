namespace Leno.UserCenter.Application;

/// <summary>
/// 用户默认地址存储抽象，封装 UserCenter BC 对 Identity BC 的跨域依赖。
/// <para>
/// User 聚合归属 Identity BC，Address 聚合归属 UserCenter BC。当 AddressAppService 切换默认地址时，
/// 需同步更新 Identity BC 中 User.DefaultAddressId 字段。本接口隔离该跨域依赖，
/// 由 UserCenter.Infrastructure 提供基于 Identity BC DbContext 的实现。
/// </para>
/// <para>
/// 注意：本接口的实现使用独立的 IdentityDbContext 事务边界，与 UserCenterDbContext 的 UnitOfWork 分离。
/// 调用方应在 UserCenter 事务提交前调用本接口，确保两库一致性以最终一致方式保证（地址变更已落库后再同步 User）。
/// </para>
/// </summary>
public interface IUserDefaultAddressStore
{
    /// <summary>
    /// 更新指定用户的默认地址标识。addressId 为 null 时表示清除默认地址。
    /// 若用户不存在则抛出 <see cref="DomainException"/>。
    /// </summary>
    Task UpdateDefaultAddressAsync(Guid userId, Guid? addressId, CancellationToken ct = default);
}

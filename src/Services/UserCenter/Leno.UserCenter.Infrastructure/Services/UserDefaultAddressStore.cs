using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Infrastructure;
using Leno.UserCenter.Application;
using Leno.UserCenter.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserCenter.Infrastructure.Services;

/// <summary>
/// 用户默认地址存储实现，跨 BC 调用 Identity BC 更新 User.DefaultAddressId。
/// <para>
/// 实现说明：
/// - 直接使用 <see cref="IdentityDbContext"/> 加载 User 聚合并调用 <see cref="User.SetDefaultAddress"/> 更新默认地址。
/// - 独立事务边界：通过 <see cref="IdentityDbContext.SaveChangesAsync(CancellationToken)"/> 提交，
///   与 UserCenter BC 的 <c>IUnitOfWork</c> 事务分离。调用方应在 UserCenter 事务提交前调用本方法，
///   确保地址变更已落库后再同步 Identity BC 的 User 字段（最终一致语义）。
/// - 用户不存在时抛出 <see cref="UserCenterDomainException"/>，与原 UserAuth BC 行为一致。
/// </para>
/// Task A6：从 UserAuth BC 迁入 UserCenter BC，防腐层抽象隔离跨域依赖。
/// </summary>
public sealed class UserDefaultAddressStore : IUserDefaultAddressStore
{
    private readonly IdentityDbContext _identityDbContext;

    public UserDefaultAddressStore(IdentityDbContext identityDbContext)
    {
        ArgumentNullException.ThrowIfNull(identityDbContext);
        _identityDbContext = identityDbContext;
    }

    /// <inheritdoc />
    public async Task UpdateDefaultAddressAsync(Guid userId, Guid? addressId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new UserCenterDomainException("用户标识不可为空", "USER_ID_EMPTY");
        }

        var user = await _identityDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            throw new UserCenterDomainException("用户不存在", "USER_NOT_FOUND");
        }

        user.SetDefaultAddress(addressId);
        await _identityDbContext.SaveChangesAsync(ct);
    }
}

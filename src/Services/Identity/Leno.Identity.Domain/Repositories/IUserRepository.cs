using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Aggregates;

namespace Leno.Identity.Domain.Repositories;

/// <summary>
/// 用户仓储接口，定义在领域层，由基础设施层实现。
/// 查询方法返回聚合根，写操作不立即持久化，由工作单元统一提交。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// 注意：移除了按角色过滤的 QueryAsync 重载（角色信息已迁出至 AccessControl BC）。
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>按用户名查询用户。</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>按邮箱查询用户。</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>按手机号查询用户。</summary>
    Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// 分页查询用户列表，支持按关键词与状态过滤。
    /// 注意：角色过滤已移除至 AccessControl BC（角色信息不再归属 Identity BC）。
    /// </summary>
    Task<(IReadOnlyList<User> Items, int Total)> QueryAsync(
        string? keyword = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>按外部登录提供方与提供方用户标识查询用户。</summary>
    Task<User?> FindByExternalLoginAsync(string provider, string providerUserId, CancellationToken ct = default);
}

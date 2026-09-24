using System.Net;
using System.Text;
using System.Text.Json;
using Leno.Infrastructure.AntiCorruption;
using Leno.UserCenter.Application;
using Leno.UserCenter.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace Leno.UserCenter.Infrastructure.Services;

/// <summary>
/// 用户默认地址存储实现：通过 HTTP 调用 Identity BC 内部 API 更新 User.DefaultAddressId。
/// <para>
/// P0 架构修复（2026-09-24）：原先直接引用 Identity.Domain/Infrastructure 并操作
/// IdentityDbContext 写他域聚合（跨 BC 强耦合、旁路 Identity 的 UnitOfWork/Outbox、
/// 无并发冲突处理）。现遵循 Order BC 防腐层既有模式（ProductAntiCorruptionService 等）：
/// 继承 <see cref="AntiCorruptionBase"/>，经 <c>PUT internal/v1/users/{id}/default-address</c>
/// 端点（X-Internal-Key 鉴权）由 Identity 侧自行提交事务并经 Outbox 发布领域事件。
/// </para>
/// </summary>
public sealed class UserDefaultAddressStore : AntiCorruptionBase, IUserDefaultAddressStore
{
    private const string TargetBc = "Identity";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    protected override string ServiceName => "identity";

    public UserDefaultAddressStore(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("X-Internal-Key", ResolveTargetInternalKey(options));
    }

    /// <inheritdoc />
    public Task UpdateDefaultAddressAsync(Guid userId, Guid? addressId, CancellationToken ct = default)
        => ExecuteAsync("update_default_address", async token =>
        {
            if (userId == Guid.Empty)
            {
                throw new UserCenterDomainException("用户标识不可为空", "USER_ID_EMPTY");
            }

            var request = new { addressId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PutAsync(
                $"internal/v1/users/{userId}/default-address", content, token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // 与 AddressAppService 原有语义保持一致：用户不存在映射为 USER_NOT_FOUND
                throw new UserCenterDomainException("用户不存在", "USER_NOT_FOUND");
            }

            EnsureSuccessStatusCode(response, "update_default_address");
        }, ct);

    private static string ResolveTargetInternalKey(IOptions<AntiCorruptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"AntiCorruption:TargetInternalApiKeys:{TargetBc} 配置缺失，请通过 Consul KV 配置 leno/security/internal-key/{TargetBc}");
        }

        return key;
    }
}

using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Notification.Domain.Services;
using Leno.SharedContracts.Grpc.User.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Services.Grpc;

/// <summary>
/// 用户联系方式 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IUserContactService"/>，与 <see cref="UserContactAntiCorruptionService"/>（HttpClient）双轨。
/// 由 <see cref="AntiCorruptionDispatcher{TService}"/> 在运行时按 <c>UseGrpc</c> 开关与熔断状态选择实现。
/// </summary>
public sealed class GrpcUserContactAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IUserContactService
{
    private const string TargetBc = "UserAuth";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly UserInternalService.UserInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "user_contact";

    public GrpcUserContactAntiCorruptionClient(
        UserInternalService.UserInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcUserContactAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
    }

    /// <inheritdoc />
    public Task<UserContactInfo?> GetContactsAsync(Guid userId, CancellationToken ct = default)
        => ExecuteAsync("get_contacts", async token =>
        {
            var request = new GetUserContactsRequest { UserId = userId.ToString() };
            var metadata = BuildMetadata();
            var response = await _client.GetUserContactsAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return MapToDto(response);
        }, ct);

    private Metadata BuildMetadata()
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
        {
            metadata.Add(InternalKeyHeader, key);
        }
        return metadata;
    }

    private static UserContactInfo? MapToDto(UserContacts proto)
    {
        // proto.user_id 为 optional string，需用 HasUserId 判断；若无则用 Guid.Empty
        var userId = proto.HasUserId && Guid.TryParse(proto.UserId, out var uid) ? uid : Guid.Empty;
        return new UserContactInfo
        {
            UserId = userId,
            Email = proto.Email ?? string.Empty,
            PhoneNumber = proto.Phone ?? string.Empty
        };
    }
}

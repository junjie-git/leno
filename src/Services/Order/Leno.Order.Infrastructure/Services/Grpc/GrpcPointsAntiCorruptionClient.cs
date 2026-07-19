using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Grpc.Points.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// 积分域 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IPointsAntiCorruptionService"/>，与 HttpClient 实现双轨。
/// 由 AntiCorruptionDispatcher 在运行时选择使用本类或 HttpClient 实现。
/// </summary>
public sealed class GrpcPointsAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IPointsAntiCorruptionService
{
    private const string TargetBc = "PointsMembership";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly PointsInternalService.PointsInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "points";

    public GrpcPointsAntiCorruptionClient(
        PointsInternalService.PointsInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcPointsAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
    }

    /// <inheritdoc />
    public Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
        => ExecuteAsync("trial_offset", async token =>
        {
            var request = new TrialOffsetRequest
            {
                UserId = userId.ToString(),
                PointsToUse = pointsToUse
            };
            var metadata = BuildMetadata();
            var response = await _client.TrialOffsetAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return response.OffsetCents / 100m;
        }, ct);

    /// <inheritdoc />
    public async Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
    {
        await ExecuteAsync("freeze_points", async token =>
        {
            var request = new FreezeRequest
            {
                UserId = userId.ToString(),
                OrderId = orderId.ToString(),
                PointsToUse = pointsToUse
            };
            var metadata = BuildMetadata();
            await _client.FreezeAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return true; // ExecuteAsync<TResult> 需要 TResult，用 bool 占位
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        await ExecuteAsync("release_points", async token =>
        {
            var request = new ReleaseRequest { OrderId = orderId.ToString() };
            var metadata = BuildMetadata();
            await _client.ReleaseAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConfirmDeductionAsync(Guid orderId, CancellationToken ct = default)
    {
        await ExecuteAsync("confirm_deduction", async token =>
        {
            var request = new ConfirmRequest { OrderId = orderId.ToString() };
            var metadata = BuildMetadata();
            await _client.ConfirmAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

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
}

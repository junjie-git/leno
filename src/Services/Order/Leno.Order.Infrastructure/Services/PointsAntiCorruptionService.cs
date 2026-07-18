using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 积分域防腐层服务，通过 HTTP 调用积分域内部 API 试算/冻结/释放/确认积分扣减。
/// 继承 <see cref="AntiCorruptionBase"/>，所有远程失败（网络异常、非 2xx、超时）统一抛 <see cref="AntiCorruptionException"/>，不再静默返回 0；用户取消透传 <see cref="OperationCanceledException"/>。
/// M5.2：通过 <see cref="AntiCorruptionOptions.TargetInternalApiKeys"/> 读取目标 BC（PointsMembership）的 InternalApiKey，
/// 注入 <c>X-Internal-Key</c> 请求头，替代旧的共用 InternalAuth:ApiKey。
/// </summary>
public sealed class PointsAntiCorruptionService : AntiCorruptionBase, IPointsAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";
    private const string TargetBc = "PointsMembership";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PointsAntiCorruptionService> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "points";

    public PointsAntiCorruptionService(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        ILogger<PointsAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _targetInternalKey = ResolveTargetInternalKey(options);
        _httpClient.DefaultRequestHeaders.Add(InternalKeyName, _targetInternalKey);
    }

    /// <inheritdoc />
    public Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default)
        => ExecuteAsync("try_offset", async token =>
        {
            var request = new { userId = userId, pointsToUse = pointsToUse };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/points/trial-offset", content, token);
            EnsureSuccessStatusCode(response, "try_offset");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse<TrialOffsetResponse>>(stream, JsonOptions, token);
            if (payload is null || payload.Data is null)
            {
                throw new AntiCorruptionException(
                    $"积分域试算抵现返回空数据（userId={userId}）",
                    "POINTS_REMOTE_FAILED");
            }

            return payload.Data.OffsetAmount;
        }, ct);

    /// <inheritdoc />
    public Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default)
        => ExecuteAsync("freeze", async token =>
        {
            var request = new { userId = userId, orderId = orderId, pointsToUse = pointsToUse };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/points/freeze", content, token);
            EnsureSuccessStatusCode(response, "freeze");
        }, ct);

    /// <inheritdoc />
    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("release", async token =>
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/points/release", content, token);
            EnsureSuccessStatusCode(response, "release");
        }, ct);

    /// <inheritdoc />
    public Task ConfirmDeductionAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("confirm_deduction", async token =>
        {
            var request = new { orderId = orderId };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("internal/v1/points/confirm", content, token);
            EnsureSuccessStatusCode(response, "confirm_deduction");
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

    private sealed class TrialOffsetResponse
    {
        public decimal OffsetAmount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }
}

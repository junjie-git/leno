using System.Net.Http.Json;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 商品域快照防腐层 HttpClient 实现。
/// 继承 <see cref="AntiCorruptionBase"/>，调用失败统一抛 <see cref="AntiCorruptionException"/>。
/// M5.2：通过 <see cref="AntiCorruptionOptions.TargetInternalApiKeys"/> 读取目标 BC（Product）的 InternalApiKey。
/// </summary>
/// <remarks>
/// P1-3：新增 <see cref="GetSkuSnapshotsAsync"/> 批量接口，单次 HTTP 替代 N 次 <see cref="GetSkuSnapshotAsync"/>，
/// 用于 <c>ProductUpdatedEventConsumer</c> 处理单事件多 SKU 场景。
/// </remarks>
public sealed class ProductSnapshotAntiCorruptionService : AntiCorruptionBase, IProductSnapshotAntiCorruption
{
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string SkuEndpointPrefix = "internal/v1/products/skus/";
    private const string SkuBatchEndpoint = "internal/v1/products/skus:batch";
    private const string TargetBc = "Product";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductSnapshotAntiCorruptionService> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "product";

    public ProductSnapshotAntiCorruptionService(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        ILogger<ProductSnapshotAntiCorruptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _targetInternalKey = ResolveTargetInternalKey(options);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshot", async token =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SkuEndpointPrefix + skuId.ToString());
        request.Headers.TryAddWithoutValidation(InternalKeyHeader, _targetInternalKey);

        using var response = await _httpClient.SendAsync(request, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new AntiCorruptionException(
                $"SKU {skuId} 不存在", "PRODUCT_REMOTE_FAILED");
        }
        EnsureSuccessStatusCode(response, "get_sku_snapshot");

        var apiResponse = await response.Content
            .ReadFromJsonAsync<ApiResponse<SkuSnapshotDto>>(token);
        if (apiResponse?.Data is null)
        {
            throw new AntiCorruptionException(
                $"商品域返回空数据 SkuId={skuId}", "PRODUCT_REMOTE_FAILED");
        }
        return apiResponse.Data;
    }, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<SkuSnapshotDto>> GetSkuSnapshotsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshots_batch", async token =>
        {
            ArgumentNullException.ThrowIfNull(skuIds);
            if (skuIds.Count == 0)
            {
                return (IReadOnlyList<SkuSnapshotDto>)Array.Empty<SkuSnapshotDto>();
            }

            // 使用 POST + JSON Body 提交批量查询，避免 GET URL 过长（URI 长度上限）
            using var request = new HttpRequestMessage(HttpMethod.Post, SkuBatchEndpoint)
            {
                Content = JsonContent.Create(new BatchGetSkuSnapshotsRequest { SkuIds = skuIds.ToList() })
            };
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _targetInternalKey);

            using var response = await _httpClient.SendAsync(request, token);
            EnsureSuccessStatusCode(response, "get_sku_snapshots_batch");

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse<IReadOnlyList<SkuSnapshotDto>>>(token);
            if (apiResponse?.Data is null)
            {
                // 商品域批量查询不应返回 null（即便全部未命中也应返回空集合）；视为远程故障
                throw new AntiCorruptionException(
                    $"商品域批量查询返回空数据 SkuCount={skuIds.Count}", "PRODUCT_REMOTE_FAILED");
            }

            // 按请求 SkuId 健壮性过滤：仅保留请求中存在的 SkuId，丢弃商品域误返回的额外项
            var requested = new HashSet<Guid>(skuIds);
            var result = apiResponse.Data.Where(s => requested.Contains(s.SkuId)).ToList();
            return (IReadOnlyList<SkuSnapshotDto>)result;
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

    /// <summary>批量查询请求体（与商品域 internal API 约定）。</summary>
    private sealed class BatchGetSkuSnapshotsRequest
    {
        public List<Guid> SkuIds { get; set; } = new();
    }
}

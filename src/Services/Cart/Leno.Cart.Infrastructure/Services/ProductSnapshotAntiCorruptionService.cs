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
public sealed class ProductSnapshotAntiCorruptionService : AntiCorruptionBase, IProductSnapshotAntiCorruption
{
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string SkuEndpointPrefix = "internal/v1/products/skus/";
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

using System.Net.Http.Json;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 商品域快照防腐层实现，通过 HttpClient 调用商品域 internal API 查询单 SKU 展示快照。
/// 使用 X-Internal-Key 头部鉴权，调用失败返回 null（不抛异常），由调用方决定降级策略。
/// </summary>
public sealed class ProductSnapshotAntiCorruptionService : IProductSnapshotAntiCorruption
{
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string SkuEndpointPrefix = "internal/v1/products/skus/";

    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _internalKeyOptions;
    private readonly ILogger<ProductSnapshotAntiCorruptionService> _logger;

    public ProductSnapshotAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> internalKeyOptions,
        ILogger<ProductSnapshotAntiCorruptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(internalKeyOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _internalKeyOptions = internalKeyOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SkuSnapshotDto?> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                SkuEndpointPrefix + skuId.ToString());
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _internalKeyOptions.ApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "查询商品域 SKU 快照失败 SkuId={SkuId} Status={Status}",
                    skuId, (int)response.StatusCode);
                return null;
            }

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse<SkuSnapshotDto>>(ct);
            return apiResponse?.Data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询商品域 SKU 快照异常 SkuId={SkuId}", skuId);
            return null;
        }
    }
}

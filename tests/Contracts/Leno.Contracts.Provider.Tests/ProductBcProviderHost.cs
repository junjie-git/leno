using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Leno.Contracts.Provider.Tests;

/// <summary>
/// Product BC Provider 测试 API 主机与夹具（阶段 4.10）。
///
/// 在真实 TCP socket 上托管一个最小化 Product BC 契约端点
/// （GET /internal/v1/products/skus/{skuId}），供 PactVerifier 验证。
/// 端点从 <see cref="InMemorySkuStore"/> 取数，数据由
/// <see cref="ProviderStateMiddleware"/> 在每个交互验证前注入。
///
/// 生产化扩展：可将此夹具替换为真实 Product.Api 的 Kestrel 实例
/// （需注入 InternalApiKey 校验豁免与 IProductInternalQueryService 替身），
/// PactVerifier 的 WithHttpEndpoint 指向真实实例即可验证端到端契约遵从性。
/// </summary>
public sealed class ProviderApiFixture : IDisposable
{
    private const int ProviderPort = 9223;

    private readonly IHost _host;
    private readonly InMemorySkuStore _store = new();

    public Uri ServerUri { get; }

    public ProviderApiFixture()
    {
        ServerUri = new Uri($"http://localhost:{ProviderPort}");

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(ServerUri.ToString());
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(_store);
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<ProviderStateMiddleware>();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet(
                            "internal/v1/products/skus/{skuId:guid}",
                            (Guid skuId, InMemorySkuStore store) =>
                            {
                                var item = store.Get(skuId);
                                if (item is null)
                                {
                                    return Results.NotFound(new ApiResponse<SkuInfoResultDto>
                                    {
                                        Code = StatusCodes.Status404NotFound,
                                        Message = "SKU 不存在",
                                        Data = null,
                                    });
                                }

                                return Results.Ok(new ApiResponse<SkuInfoResultDto>
                                {
                                    Code = StatusCodes.Status200OK,
                                    Message = "success",
                                    Data = item,
                                });
                            });
                    });
                });
            })
            .Build();

        _host.Start();
    }

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }
}

/// <summary>
/// 内存 SKU 存储，线程安全，由 ProviderStateMiddleware 注入测试数据。
/// </summary>
public sealed class InMemorySkuStore
{
    private readonly ConcurrentDictionary<Guid, SkuInfoResultDto> _items = new();

    public void Seed(SkuInfoResultDto item) => _items[item.SkuId] = item;

    public SkuInfoResultDto? Get(Guid skuId) =>
        _items.TryGetValue(skuId, out var value) ? value : null;

    public void Clear() => _items.Clear();
}

/// <summary>
/// SKU 查询结果 DTO，镜像 Product BC <c>SkuInfoResultDto</c> 的完整字段
/// （源码位于 src/Services/Product/Leno.Product.Application/SkuInfoResultDto.cs）。
/// Provider 返回完整字段，Consumer 契约仅校验其消费的子集，Pact 默认允许额外字段。
/// </summary>
public sealed class SkuInfoResultDto
{
    public Guid SkuId { get; set; }

    public Guid SpuId { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "CNY";

    public bool Available { get; set; }

    public int Stock { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MainImageUrl { get; set; } = string.Empty;

    public Guid SellerId { get; set; }

    public Guid? ShopId { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 统一 API 响应体，镜像 <c>Leno.SharedContracts.Responses.ApiResponse&lt;T&gt;</c> 的 JSON 形状。
/// </summary>
public sealed class ApiResponse<T>
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }
}

/// <summary>
/// SKU 测试数据工厂，产出与 Consumer 契约预期值一致的 SKU 快照。
/// </summary>
internal static class SkuTestFixtures
{
    private static readonly Guid SellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SpuId = new("44444444-4444-4444-4444-444444444444");

    public static SkuInfoResultDto CreateSku(Guid skuId) => new()
    {
        SkuId = skuId,
        SpuId = SpuId,
        Price = 99.99m,
        Currency = "CNY",
        Available = true,
        Stock = 100,
        Status = "OnSale",
        Title = "Test SKU",
        MainImageUrl = "https://img.example.com/sku-001.jpg",
        SellerId = SellerId,
        ShopId = null,
        UpdatedAt = null,
    };
}

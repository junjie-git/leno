using Grpc.Core;
using Leno.Product.Application;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Product.Api.GrpcServices;

/// <summary>
/// 商品域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IProductInternalQueryService"/> 业务逻辑，与 InternalProductsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class ProductGrpcService : ProductInternalService.ProductInternalServiceBase
{
    private readonly IProductInternalQueryService _queryService;
    private readonly ILogger<ProductGrpcService> _logger;

    public ProductGrpcService(
        IProductInternalQueryService queryService,
        ILogger<ProductGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<SkuInfo> GetSkuInfo(GetSkuInfoRequest request, ServerCallContext context)
    {
        // 注：product.proto 中 sku_id 为 int64，POC 阶段使用 GetHashCode 简化
        // 生产化阶段需将 .proto 改为 string sku_id 承载 Guid.ToString()
        var skuId = new Guid(Convert.FromHexString(request.SkuId.ToString("X16")));
        var dto = await _queryService.GetSkuInfoAsync(skuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU {request.SkuId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<BatchGetSkuInfoResponse> BatchGetSkuInfo(
        BatchGetSkuInfoRequest request, ServerCallContext context)
    {
        var skuIds = request.SkuIds.Select(id => new Guid(Convert.FromHexString(id.ToString("X16")))).ToList();
        var dtos = await _queryService.GetSkuInfosBatchAsync(skuIds, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new BatchGetSkuInfoResponse();
        response.Skus.AddRange(dtos.Select(MapToProto));
        return response;
    }

    public override Task<SkuStock> GetSkuStock(GetSkuStockRequest request, ServerCallContext context)
    {
        // POC 阶段未实现库存查询，返回占位（后续阶段补齐）
        return Task.FromResult(new SkuStock
        {
            SkuId = request.SkuId,
            Available = 0,
            Reserved = 0
        });
    }

    public override Task<ProductDetail> GetProductDetail(GetProductDetailRequest request, ServerCallContext context)
    {
        // POC 阶段未实现，抛 Unimplemented
        throw new RpcException(new Status(StatusCode.Unimplemented, "GetProductDetail not implemented in POC"));
    }

    private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
    {
        SkuId = (long)dto.SkuId.GetHashCode(),  // POC 简化：Guid→int64 映射，生产化改为 string
        SpuId = (long)dto.SpuId.GetHashCode(),
        Title = dto.Title,
        MainImage = dto.MainImageUrl,
        PriceCents = (long)(dto.Price * 100),
        Currency = dto.Currency,
        Salable = dto.Available,
        SellerId = (long)dto.SellerId.GetHashCode(),
        Stock = dto.Stock,
        Status = dto.Status,
        ShopId = dto.ShopId?.ToString() ?? string.Empty,
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L
    };
}

internal static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dt)
        => new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds();
}

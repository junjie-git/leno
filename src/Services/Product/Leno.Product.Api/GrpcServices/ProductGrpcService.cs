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
        // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_id_str 为唯一标识形态
        if (!Guid.TryParse(request.SkuIdStr, out var skuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or missing sku_id_str: {request.SkuIdStr}"));
        }

        var dto = await _queryService.GetSkuInfoAsync(skuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU {request.SkuIdStr} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<BatchGetSkuInfoResponse> BatchGetSkuInfo(
        BatchGetSkuInfoRequest request, ServerCallContext context)
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_ids_str 为唯一标识形态
        var skuIds = request.SkuIdsStr.Select(Guid.Parse).ToList();

        var dtos = await _queryService.GetSkuInfosBatchAsync(skuIds, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new BatchGetSkuInfoResponse();
        response.Skus.AddRange(dtos.Select(MapToProto));
        return response;
    }

    public override async Task<SkuStock> GetSkuStock(GetSkuStockRequest request, ServerCallContext context)
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，sku_id_str 为唯一标识形态
        if (!Guid.TryParse(request.SkuIdStr, out var skuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or missing sku_id_str: {request.SkuIdStr}"));
        }

        var dto = await _queryService.GetSkuStockAsync(skuId, context.CancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU stock {skuId} not found"));
        }

        return new SkuStock
        {
            SkuIdStr = dto.SkuId.ToString(),
            Available = dto.Available,
            Reserved = dto.Reserved
        };
    }

    public override async Task<ProductDetail> GetProductDetail(GetProductDetailRequest request, ServerCallContext context)
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，spu_id_str 为唯一标识形态
        if (!Guid.TryParse(request.SpuIdStr, out var spuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or missing spu_id_str: {request.SpuIdStr}"));
        }

        var dto = await _queryService.GetSpuDetailAsync(spuId, context.CancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SPU {spuId} not found"));
        }

        var detail = new ProductDetail
        {
            SpuIdStr = dto.SpuId.ToString(),
            Title = dto.Title,
            Description = dto.Description,
            SellerIdStr = dto.SellerId.ToString()
        };

        foreach (var sku in dto.Skus)
        {
            detail.Skus.Add(new SkuInfo
            {
                SkuIdStr = sku.SkuId.ToString(),
                Title = sku.Title,
                MainImage = sku.MainImageUrl,
                // 修复审计 #12：PriceCents 从截断改为四舍五入
                PriceCents = (long)Math.Round(sku.Price * 100m, MidpointRounding.AwayFromZero),
                Currency = sku.Currency,
                Stock = sku.Stock,
                Status = sku.Status
            });
        }

        return detail;
    }

    public override async Task<GetLowStockByShopResponse> GetLowStockByShop(
        GetLowStockByShopRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ShopIdStr, out var shopId))
        {
            return new GetLowStockByShopResponse();
        }

        var items = await _queryService.GetLowStockByShopAsync(shopId, request.Threshold, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new GetLowStockByShopResponse();
        response.Items.AddRange(items.Select(x => new LowStockSkuItem
        {
            SkuIdStr = x.SkuId.ToString(),
            ProductIdStr = x.ProductId.ToString(),
            ProductName = x.ProductName ?? string.Empty,
            SkuName = x.SkuName ?? string.Empty,
            Stock = x.Stock,
            Threshold = x.Threshold,
            ShopIdStr = x.ShopId.ToString()
        }));
        return response;
    }

    private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
    {
        Title = dto.Title,
        MainImage = dto.MainImageUrl,
        // 修复审计 #12：PriceCents 从截断改为四舍五入
        PriceCents = (long)Math.Round(dto.Price * 100m, MidpointRounding.AwayFromZero),
        Currency = dto.Currency,
        Salable = dto.Available,
        Stock = dto.Stock,
        Status = dto.Status,
        ShopId = dto.ShopId?.ToString() ?? string.Empty,
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L,
        // Guid→string 迁移权威字段（C3 后 int64 字段已从契约删除）
        SkuIdStr = dto.SkuId.ToString(),
        SpuIdStr = dto.SpuId.ToString(),
        SellerIdStr = dto.SellerId.ToString()
    };
}

internal static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dt)
        => new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds();
}

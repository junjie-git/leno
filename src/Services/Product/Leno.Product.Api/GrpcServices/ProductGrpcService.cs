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
        // 优先读 string 字段（Guid.ToString()），回退到 int64（向后兼容旧客户端）
        Guid skuId;
        if (!string.IsNullOrEmpty(request.SkuIdStr))
        {
            if (!Guid.TryParse(request.SkuIdStr, out skuId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid sku_id_str: {request.SkuIdStr}"));
            }
        }
        else
        {
            // 旧客户端回退：int64 → Guid（X16 十六进制反序列化）
            skuId = new Guid(Convert.FromHexString(request.SkuId.ToString("X16")));
        }

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
        // 优先读 string 字段，回退到 int64
        List<Guid> skuIds;
        if (request.SkuIdsStr.Count > 0)
        {
            skuIds = request.SkuIdsStr.Select(Guid.Parse).ToList();
        }
        else
        {
            skuIds = request.SkuIds.Select(id => new Guid(Convert.FromHexString(id.ToString("X16")))).ToList();
        }

        var dtos = await _queryService.GetSkuInfosBatchAsync(skuIds, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new BatchGetSkuInfoResponse();
        response.Skus.AddRange(dtos.Select(MapToProto));
        return response;
    }

    public override Task<SkuStock> GetSkuStock(GetSkuStockRequest request, ServerCallContext context)
    {
        // POC 阶段未实现库存查询，返回占位（后续阶段补齐）
        // 双写 int64 + string ID 字段，保持向后兼容
        var stock = new SkuStock
        {
            Available = 0,
            Reserved = 0
        };
        if (!string.IsNullOrEmpty(request.SkuIdStr))
        {
            stock.SkuIdStr = request.SkuIdStr;
        }
        else
        {
            stock.SkuId = request.SkuId;
        }
        return Task.FromResult(stock);
    }

    public override Task<ProductDetail> GetProductDetail(GetProductDetailRequest request, ServerCallContext context)
    {
        // POC 阶段未实现，抛 Unimplemented
        throw new RpcException(new Status(StatusCode.Unimplemented, "GetProductDetail not implemented in POC"));
    }

    private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
    {
        // 既有 int64 字段（向后兼容，标记 deprecated）
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
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L,
        // 新增 string 字段（Guid→string 迁移，新客户端优先读）
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

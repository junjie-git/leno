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

    public override async Task<SkuStock> GetSkuStock(GetSkuStockRequest request, ServerCallContext context)
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

        var dto = await _queryService.GetSkuStockAsync(skuId, context.CancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU stock {skuId} not found"));
        }

        // 双写 int64 + string ID 字段，保持向后兼容
        return new SkuStock
        {
            SkuId = request.SkuId,
            SkuIdStr = dto.SkuId.ToString(),
            Available = dto.Available,
            Reserved = dto.Reserved
        };
    }

    public override async Task<ProductDetail> GetProductDetail(GetProductDetailRequest request, ServerCallContext context)
    {
        // 优先读 string 字段（Guid.ToString()），回退到 int64（向后兼容旧客户端）
        Guid spuId;
        if (!string.IsNullOrEmpty(request.SpuIdStr))
        {
            if (!Guid.TryParse(request.SpuIdStr, out spuId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid spu_id_str: {request.SpuIdStr}"));
            }
        }
        else
        {
            // 旧客户端回退：int64 → Guid（X16 十六进制反序列化）
            spuId = new Guid(Convert.FromHexString(request.SpuId.ToString("X16")));
        }

        var dto = await _queryService.GetSpuDetailAsync(spuId, context.CancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SPU {spuId} not found"));
        }

        var detail = new ProductDetail
        {
            SpuId = request.SpuId,
            SpuIdStr = dto.SpuId.ToString(),
            Title = dto.Title,
            Description = dto.Description,
            // 修复审计 #5：使用稳定算法替代 GetHashCode()（32 位碰撞率高）
            SellerId = GuidToInt64Stable(dto.SellerId),
            SellerIdStr = dto.SellerId.ToString()
        };

        foreach (var sku in dto.Skus)
        {
            detail.Skus.Add(new SkuInfo
            {
                SkuId = GuidToInt64Stable(sku.SkuId),
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
        // 既有 int64 字段（向后兼容，标记 deprecated）
        // 修复审计 #5：使用稳定算法替代 GetHashCode()（32 位碰撞率高）
        SkuId = GuidToInt64Stable(dto.SkuId),
        SpuId = GuidToInt64Stable(dto.SpuId),
        Title = dto.Title,
        MainImage = dto.MainImageUrl,
        // 修复审计 #12：PriceCents 从截断改为四舍五入
        PriceCents = (long)Math.Round(dto.Price * 100m, MidpointRounding.AwayFromZero),
        Currency = dto.Currency,
        Salable = dto.Available,
        SellerId = GuidToInt64Stable(dto.SellerId),
        Stock = dto.Stock,
        Status = dto.Status,
        ShopId = dto.ShopId?.ToString() ?? string.Empty,
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L,
        // 新增 string 字段（Guid→string 迁移，新客户端优先读）
        SkuIdStr = dto.SkuId.ToString(),
        SpuIdStr = dto.SpuId.ToString(),
        SellerIdStr = dto.SellerId.ToString()
    };

    /// <summary>
    /// 将 Guid 映射为 int64 的稳定算法：取 Guid 字节序列前 8 字节转 int64。
    /// 替代 GetHashCode()（32 位，碰撞率高），确保相同 Guid 始终映射到相同 int64。
    /// 注：此映射不可逆（int64 仅 8 字节，Guid 16 字节），仅用于 deprecated int64 字段的向后兼容。
    /// 新客户端应使用 XxxIdStr 字段。
    /// </summary>
    private static long GuidToInt64Stable(Guid guid)
        => BitConverter.ToInt64(guid.ToByteArray(), 0);
}

internal static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dt)
        => new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds();
}

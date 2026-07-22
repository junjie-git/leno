using Grpc.Core;
using Leno.Order.Application;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Order.Api.GrpcServices;

/// <summary>
/// 订单域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IOrderInternalQueryService"/> 业务逻辑，与 InternalOrdersController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class OrderGrpcService : OrderInternalService.OrderInternalServiceBase
{
    private readonly IOrderInternalQueryService _queryService;
    private readonly ILogger<OrderGrpcService> _logger;

    public OrderGrpcService(
        IOrderInternalQueryService queryService,
        ILogger<OrderGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<OrderStatus> GetOrderStatus(
        GetOrderStatusRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"OrderId 格式无效：{request.OrderId}"));
        }

        var dto = await _queryService.GetOrderStatusAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.OrderId} not found"));
        }

        return MapToProto(dto);
    }

    /// <summary>
    /// 查询订单卖家标识，供卖家与店铺管理域跨域归属校验调用。
    /// 双轨方案：优先读 <c>order_id_str</c>（Guid.ToString()），回退到 <c>order_id</c>（int64 X16 十六进制反序列化）。
    /// 订单不存在或为会员订阅订单（SellerId 为 null）时抛 <see cref="StatusCode.NotFound"/>。
    /// </summary>
    public override async Task<GetOrderSellerIdResponse> GetOrderSellerId(
        GetOrderSellerIdRequest request,
        ServerCallContext context)
    {
        Guid orderId;
        if (!string.IsNullOrEmpty(request.OrderIdStr))
        {
            if (!Guid.TryParse(request.OrderIdStr, out orderId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id_str: {request.OrderIdStr}"));
            }
        }
        else
        {
            // 旧客户端回退：int64 → Guid（X16 十六进制反序列化）
            orderId = new Guid(Convert.FromHexString(request.OrderId.ToString("X16")));
        }

        var sellerId = await _queryService.GetOrderSellerIdAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);
        if (sellerId is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {orderId} not found or has no seller"));
        }

        return new GetOrderSellerIdResponse
        {
            // P1-T27：原 (long)GetHashCode() 是 32 位 int 转 long，2^32 哈希碰撞率不可接受。
            // 改用 BitConverter.ToInt64(sellerId.ToByteArray(), 0) 取 Guid 前 8 字节作为 long，
            // 碰撞率降至 2^64，远低于 GetHashCode 的 2^32，作为 string 字段迁移完成前的向后兼容兜底。
            SellerId = BitConverter.ToInt64(sellerId.Value.ToByteArray(), 0),
            SellerIdStr = sellerId.ToString()
        };
    }

    private static OrderStatus MapToProto(OrderStatusResultDto dto)
    {
        var proto = new OrderStatus
        {
            OrderId = dto.OrderId.ToString(),
            Status = dto.Status.ToString(),  // int → string，POC 简化
            UserId = dto.UserId.ToString(),
            CompletedAt = dto.CompletedAt != default
                ? new DateTimeOffset(dto.CompletedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                : 0L,
            CreatedAt = dto.CreatedAt != default
                ? new DateTimeOffset(dto.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                : 0L
        };

        // 注：proto OrderStatus 的 payment_status/shipping_status/cancelled_at/seller_id 字段
        // 当前 DTO 未提供，留默认值（向后兼容）

        foreach (var item in dto.Items)
        {
            // 双写：int64 字段（P1-T27：BitConverter.ToInt64 取 Guid 前 8 字节）+ string 字段（Guid.ToString()）
            proto.Items.Add(new OrderItem
            {
                SkuId = BitConverter.ToInt64(item.SkuId.ToByteArray(), 0),
                SkuIdStr = item.SkuId.ToString(),
                Quantity = item.Quantity
                // sku_name/sub_total_cents 当前 DTO 未提供，留默认值
            });
        }

        return proto;
    }
}

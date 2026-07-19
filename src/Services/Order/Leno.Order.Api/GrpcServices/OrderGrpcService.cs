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
            // 注：proto OrderItem 用 sku_id (int64)，POC 简化用 GetHashCode
            // 生产化阶段需将 .proto 改为 string sku_id 承载 Guid.ToString()
            proto.Items.Add(new OrderItem
            {
                SkuId = (long)item.SkuId.GetHashCode(),
                Quantity = item.Quantity
                // sku_name/sub_total_cents 当前 DTO 未提供，留默认值
            });
        }

        return proto;
    }
}

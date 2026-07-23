using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.AfterSales.Domain.Services;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.AfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 订单状态查询 gRPC 防腐层客户端（售后 BC 独立维护，M4 双轨方案）。
/// 实现 <see cref="IOrderStatusProvider"/>，与 <see cref="HttpOrderStatusProvider"/>（HttpClient）双轨。
/// 由 <see cref="AntiCorruptionDispatcher{TService}"/> 在运行时选择使用本类或 HttpClient 实现。
/// </summary>
public sealed class GrpcOrderStatusProvider
    : GrpcAntiCorruptionClientBase, IOrderStatusProvider
{
    private const string TargetBc = "Order";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly OrderInternalService.OrderInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "order";

    public GrpcOrderStatusProvider(
        OrderInternalService.OrderInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcOrderStatusProvider> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<OrderStatusInfo?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
        => ExecuteAsync("get_order_status", async token =>
        {
            var request = new GetOrderStatusRequest { OrderId = orderId.ToString() };
            var metadata = BuildMetadata();
            var response = await _client.GetOrderStatusAsync(request, metadata, cancellationToken: token)
                .ConfigureAwait(false);
            return (OrderStatusInfo?)MapToInfo(response);
        }, ct);

    private Metadata BuildMetadata()
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
        {
            metadata.Add(InternalKeyHeader, key);
        }
        return metadata;
    }

    private static OrderStatusInfo MapToInfo(OrderStatus proto)
    {
        // 合并审计 3.5：关键字段解析失败抛 AntiCorruptionException 而非静默 Guid.Empty，
        // 避免下游业务在不知情情况下使用空 Guid 创建聚合或做归属校验。
        if (!Guid.TryParse(proto.OrderId, out var orderId) || orderId == Guid.Empty)
        {
            throw new AntiCorruptionException(
                $"订单域返回无效 OrderId：{proto.OrderId}", "ORDER_REMOTE_FAILED");
        }

        var userId = proto.HasUserId && Guid.TryParse(proto.UserId, out var uid) ? uid : Guid.Empty;
        if (userId == Guid.Empty)
        {
            throw new AntiCorruptionException(
                $"订单域返回无效 UserId：OrderId={orderId}", "ORDER_REMOTE_FAILED");
        }

        var sellerId = proto.HasSellerId && Guid.TryParse(proto.SellerId, out var sid) ? sid : Guid.Empty;
        if (sellerId == Guid.Empty)
        {
            throw new AntiCorruptionException(
                $"订单域返回无效 SellerId：OrderId={orderId}", "ORDER_REMOTE_FAILED");
        }

        var info = new OrderStatusInfo
        {
            // 注：proto OrderStatus.status 为 string，DTO 为 int
            Status = int.TryParse(proto.Status, out var s) ? s : 0,
            UserId = userId,
            // 从订单域 proto 读取真实 SellerId，防止客户端伪造
            SellerId = sellerId,
            CompletedAt = proto.CompletedAt != 0
                ? DateTimeOffset.FromUnixTimeSeconds(proto.CompletedAt).UtcDateTime
                : default,
            CreatedAt = proto.CreatedAt != 0
                ? DateTimeOffset.FromUnixTimeSeconds(proto.CreatedAt).UtcDateTime
                : default,
            OrderId = orderId
        };

        foreach (var item in proto.Items)
        {
            // 合并审计 3.5：SkuId 由 sku_id_str 解析，失败抛异常避免静默 Guid.Empty。
            // 注：order.proto 已定义 order_line_id/spu_id（字段 7/8），但 Generated/Order.cs
            // 未随 proto 重新生成，gRPC OrderItem 暂不暴露这两个字段，故此处无法填充
            // OrderLineId/SpuId；待契约重新生成后恢复按审计 3.5 校验并填充。
            // M4 Guid→string 迁移：优先读 sku_id_str
            if (string.IsNullOrEmpty(item.SkuIdStr) || !Guid.TryParse(item.SkuIdStr, out var skuId) || skuId == Guid.Empty)
            {
                throw new AntiCorruptionException(
                    $"订单域返回无效 SkuId：OrderId={orderId}", "ORDER_REMOTE_FAILED");
            }

            info.Items.Add(new OrderItemStatusInfo
            {
                // gRPC 契约未生成 order_line_id/spu_id，暂留 Guid.Empty；售后按订单行匹配需走 HttpClient 双轨。
                OrderLineId = Guid.Empty,
                SkuId = skuId,
                SpuId = Guid.Empty,
                // 从订单级别复制 SellerId 到行级别，供售后聚合创建时使用（P0-2.7）
                SellerId = sellerId,
                Quantity = item.Quantity,
                AfterSalesStatus = 0  // proto 未提供
            });
        }

        return info;
    }
}

using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ReviewAfterSales.Infrastructure.Services.Grpc;

/// <summary>
/// 订单状态查询 gRPC 防腐层客户端（M4 双轨方案）。
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
        ILogger<GrpcOrderStatusProvider> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger; // 保留参数供未来扩展，当前基类不使用 logger
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
        var info = new OrderStatusInfo
        {
            // 注：proto OrderStatus.status 为 string，DTO 为 int，POC 简化用 int.Parse
            Status = int.TryParse(proto.Status, out var s) ? s : 0,
            UserId = proto.HasUserId && Guid.TryParse(proto.UserId, out var uid) ? uid : Guid.Empty,
            CompletedAt = proto.CompletedAt != 0
                ? DateTimeOffset.FromUnixTimeSeconds(proto.CompletedAt).UtcDateTime
                : default,
            CreatedAt = proto.CreatedAt != 0
                ? DateTimeOffset.FromUnixTimeSeconds(proto.CreatedAt).UtcDateTime
                : default,
            OrderId = Guid.TryParse(proto.OrderId, out var oid) ? oid : Guid.Empty
        };

        foreach (var item in proto.Items)
        {
            // 注：proto OrderItem 无 order_line_id 字段，POC 简化为 Guid.Empty
            info.Items.Add(new OrderItemStatusInfo
            {
                OrderLineId = Guid.Empty,
                // M4 Guid→string 迁移：优先读 sku_id_str，回退到 Guid.Empty（POC 阶段 int64→Guid 不可逆）
                SkuId = !string.IsNullOrEmpty(item.SkuIdStr) ? Guid.Parse(item.SkuIdStr) : Guid.Empty,
                Quantity = item.Quantity,
                AfterSalesStatus = 0  // proto 未提供
            });
        }

        return info;
    }
}

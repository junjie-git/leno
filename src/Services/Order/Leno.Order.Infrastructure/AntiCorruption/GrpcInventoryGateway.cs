using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Leno.Order.Application.Abstractions;
using Leno.Order.Domain.Exceptions;
using Leno.SharedContracts.Grpc.Inventory.V1;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.AntiCorruption;

/// <summary>
/// 库存防腐层网关的 gRPC 实现 —— Order BC 与 Inventory BC 之间唯一的库存交互通道。
/// <para>
/// 双轨下线 DEC-4（2026-09-22）：旧实现（Order 侧 <c>RedisInventoryRepository</c> 直写
/// <c>inventory:stock:*</c> 键 + 本地补偿后台服务）已废除 —— 库存数量以 Inventory BC 的
/// SQL 台账/基线为唯一权威，Order 不再持有任何库存存储。
/// </para>
/// <para>
/// 地址来源：配置键 <c>Inventory:GrpcUrl</c>（本地默认 http://localhost:5265；
/// Docker/K8s 为 http://leno-inventory-api:5265，Helm 的 grpcPort 已预留 5265）。
/// 预占为同步 gRPC（下单成败需要即时应答）；确认/释放/归还为 MassTransit 命令（异步，
/// Inventory 侧按订单幂等，重发安全）。
/// </para>
/// </summary>
public sealed class GrpcInventoryGateway : IInventoryGateway
{
    private readonly InventoryInternalService.InventoryInternalServiceClient _client;
    private readonly IBus _bus;
    private readonly ILogger<GrpcInventoryGateway> _logger;

    public GrpcInventoryGateway(
        InventoryInternalService.InventoryInternalServiceClient client,
        IBus bus,
        ILogger<GrpcInventoryGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// 由配置创建 gRPC 通道与客户端（DI 以单例注册本网关，通道全进程复用）。
    /// </summary>
    public static GrpcInventoryGateway Create(
        IConfiguration configuration, IBus bus, ILogger<GrpcInventoryGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var address = configuration["Inventory:GrpcUrl"]
            ?? throw new InvalidOperationException(
                "缺少 Inventory:GrpcUrl 配置（Inventory BC gRPC 地址）。" +
                "本地默认 http://localhost:5265；容器环境为 http://leno-inventory-api:5265。");
        var channel = GrpcChannel.ForAddress(address);
        // 内部鉴权：附加 x-internal-key（与服务端拦截器/HTTP 内部面同一密钥源）
        var invoker = channel.Intercept(
            new InternalApiKeyClientInterceptor(configuration["InternalAuth:ApiKey"]));
        var client = new InventoryInternalService.InventoryInternalServiceClient(invoker);
        return new GrpcInventoryGateway(client, bus, logger);
    }

    /// <inheritdoc />
    public async Task<bool> ReserveBatchAsync(
        Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuQuantities);
        if (skuQuantities.Count == 0)
        {
            _logger.LogWarning("库存预占：明细为空，直接返回成功 OrderId={OrderId}", orderId);
            return true;
        }

        var request = new ReserveStockRequest
        {
            OrderId = orderId.ToString(),
            IdempotencyKey = Guid.NewGuid().ToString()
        };
        foreach (var (skuId, quantity) in skuQuantities)
        {
            request.Items.Add(new global::Leno.SharedContracts.Grpc.Inventory.V1.ReserveStockItem
            {
                SkuId = skuId.ToString(),
                Quantity = quantity
            });
        }

        try
        {
            var response = await _client.ReserveStockAsync(request, cancellationToken: ct)
                .ConfigureAwait(false);
            if (!response.Success)
            {
                _logger.LogWarning("库存预占被 Inventory 拒绝 OrderId={OrderId} Reason={Reason}",
                    orderId, response.FailureReason);
                return false;
            }

            _logger.LogInformation("库存预占成功 OrderId={OrderId} ItemCount={Count}",
                orderId, skuQuantities.Count);
            return true;
        }
        catch (RpcException ex)
        {
            // 基础设施级失败（网络/服务不可用）：不可静默当作"库存不足"，向调用方抛出由编排器处置
            _logger.LogError(ex, "库存预占 gRPC 调用失败 OrderId={OrderId} Status={Status}",
                orderId, ex.StatusCode);
            throw new OrderDomainException(
                $"库存服务暂不可用：{ex.StatusCode}", ex, "STOCK_SERVICE_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task ConfirmBatchAsync(Guid orderId, CancellationToken ct = default)
        => await PublishReleaseOrConfirmAsync(
            new ConfirmStockCommand(orderId, Guid.NewGuid()), orderId, "Confirm", ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ReleaseBatchAsync(Guid orderId, CancellationToken ct = default)
        => await PublishReleaseOrConfirmAsync(
            new ReleaseStockCommand(orderId, Guid.NewGuid(), ReleaseStockOperationType.Release),
            orderId, "Release", ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ReturnDeductedBatchAsync(Guid orderId, CancellationToken ct = default)
        => await PublishReleaseOrConfirmAsync(
            new ReleaseStockCommand(orderId, Guid.NewGuid(), ReleaseStockOperationType.ReturnDeducted),
            orderId, "ReturnDeducted", ct)
            .ConfigureAwait(false);

    private async Task PublishReleaseOrConfirmAsync<T>(T command, Guid orderId, string action, CancellationToken ct)
        where T : class
    {
        // 异步命令：Inventory 侧按订单幂等（台账唯一约束 + 状态机），重发安全；
        // 失败由 MassTransit 重试承担，仍失败进入死信队列（SystemAdmin DLQ 管理人工介入）
        await _bus.Publish(command, ct).ConfigureAwait(false);
        _logger.LogInformation("库存{Action}命令已发布 OrderId={OrderId}", action, orderId);
    }
}

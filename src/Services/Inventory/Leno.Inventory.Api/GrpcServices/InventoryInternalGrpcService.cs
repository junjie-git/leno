using Grpc.Core;
using Leno.Inventory.Application;
using Leno.SharedContracts.Grpc.Inventory.V1;
using Leno.SharedContracts.Integration.Inventory;
// 命令明细与 gRPC 消息类型同名（ReserveStockItem）：命令版用于应用服务入参，gRPC 版仅出现在请求/响应消息中
using ReserveStockItem = Leno.SharedContracts.Integration.Inventory.ReserveStockItem;

namespace Leno.Inventory.Api.GrpcServices;

/// <summary>
/// 库存内部 gRPC 服务 —— <see cref="InventoryInternalService.InventoryInternalServiceBase"/> 的实现。
/// <para>
/// 同步库存操作的唯一入口（双轨下线 DEC-4，2026-09-22）：Order BC 下单预占与库存查询经此调用，
/// 底层统一走 <see cref="IInventoryAppService"/>（同事务更新台账与基线）。
/// proto 头注自述的"GrpcService 实现待接入（遗留项）"由本类落地。
/// </para>
/// <para>
/// 错误语义：业务失败（库存不足）以 <c>Success=false + FailureReason</c> 返回（正常业务路径）；
/// 参数非法抛 <c>InvalidArgument</c>；基础设施异常向上抛出由调用方重试。
/// </para>
/// </summary>
public sealed class InventoryInternalGrpcService : InventoryInternalService.InventoryInternalServiceBase
{
    private readonly IInventoryAppService _inventoryAppService;
    private readonly ILogger<InventoryInternalGrpcService> _logger;

    public InventoryInternalGrpcService(
        IInventoryAppService inventoryAppService,
        ILogger<InventoryInternalGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryAppService = inventoryAppService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ReserveStockResponse> ReserveStock(
        ReserveStockRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = context.CancellationToken;

        var orderId = ParseGuid(request.OrderId, "order_id");
        var idempotencyKey = ParseGuid(request.IdempotencyKey, "idempotency_key");
        var items = request.Items
            .Select(i => new ReserveStockItem(ParseGuid(i.SkuId, "items.sku_id"), i.Quantity))
            .ToList();

        var result = await _inventoryAppService.ReserveAsync(orderId, items, idempotencyKey, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogWarning("gRPC 库存预占失败 OrderId={OrderId} Reason={Reason}",
                orderId, result.FailureReason);
            return new ReserveStockResponse
            {
                Success = false,
                FailureReason = result.FailureReason ?? "库存预占失败"
            };
        }

        _logger.LogInformation("gRPC 库存预占成功 OrderId={OrderId} ItemCount={Count}",
            orderId, result.ReservedItems.Count);

        // 台账按订单寻址（命令不带 ReservationId），预留字段返回订单标识以保持契约稳定；
        // 调用方无需持久化该值
        return new ReserveStockResponse
        {
            Success = true,
            ReservationId = orderId.ToString()
        };
    }

    /// <inheritdoc />
    public override async Task<ConfirmStockResponse> ConfirmStock(
        ConfirmStockRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = context.CancellationToken;

        var orderId = ParseGuid(request.OrderId, "order_id");
        var idempotencyKey = ParseGuid(request.IdempotencyKey, "idempotency_key");

        try
        {
            await _inventoryAppService.ConfirmAsync(orderId, idempotencyKey, ct).ConfigureAwait(false);
            return new ConfirmStockResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC 库存确认失败 OrderId={OrderId}", orderId);
            return new ConfirmStockResponse
            {
                Success = false,
                FailureReason = "库存确认失败，请重试"
            };
        }
    }

    /// <inheritdoc />
    public override async Task<ReleaseStockResponse> ReleaseStock(
        ReleaseStockRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = context.CancellationToken;

        var orderId = ParseGuid(request.OrderId, "order_id");
        var idempotencyKey = ParseGuid(request.IdempotencyKey, "idempotency_key");
        if (request.OperationType is not ((int)ReleaseStockOperationType.Release)
            and not ((int)ReleaseStockOperationType.ReturnDeducted))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                $"未知的释放操作类型 {request.OperationType}"));
        }

        var operationType = (ReleaseStockOperationType)request.OperationType;

        try
        {
            await _inventoryAppService.ReleaseAsync(orderId, operationType, idempotencyKey, ct)
                .ConfigureAwait(false);
            return new ReleaseStockResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC 库存释放失败 OrderId={OrderId} OpType={OpType}", orderId, operationType);
            return new ReleaseStockResponse
            {
                Success = false,
                FailureReason = "库存释放失败，请重试"
            };
        }
    }

    /// <inheritdoc />
    public override async Task<AvailableStockResponse> GetAvailableStock(
        GetAvailableStockRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = context.CancellationToken;

        var skuId = ParseGuid(request.SkuId, "sku_id");
        var sellable = await _inventoryAppService.GetSellableAsync(skuId, ct).ConfigureAwait(false);

        return new AvailableStockResponse { AvailableQty = sellable };
    }

    private static Guid ParseGuid(string value, string field)
    {
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"字段 {field} 不是合法的 GUID：{value}"));
        }

        return parsed;
    }
}

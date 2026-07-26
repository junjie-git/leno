using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Payment.Application.Services;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Payment.Infrastructure.Services.Grpc;

/// <summary>
/// 订单支付上下文 gRPC 防腐层客户端（支付 BC 视角）。
/// 实现 <see cref="IPaymentOrderAntiCorruptionService"/>，供 POST /api/payments 同步发起支付时校验订单。
/// 通过 <see cref="OrderInternalService.OrderInternalServiceClient.GetOrderStatus"/> 获取订单状态、归属与明细，
/// 屏蔽订单域具体实现，应用层仅依赖 <see cref="IPaymentOrderAntiCorruptionService"/> 抽象。
/// </summary>
/// <remarks>
/// 防腐层职责：
/// <list type="bullet">
/// <item>调用订单域 gRPC <c>GetOrderStatus</c> 获取订单状态、归属用户、订单明细；</item>
/// <item>将订单域 <c>OrderStatus</c> 枚举的字符串值（"0"=PendingPayment）映射为本上下文的 <see cref="OrderPaymentContext.IsPayable"/> 布尔值，
/// 避免跨域枚举依赖（INV-PAY-01）；</item>
/// <item>从订单明细 <c>sub_total_cents</c> 之和推导应付金额（分→元），保证支付单金额与订单应付一致，
/// 防 buyer 端伪造金额（INV-PAY-01）；</item>
/// <item>订单不存在时（gRPC <c>NotFound</c>）返回 <c>null</c>，由应用层映射为 404；</item>
/// <item>网络故障或订单域返回异常时抛 <see cref="AntiCorruptionException"/>，由全局异常中间件映射为 503。</item>
/// </list>
/// </remarks>
public sealed class GrpcPaymentOrderAntiCorruptionService
    : GrpcAntiCorruptionClientBase, IPaymentOrderAntiCorruptionService
{
    private const string TargetBc = "Order";
    private const string InternalKeyHeader = "x-internal-key";

    /// <summary>订单域 OrderStatus.PendingPayment 的整型值（待支付）。
    /// 见 Leno.Order.Domain.ValueObjects.OrderEnums，支付域不直接引用订单域枚举，仅以常量值约定跨域契约。</summary>
    private const int OrderStatusPendingPayment = 0;

    /// <summary>订单域 OrderStatus.PendingPayment 的字符串名（待支付），作为 proto.Status 字符串形式兜底匹配。</summary>
    private const string OrderStatusPendingPaymentName = "PendingPayment";

    private readonly OrderInternalService.OrderInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcPaymentOrderAntiCorruptionService> _logger;

    protected override string ServiceName => "order";

    public GrpcPaymentOrderAntiCorruptionService(
        OrderInternalService.OrderInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcPaymentOrderAntiCorruptionService> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<OrderPaymentContext?> GetOrderPaymentContextAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_order_payment_context", async token =>
            {
                var request = new GetOrderStatusRequest { OrderId = orderId.ToString() };
                var metadata = BuildMetadata();
                var response = await _client.GetOrderStatusAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return MapToContext(orderId, response);
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex) when (IsNotFound(ex))
        {
            // 订单不存在：gRPC 返回 NotFound 状态码，基类已包装为 AntiCorruptionException（ORDER_REMOTE_FAILED）。
            // 此处捕获并返回 null，由应用层映射为 404（订单不存在）语义，符合 spec F-PAY-001。
            _logger.LogInformation("订单域返回 NotFound，订单不存在 OrderId={OrderId}", orderId);
            return null;
        }
    }

    /// <summary>
    /// 判断异常是否由 gRPC <c>StatusCode.NotFound</c> 引起（订单不存在）。
    /// 基类 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 将所有 RpcException 包装为
    /// <see cref="AntiCorruptionException"/> 并保留 InnerException，本方法据此判断是否为 NotFound。
    /// </summary>
    private static bool IsNotFound(AntiCorruptionException ex)
    {
        if (ex.InnerException is RpcException rpc && rpc.StatusCode == StatusCode.NotFound)
        {
            return true;
        }
        // 兜底：错误码 ORDER_REMOTE_FAILED 且消息含 NotFound 时也视为订单不存在
        return ex.ErrorCode == "ORDER_REMOTE_FAILED"
            && ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// 将订单域 <see cref="OrderStatus"/> proto 映射为支付域 <see cref="OrderPaymentContext"/>。
    /// </summary>
    /// <param name="inputOrderId">调用方传入的订单标识（用于校验 proto 返回的 OrderId 一致）。</param>
    /// <param name="proto">订单域返回的 <see cref="OrderStatus"/> proto。</param>
    /// <returns>订单支付上下文；proto 字段非法时抛 <see cref="AntiCorruptionException"/>（fail-fast）。</returns>
    private static OrderPaymentContext MapToContext(Guid inputOrderId, OrderStatus proto)
    {
        // 校验 proto 返回的 OrderId 与调用方传入一致，防止订单域返回错单
        if (!Guid.TryParse(proto.OrderId, out var protoOrderId) || protoOrderId == Guid.Empty)
        {
            throw new AntiCorruptionException(
                $"订单域返回无效 OrderId：{proto.OrderId}", "ORDER_REMOTE_FAILED");
        }
        if (protoOrderId != inputOrderId)
        {
            throw new AntiCorruptionException(
                $"订单域返回 OrderId 不一致：期望 {inputOrderId} 实际 {protoOrderId}", "ORDER_REMOTE_FAILED");
        }

        // 校验 UserId：proto.user_id 为 optional，缺失或解析失败视为非法（防静默 Guid.Empty）
        if (!proto.HasUserId || string.IsNullOrEmpty(proto.UserId)
            || !Guid.TryParse(proto.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new AntiCorruptionException(
                $"订单域返回无效 UserId：OrderId={inputOrderId}", "ORDER_REMOTE_FAILED");
        }

        // 解析订单状态：proto.Status 为 OrderStatus 枚举值的字符串形式（"0"=PendingPayment），
        // 仅待支付态视为可发起支付（INV-PAY-01）。支付域不直接引用订单域枚举，仅以常量值约定。
        if (!int.TryParse(proto.Status, out var statusInt))
        {
            // 兼容状态名（如 "PendingPayment"）的兜底解析：仅识别待支付态，其他状态名视为不可支付
            statusInt = string.Equals(proto.Status, OrderStatusPendingPaymentName, StringComparison.OrdinalIgnoreCase)
                ? OrderStatusPendingPayment
                : -1;
        }
        var isPayable = statusInt == OrderStatusPendingPayment;

        // 计算应付金额（分→元）：优先累加 sub_total_cents，缺失时回退 unit_price_cents * quantity。
        // 订单域 gRPC 当前未填充 sub_total_cents/unit_price_cents 时返回 0，应用层将抛 PAYMENT_AMOUNT_INVALID 拒绝发起支付，
        // 待订单域 gRPC 补全金额字段后自动恢复可用，本防腐层逻辑无需变更。
        long totalCents = 0L;
        foreach (var item in proto.Items)
        {
            var subTotalCents = item.HasSubTotalCents && item.SubTotalCents > 0
                ? item.SubTotalCents
                : item.UnitPriceCents * item.Quantity;
            if (subTotalCents > 0)
            {
                totalCents += subTotalCents;
            }
        }
        var amount = totalCents / 100m;

        return new OrderPaymentContext
        {
            OrderId = protoOrderId,
            UserId = userId,
            IsPayable = isPayable,
            Amount = amount,
            Currency = "CNY"
        };
    }
}

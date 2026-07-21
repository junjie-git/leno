using Leno.Order.Application.DTOs;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Services;

/// <summary>
/// 多卖家拆单 Saga 编排器，按顺序执行每组（预占库存 → 冻结积分 → 保存订单），
/// 任一组失败时对已成功组执行补偿（释放库存/积分/优惠券、移除未提交的订单聚合），最终抛原始异常。
/// 全部组成功后在统一工作单元提交（<see cref="IUnitOfWork.SaveEntitiesAsync"/>），保证"要么全部持久化、要么全部不持久化"。
/// </summary>
public sealed class OrderSagaOrchestrator : IOrderSagaOrchestrator
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IStockReservationDomainService _stockService;
    private readonly IOrderPricingDomainService _pricingService;
    private readonly IFreightCalculator _freightCalculator;
    private readonly IPromotionAntiCorruptionService _promotionAntiCorruption;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;
    private readonly IBus _bus;
    private readonly ILogger<OrderSagaOrchestrator> _logger;

    public OrderSagaOrchestrator(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator orderNumberGenerator,
        IStockReservationDomainService stockService,
        IOrderPricingDomainService pricingService,
        IFreightCalculator freightCalculator,
        IPromotionAntiCorruptionService promotionAntiCorruption,
        IPointsAntiCorruptionService pointsAntiCorruption,
        IBus bus,
        ILogger<OrderSagaOrchestrator> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
        _stockService = stockService;
        _pricingService = pricingService;
        _freightCalculator = freightCalculator;
        _promotionAntiCorruption = promotionAntiCorruption;
        _pointsAntiCorruption = pointsAntiCorruption;
        _bus = bus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderSagaResult> ExecuteAsync(OrderSagaContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var completed = new List<CompletedGroup>();
        foreach (var group in context.Groups)
        {
            try
            {
                completed.Add(await ExecuteGroupAsync(context.UserId, context.Address, group, ct));
            }
            catch (Exception)
            {
                // 任一组失败：补偿已成功组后向上抛原始异常（库存/积分/券/订单聚合回滚）
                await CompensateAsync(completed, CancellationToken.None);
                throw;
            }
        }

        // 全部组成功 → 统一提交工作单元（订单聚合 + 发件箱集成事件同事务持久化）
        await _unitOfWork.SaveEntitiesAsync(ct);

        // SaveEntitiesAsync 成功后统一调度超时延迟消息（保证订单已持久化，避免 Saga 失败回滚后产生幽灵延迟消息）
        foreach (var g in completed)
        {
            var scheduler = _bus.CreateMessageScheduler();
            await scheduler.ScheduleSend(
                new Uri("queue:order-timeout"),
                g.Order.ExpireAt,
                new OrderTimeoutMessage(g.OrderId),
                ct);
        }

        return new OrderSagaResult
        {
            FirstResult = OrderCreatedResult.FromOrder(completed[0].Order),
            Results = completed.Select(c => OrderCreatedResult.FromOrder(c.Order)).ToList()
        };
    }

    /// <summary>
    /// 执行单组下单流程：构建明细 → 价格校验 → 优惠计算 → 运费 → 预占库存 → 冻结积分 → 创建订单聚合 → 入库追踪。
    /// 积分冻结失败时执行组内回滚（释放已预占库存）后向上抛（Task 8 单组原子回滚）。
    /// 超时延迟消息调度由 <see cref="ExecuteAsync"/> 在 <see cref="IUnitOfWork.SaveEntitiesAsync"/> 成功后统一执行。
    /// </summary>
    private async Task<CompletedGroup> ExecuteGroupAsync(
        Guid userId,
        AddressSnapshot address,
        OrderSagaGroupInput group,
        CancellationToken ct)
    {
        // 构建订单明细与 SKU 数量映射
        var orderItems = new List<OrderItem>();
        var skuQuantities = new Dictionary<Guid, int>();
        var itemSubtotals = new List<(Guid SkuId, decimal Subtotal)>();
        decimal groupItemsAmount = 0;
        foreach (var ci in group.Items)
        {
            var info = group.SkuInfos[ci.SkuId];
            var snapshot = ProductSnapshot.Create(
                info.SkuId, info.SpuId, info.ProductName, info.SkuName, info.MainImage, info.SellerId);
            var orderItem = OrderItem.Create(Guid.NewGuid(), info.SkuId, snapshot, info.UnitPrice, ci.Quantity, ci.SourceCartItemId);
            orderItems.Add(orderItem);
            skuQuantities[info.SkuId] = ci.Quantity;
            itemSubtotals.Add((info.SkuId, orderItem.Subtotal));
            groupItemsAmount += orderItem.Subtotal;
        }

        // 价格防篡改校验（使用预查的 SkuInfos，避免 N+1）
        var skuPrices = itemSubtotals.Select(s => (s.SkuId, group.SkuInfos[s.SkuId].UnitPrice)).ToList();
        var skuCurrentPrices = group.SkuInfos.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.UnitPrice);
        await _pricingService.ValidatePricesAsync(skuPrices, skuCurrentPrices, ct);

        // 计算优惠并按 SKU 分摊
        var discount = await _promotionAntiCorruption.CalculateDiscountAsync(userId, itemSubtotals, ct);
        var allocations = discount > 0
            ? await _pricingService.CalculateAndAllocateAsync(discount, itemSubtotals, ct)
            : new List<(Guid SkuId, decimal Allocation)>();

        // 计算运费
        var quantity = group.Items.Sum(i => i.Quantity);
        var freight = await _freightCalculator.CalculateAsync(group.SellerId, address.Province, quantity, groupItemsAmount, ct);

        // 积分抵现上限裁剪：抵现金额不得超过 商品总额 - 优惠（避免总金额为负）
        var groupPointsOffset = group.GroupPointsOffsetRaw;
        var maxOffset = groupItemsAmount - discount;
        if (groupPointsOffset > maxOffset)
        {
            groupPointsOffset = maxOffset;
        }
        if (groupPointsOffset < 0)
        {
            groupPointsOffset = 0m;
        }
        var groupPoints = (group.UsePoints && groupPointsOffset > 0)
            ? (int)Math.Round(groupPointsOffset * 100m, MidpointRounding.ToEven)
            : 0;

        // 预占库存
        var orderId = Guid.NewGuid();
        var reserved = await _stockService.ReserveBatchAsync(orderId, skuQuantities, ct);
        if (!reserved)
        {
            throw new OrderDomainException("库存预占失败，SKU 库存不足", "ORDER_STOCK_RESERVE_FAILED");
        }

        // 冻结积分（Task 8 单组原子回滚：失败时释放已预占库存后向上抛）
        var pointsFrozen = false;
        if (groupPoints > 0)
        {
            try
            {
                await _pointsAntiCorruption.FreezeAsync(userId, orderId, groupPoints, ct);
                pointsFrozen = true;
            }
            catch (Exception)
            {
                await _stockService.ReleaseBatchAsync(orderId, skuQuantities, CancellationToken.None);
                throw;
            }
        }

        // 生成订单编号并创建订单聚合（积分抵现初始为 0，由 ApplyPointsOffset 校验不变量）
        var orderNo = await _orderNumberGenerator.GenerateAsync(ct);
        var order = OrderAggregate.Create(
            orderId, orderNo, OrderType.Normal, userId, group.SellerId,
            orderItems, address, freight, pointsOffsetAmount: 0m, DateTime.UtcNow.AddMinutes(30));

        // 应用优惠分摊（聚合根校验分摊总和与单项上限）
        if (discount > 0 && allocations.Count > 0)
        {
            order.ApplyDiscount(discount, allocations);
        }

        // 应用积分抵现（聚合根校验 0 ≤ pointsOffset ≤ ItemsAmount - DiscountAmount）
        // Saga 已按 maxOffset = groupItemsAmount - discount 裁剪，ApplyPointsOffset 会再次校验
        if (groupPointsOffset > 0)
        {
            order.ApplyPointsOffset(groupPointsOffset);
        }

        // 入库追踪（未提交，待 Saga 全部成功后统一 SaveEntitiesAsync）
        await _orderRepository.AddAsync(order, ct);

        return new CompletedGroup
        {
            Order = order,
            OrderId = orderId,
            SkuQuantities = skuQuantities,
            PointsFrozen = pointsFrozen,
            HasDiscount = discount > 0
        };
    }

    /// <summary>
    /// 对已成功组逆序执行补偿：释放优惠券 → 释放积分 → 释放库存 → 移除未提交的订单聚合。
    /// 每个补偿动作独立 try/catch 收集失败，全部补偿后若有失败则抛 <see cref="SagaCompensationFailedException"/> 触发告警。
    /// </summary>
    private async Task CompensateAsync(List<CompletedGroup> completed, CancellationToken ct)
    {
        var failures = new List<CompensationFailure>();

        for (var i = completed.Count - 1; i >= 0; i--)
        {
            var g = completed[i];

            // 释放优惠券（若该组涉及优惠）
            if (g.HasDiscount)
            {
                try
                {
                    await _promotionAntiCorruption.ReleaseCouponsAsync(g.OrderId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Saga 补偿：释放优惠券失败 OrderId={OrderId}", g.OrderId);
                    failures.Add(new CompensationFailure(g.OrderId, "ReleaseCoupons", ex.Message));
                }
            }

            // 释放积分（若该组已冻结积分）
            if (g.PointsFrozen)
            {
                try
                {
                    await _pointsAntiCorruption.ReleaseAsync(g.OrderId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Saga 补偿：释放积分失败 OrderId={OrderId}", g.OrderId);
                    failures.Add(new CompensationFailure(g.OrderId, "ReleasePoints", ex.Message));
                }
            }

            // 释放预占库存
            try
            {
                await _stockService.ReleaseBatchAsync(g.OrderId, g.SkuQuantities, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga 补偿：释放库存失败 OrderId={OrderId}", g.OrderId);
                failures.Add(new CompensationFailure(g.OrderId, "ReleaseStock", ex.Message));
            }

            // 移除未提交的订单聚合（Saga 失败未统一提交，聚合仅在变更跟踪器中）
            try
            {
                await _orderRepository.RemoveAsync(g.Order, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga 补偿：移除订单聚合失败 OrderId={OrderId}", g.OrderId);
                failures.Add(new CompensationFailure(g.OrderId, "RemoveOrder", ex.Message));
            }
        }

        // 有补偿失败时抛异常触发告警（库存有 T18 补偿表兜底，但积分/优惠券无补偿表）
        if (failures.Count > 0)
        {
            throw new SagaCompensationFailedException(failures);
        }
    }

    private sealed class CompletedGroup
    {
        public required OrderAggregate Order { get; init; }
        public Guid OrderId { get; init; }
        public required Dictionary<Guid, int> SkuQuantities { get; init; }
        public bool PointsFrozen { get; init; }
        public bool HasDiscount { get; init; }
    }
}

/// <summary>
/// 多卖家拆单 Saga 编排器接口。
/// </summary>
public interface IOrderSagaOrchestrator
{
    /// <summary>
    /// 按顺序执行所有分组（预占库存 → 冻结积分 → 保存订单），任一组失败时补偿已成功组并抛原始异常。
    /// 全部成功后统一提交工作单元。
    /// </summary>
    /// <param name="context">Saga 上下文，含买家标识、收货地址与分组输入。</param>
    /// <param name="ct">取消令牌。</param>
    Task<OrderSagaResult> ExecuteAsync(OrderSagaContext context, CancellationToken ct = default);
}

/// <summary>
/// Saga 上下文，承载跨分组共享的买家信息与收货地址，以及各分组输入列表。
/// </summary>
public sealed class OrderSagaContext
{
    /// <summary>买家标识。</summary>
    public required Guid UserId { get; init; }

    /// <summary>收货地址快照（各分组共享）。</summary>
    public required AddressSnapshot Address { get; init; }

    /// <summary>按卖家拆分后的分组输入列表，按顺序执行。</summary>
    public required IReadOnlyList<OrderSagaGroupInput> Groups { get; init; }
}

/// <summary>
/// 单个卖家分组的 Saga 输入，含明细、SKU 信息与积分抵现原始分摊金额。
/// 积分抵现上限裁剪（商品总额 - 优惠）在 Saga 内部优惠计算后执行。
/// </summary>
public sealed class OrderSagaGroupInput
{
    /// <summary>卖家标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>该分组的下单明细。</summary>
    public required IReadOnlyList<CheckoutItemDto> Items { get; init; }

    /// <summary>SKU 信息映射（各分组共享，按 SkuId 索引）。</summary>
    public required IReadOnlyDictionary<Guid, SkuInfo> SkuInfos { get; init; }

    /// <summary>该分组按比例分摊的积分抵现原始金额（未按优惠裁剪）。</summary>
    public decimal GroupPointsOffsetRaw { get; init; }

    /// <summary>是否启用积分抵现（<c>dto.PointsToUse &gt; 0</c>）。</summary>
    public bool UsePoints { get; init; }
}

/// <summary>
/// Saga 执行结果，含首单创建结果 DTO（多卖家拆单返回首单）与全部订单创建结果列表（P1-T23）。
/// 不再暴露 <c>OrderAggregate</c> 聚合根实例给应用层，应用层经 <see cref="OrderCreatedResult"/> DTO 访问下单结果。
/// </summary>
public sealed class OrderSagaResult
{
    /// <summary>首个订单创建结果 DTO（多卖家拆单返回首单）。</summary>
    public required OrderCreatedResult FirstResult { get; init; }

    /// <summary>全部成功创建的订单结果 DTO 列表。</summary>
    public required IReadOnlyList<OrderCreatedResult> Results { get; init; }
}

/// <summary>
/// 订单创建结果 DTO（P1-T23），表达 Saga 创建订单后的快照视图，避免应用层直接持有 <see cref="OrderAggregate"/> 聚合根实例。
/// 含下单瞬间的金额、状态、明细与超时时间等字段；生命周期字段（支付/发货/完成/取消）在创建时为默认值。
/// </summary>
public sealed class OrderCreatedResult
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>订单编号。</summary>
    public string OrderNo { get; init; } = string.Empty;

    /// <summary>订单类型。</summary>
    public OrderType OrderType { get; init; }

    /// <summary>买家标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>卖家标识，会员订阅订单为 <see cref="Guid.Empty"/>。</summary>
    public Guid SellerId { get; init; }

    /// <summary>订单状态（创建后为 <see cref="OrderStatus.PendingPayment"/>）。</summary>
    public OrderStatus Status { get; init; }

    /// <summary>商品总金额。</summary>
    public decimal ItemsAmount { get; init; }

    /// <summary>优惠总金额。</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>积分抵现金额。</summary>
    public decimal PointsOffsetAmount { get; init; }

    /// <summary>运费金额。</summary>
    public decimal FreightAmount { get; init; }

    /// <summary>订单总金额（实付）。</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>支付截止时间（UTC）。</summary>
    public DateTime ExpireAt { get; init; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>订单明细 DTO 列表。</summary>
    public required IReadOnlyList<OrderItemDto> Items { get; init; }

    /// <summary>
    /// 由订单聚合根构造创建结果 DTO（P1-T23）。
    /// 仅读取聚合根当前状态做快照映射，不持有聚合根引用，避免应用层绕过聚合根方法。
    /// </summary>
    /// <param name="order">订单聚合根实例。</param>
    /// <returns>下单瞬间的快照 DTO。</returns>
    public static OrderCreatedResult FromOrder(OrderAggregate order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var items = order.Items.Select(i => new OrderItemDto
        {
            SkuId = i.SkuId,
            ProductName = i.ProductSnapshot.ProductName,
            SkuName = i.ProductSnapshot.SkuName,
            MainImage = i.ProductSnapshot.MainImage,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            DiscountAllocation = i.DiscountAllocation,
            Subtotal = i.Subtotal
        }).ToList();

        return new OrderCreatedResult
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            OrderType = order.OrderType,
            UserId = order.UserId,
            SellerId = order.SellerId ?? Guid.Empty,
            Status = order.Status,
            ItemsAmount = order.ItemsAmount,
            DiscountAmount = order.DiscountAmount,
            PointsOffsetAmount = order.PointsOffsetAmount,
            FreightAmount = order.FreightAmount,
            TotalAmount = order.TotalAmount,
            ExpireAt = order.ExpireAt,
            CreatedAt = order.CreatedAt,
            Items = items
        };
    }
}

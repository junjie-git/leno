using Leno.Order.Application.DTOs;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Services;

/// <summary>
/// 订单应用服务实现，编排下单、支付、发货、确认收货、取消与查询用例。
/// 下单按卖家自动拆单：分别预占库存、冻结积分、生成订单号并创建订单聚合。
/// </summary>
public sealed class OrderAppService : IOrderAppService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IStockReservationDomainService _stockService;
    private readonly IOrderPricingDomainService _pricingService;
    private readonly IFreightCalculator _freightCalculator;
    private readonly IProductAntiCorruptionService _productAntiCorruption;
    private readonly IPromotionAntiCorruptionService _promotionAntiCorruption;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;
    private readonly ILogisticsTrackingService _logisticsTrackingService;
    private readonly ILogisticsCompanyRepository _logisticsCompanyRepository;
    private readonly IEventBus _eventBus;
    private readonly IBus _bus;

    public OrderAppService(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator orderNumberGenerator,
        IStockReservationDomainService stockService,
        IOrderPricingDomainService pricingService,
        IFreightCalculator freightCalculator,
        IProductAntiCorruptionService productAntiCorruption,
        IPromotionAntiCorruptionService promotionAntiCorruption,
        IPointsAntiCorruptionService pointsAntiCorruption,
        ILogisticsTrackingService logisticsTrackingService,
        ILogisticsCompanyRepository logisticsCompanyRepository,
        IEventBus eventBus,
        IBus bus)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
        _stockService = stockService;
        _pricingService = pricingService;
        _freightCalculator = freightCalculator;
        _productAntiCorruption = productAntiCorruption;
        _promotionAntiCorruption = promotionAntiCorruption;
        _pointsAntiCorruption = pointsAntiCorruption;
        _logisticsTrackingService = logisticsTrackingService;
        _logisticsCompanyRepository = logisticsCompanyRepository;
        _eventBus = eventBus;
        _bus = bus;
    }

    /// <inheritdoc />
    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto, CancellationToken ct = default)
    {
        // 构建收货地址快照
        var address = AddressSnapshot.Create(
            dto.RecipientName, dto.RecipientPhone, dto.Province, dto.City, dto.District, dto.Detail);

        // 查询全部 SKU 信息并校验在售
        var skuInfos = new Dictionary<Guid, SkuInfo>();
        foreach (var ci in dto.Items)
        {
            var info = await _productAntiCorruption.GetSkuInfoAsync(ci.SkuId, ct)
                ?? throw new OrderDomainException($"SKU {ci.SkuId} 不存在或已下架", "ORDER_SKU_NOT_FOUND", 404);
            if (!info.IsOnSale)
            {
                throw new OrderDomainException($"SKU {ci.SkuId} 已下架", "ORDER_SKU_OFF_SHELF");
            }
            skuInfos[ci.SkuId] = info;
        }

        // 按卖家分组（多卖家自动拆单）
        var groups = dto.Items
            .GroupBy(i => skuInfos[i.SkuId].SellerId)
            .Select(g => new { SellerId = g.Key, Items = g.ToList() })
            .ToList();

        // 总积分抵现金额与总商品金额，用于按比例分摊积分到各卖家订单
        var totalPointsOffset = dto.PointsToUse > 0 ? dto.PointsToUse / 100m : 0m;
        if (totalPointsOffset > OrderAggregate.MaxPointsOffsetAmount)
        {
            totalPointsOffset = OrderAggregate.MaxPointsOffsetAmount;
        }
        var totalItemsAmount = dto.Items.Sum(i => skuInfos[i.SkuId].UnitPrice * i.Quantity);

        OrderAggregate? firstOrder = null;
        var pointsRemaining = totalPointsOffset;
        for (var idx = 0; idx < groups.Count; idx++)
        {
            var group = groups[idx];
            var sellerId = group.SellerId;

            // 构建订单明细
            var orderItems = new List<OrderItem>();
            var skuQuantities = new Dictionary<Guid, int>();
            var itemSubtotals = new List<(Guid SkuId, decimal Subtotal)>();
            decimal groupItemsAmount = 0;
            foreach (var ci in group.Items)
            {
                var info = skuInfos[ci.SkuId];
                var snapshot = ProductSnapshot.Create(
                    info.SkuId, info.SpuId, info.ProductName, info.SkuName, info.MainImage, info.SellerId);
                var orderItem = OrderItem.Create(Guid.NewGuid(), info.SkuId, snapshot, info.UnitPrice, ci.Quantity, ci.SourceCartItemId);
                orderItems.Add(orderItem);
                skuQuantities[info.SkuId] = ci.Quantity;
                itemSubtotals.Add((info.SkuId, orderItem.Subtotal));
                groupItemsAmount += orderItem.Subtotal;
            }

            // 价格防篡改校验
            var skuPrices = itemSubtotals.Select(s => (s.SkuId, skuInfos[s.SkuId].UnitPrice)).ToList();
            await _pricingService.ValidatePricesAsync(skuPrices, ct);

            // 计算优惠并按 SKU 分摊
            var discount = await _promotionAntiCorruption.CalculateDiscountAsync(userId, itemSubtotals, ct);
            var allocations = discount > 0
                ? await _pricingService.CalculateAndAllocateAsync(discount, itemSubtotals, ct)
                : new List<(Guid SkuId, decimal Allocation)>();

            // 计算运费
            var quantity = group.Items.Sum(i => i.Quantity);
            var freight = await _freightCalculator.CalculateAsync(sellerId, dto.Province, quantity, groupItemsAmount, ct);

            // 按比例分摊积分抵现，尾差归到最后一组
            decimal groupPointsOffset;
            if (idx == groups.Count - 1)
            {
                groupPointsOffset = pointsRemaining;
            }
            else
            {
                groupPointsOffset = totalItemsAmount > 0
                    ? Math.Round(totalPointsOffset * (groupItemsAmount / totalItemsAmount), 2, MidpointRounding.ToEven)
                    : 0m;
                pointsRemaining -= groupPointsOffset;
            }

            // 积分抵现上限：商品总额 - 优惠
            var maxOffset = groupItemsAmount - discount;
            if (groupPointsOffset > maxOffset)
            {
                groupPointsOffset = maxOffset;
            }
            if (groupPointsOffset < 0)
            {
                groupPointsOffset = 0m;
            }

            // 预占库存
            var orderId = Guid.NewGuid();
            var reserved = await _stockService.ReserveBatchAsync(orderId, skuQuantities, ct);
            if (!reserved)
            {
                throw new OrderDomainException("库存预占失败，SKU 库存不足", "ORDER_STOCK_RESERVE_FAILED");
            }

            // 冻结积分
            if (dto.PointsToUse > 0 && groupPointsOffset > 0)
            {
                var groupPoints = (int)Math.Round(groupPointsOffset * 100m, MidpointRounding.ToEven);
                if (groupPoints > 0)
                {
                    await _pointsAntiCorruption.FreezeAsync(userId, orderId, groupPoints, ct);
                }
            }

            // 生成订单编号并创建订单聚合
            var orderNo = await _orderNumberGenerator.GenerateAsync(ct);
            var order = OrderAggregate.Create(
                orderId, orderNo, OrderType.Normal, userId, sellerId,
                orderItems, address, freight, groupPointsOffset, DateTime.UtcNow.AddMinutes(30));

            // 应用优惠分摊
            if (discount > 0 && allocations.Count > 0)
            {
                order.ApplyDiscount(discount, allocations);
            }

            await _orderRepository.AddAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            // Schedule timeout cancellation (30 minutes)
            var scheduler = _bus.CreateMessageScheduler();
            await scheduler.ScheduleSend(
                new Uri("queue:order-timeout"),
                order.ExpireAt,
                new OrderTimeoutMessage(orderId),
                ct);

            // 多卖家拆单返回首单 DTO
            firstOrder ??= order;
        }

        return ToDto(firstOrder!);
    }

    /// <inheritdoc />
    public async Task<OrderDto> BuyNowAsync(Guid userId, BuyNowDto dto, CancellationToken ct = default)
    {
        // 立即购买转换为创建订单入参，复用拆单与计价逻辑
        var createDto = new CreateOrderDto
        {
            Items = new List<CheckoutItemDto>
            {
                new() { SkuId = dto.SkuId, Quantity = dto.Quantity }
            },
            PaymentMethod = dto.PaymentMethod,
            PointsToUse = dto.PointsToUse,
            RecipientName = dto.RecipientName,
            RecipientPhone = dto.RecipientPhone,
            Province = dto.Province,
            City = dto.City,
            District = dto.District,
            Detail = dto.Detail
        };
        return await CreateOrderAsync(userId, createDto, ct);
    }

    /// <inheritdoc />
    public async Task<OrderPreviewResultDto> PreviewAsync(Guid userId, CreateOrderDto dto, CancellationToken ct = default)
    {
        // 查询 SKU 信息并构建预览明细
        var details = new List<PreviewItemDetail>();
        var sellerSubtotals = new Dictionary<Guid, List<(Guid SkuId, decimal Subtotal)>>();
        var sellerAmounts = new Dictionary<Guid, decimal>();
        var sellerQuantities = new Dictionary<Guid, int>();
        decimal itemsAmount = 0;
        foreach (var ci in dto.Items)
        {
            var info = await _productAntiCorruption.GetSkuInfoAsync(ci.SkuId, ct)
                ?? throw new OrderDomainException($"SKU {ci.SkuId} 不存在或已下架", "ORDER_SKU_NOT_FOUND", 404);
            var subtotal = info.UnitPrice * ci.Quantity;
            details.Add(new PreviewItemDetail
            {
                SkuId = ci.SkuId,
                ProductName = info.ProductName,
                UnitPrice = info.UnitPrice,
                Quantity = ci.Quantity,
                Subtotal = subtotal
            });
            itemsAmount += subtotal;

            if (!sellerSubtotals.TryGetValue(info.SellerId, out var subs))
            {
                subs = new List<(Guid, decimal)>();
                sellerSubtotals[info.SellerId] = subs;
                sellerAmounts[info.SellerId] = 0;
                sellerQuantities[info.SellerId] = 0;
            }
            subs.Add((ci.SkuId, subtotal));
            sellerAmounts[info.SellerId] += subtotal;
            sellerQuantities[info.SellerId] += ci.Quantity;
        }

        // 价格防篡改校验
        var skuPrices = details.Select(d => (d.SkuId, d.UnitPrice)).ToList();
        await _pricingService.ValidatePricesAsync(skuPrices, ct);

        // 按卖家分组计算优惠与运费
        decimal discountAmount = 0;
        decimal freightAmount = 0;
        foreach (var sellerId in sellerSubtotals.Keys)
        {
            var subtotals = sellerSubtotals[sellerId];
            discountAmount += await _promotionAntiCorruption.CalculateDiscountAsync(userId, subtotals, ct);
            freightAmount += await _freightCalculator.CalculateAsync(
                sellerId, dto.Province, sellerQuantities[sellerId], sellerAmounts[sellerId], ct);
        }

        // 积分抵现，上限为商品总额 - 优惠
        var pointsOffset = dto.PointsToUse > 0 ? dto.PointsToUse / 100m : 0m;
        if (pointsOffset > OrderAggregate.MaxPointsOffsetAmount)
        {
            pointsOffset = OrderAggregate.MaxPointsOffsetAmount;
        }
        var maxOffset = itemsAmount - discountAmount;
        if (pointsOffset > maxOffset)
        {
            pointsOffset = maxOffset;
        }
        if (pointsOffset < 0)
        {
            pointsOffset = 0m;
        }

        var totalAmount = itemsAmount - discountAmount - pointsOffset + freightAmount;

        return new OrderPreviewResultDto
        {
            ItemsAmount = itemsAmount,
            DiscountAmount = discountAmount,
            PointsOffsetAmount = pointsOffset,
            FreightAmount = freightAmount,
            TotalAmount = totalAmount,
            Items = details
        };
    }

    /// <inheritdoc />
    public async Task PayAsync(Guid orderId, Guid userId, PayOrderDto dto, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        if (order.UserId != userId)
        {
            throw new OrderDomainException("无权操作此订单", "ORDER_FORBIDDEN", 403);
        }
        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException($"订单状态 {order.Status} 不可发起支付", "ORDER_PAY_STATUS_INVALID");
        }

        // 发布支付请求集成事件，由支付域创建支付单并拉起第三方支付
        var channel = dto.PaymentMethod.ToString();
        var evt = new PaymentRequestedIntegrationEvent(orderId, userId, order.TotalAmount, "CNY", channel, DateTime.UtcNow);
        await _eventBus.PublishAsync(evt, ct);
    }

    /// <inheritdoc />
    public async Task ShipAsync(Guid orderId, Guid operatorId, ShipOrderDto dto, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        order.Ship(dto.LogisticsNo, dto.LogisticsCompanyCode, DateTime.UtcNow, operatorId);
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ConfirmReceiptAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        if (order.UserId != userId)
        {
            throw new OrderDomainException("无权操作此订单", "ORDER_FORBIDDEN", 403);
        }
        order.ConfirmReceipt();
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 调度售后窗口结束延迟消息（7 天后）
        var scheduler = _bus.CreateMessageScheduler();
        await scheduler.ScheduleSend(
            new Uri("queue:order-after-sales-window"),
            order.AfterSalesWindowEndsAt!.Value,
            new AfterSalesWindowMessage(orderId),
            ct);
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid orderId, Guid userId, CancelOrderDto dto, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        if (order.UserId != userId)
        {
            throw new OrderDomainException("无权操作此订单", "ORDER_FORBIDDEN", 403);
        }
        order.Cancel(dto.Reason, "Buyer");

        // 释放预占库存、冻结积分与优惠券
        var skuQuantities = BuildSkuQuantities(order);
        await _stockService.ReleaseBatchAsync(orderId, skuQuantities, ct);
        await _pointsAntiCorruption.ReleaseAsync(orderId, ct);
        await _promotionAntiCorruption.ReleaseCouponsAsync(orderId, ct);

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ForceCancelAsync(Guid orderId, ForceCancelOrderDto dto, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        order.ForceCancel(dto.Reason, "Operator");

        // 释放预占库存、冻结积分与优惠券
        var skuQuantities = BuildSkuQuantities(order);
        await _stockService.ReleaseBatchAsync(orderId, skuQuantities, ct);
        await _pointsAntiCorruption.ReleaseAsync(orderId, ct);
        await _promotionAntiCorruption.ReleaseCouponsAsync(orderId, ct);

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OrderDto> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        return ToDto(order);
    }

    /// <inheritdoc />
    public async Task<OrderListResultDto> QueryAsync(Guid? userId, Guid? sellerId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var orders = await _orderRepository.QueryAsync(userId, sellerId, status, null, null, page, pageSize, ct);
        var total = await _orderRepository.CountAsync(userId, sellerId, status, null, null, ct);
        var items = orders.Select(ToDto).ToList();
        return new OrderListResultDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<LogisticsTrackingDto> GetLogisticsTraceAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);

        if (string.IsNullOrWhiteSpace(order.LogisticsNo))
        {
            return new LogisticsTrackingDto { LogisticsNo = string.Empty, Nodes = new List<LogisticsTrackingNode>() };
        }

        if (string.IsNullOrWhiteSpace(order.LogisticsCompanyCode))
        {
            return new LogisticsTrackingDto
            {
                LogisticsNo = order.LogisticsNo,
                CompanyCode = string.Empty,
                Nodes = new List<LogisticsTrackingNode>(),
                HasWarning = true
            };
        }

        // 校验物流公司是否支持轨迹查询
        // 通过查询所有已启用的物流公司来匹配 Code
        var companies = await _logisticsCompanyRepository.ListAsync(1, 100, ct);
        var company = companies.FirstOrDefault(c =>
            string.Equals(c.Code, order.LogisticsCompanyCode, StringComparison.OrdinalIgnoreCase) &&
            c.Status == LogisticsCompanyStatus.Enabled);

        if (company is null || !company.SupportTracking)
        {
            return new LogisticsTrackingDto
            {
                LogisticsNo = order.LogisticsNo,
                CompanyCode = order.LogisticsCompanyCode,
                Nodes = new List<LogisticsTrackingNode>(),
                HasWarning = true
            };
        }

        // 调用领域服务查询物流轨迹
        var traceResult = await _logisticsTrackingService.QueryTraceAsync(
            order.LogisticsNo, order.LogisticsCompanyCode, ct);

        return new LogisticsTrackingDto
        {
            LogisticsNo = traceResult.LogisticsNo,
            CompanyCode = traceResult.CompanyCode,
            Nodes = traceResult.Nodes.Select(n => new LogisticsTrackingNode
            {
                Description = n.Description,
                OccurredAt = n.OccurredAt,
                Location = n.Location
            }).ToList(),
            IsFromCache = traceResult.IsFromCache,
            HasWarning = false
        };
    }

    /// <summary>
    /// 按标识加载订单，不存在抛领域异常。
    /// </summary>
    private async Task<OrderAggregate> RequireOrderAsync(Guid orderId, CancellationToken ct)
        => await _orderRepository.GetByIdAsync(orderId, ct)
           ?? throw new OrderDomainException($"订单 {orderId} 不存在", "ORDER_NOT_FOUND", 404);

    /// <summary>
    /// 由订单明细构建 SKU 与数量映射，供库存释放使用。
    /// </summary>
    private static Dictionary<Guid, int> BuildSkuQuantities(OrderAggregate order)
    {
        var dict = new Dictionary<Guid, int>();
        foreach (var item in order.Items)
        {
            dict[item.SkuId] = dict.TryGetValue(item.SkuId, out var q) ? q + item.Quantity : item.Quantity;
        }
        return dict;
    }

    private static OrderDto ToDto(OrderAggregate order)
        => new()
        {
            Id = order.Id,
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
            PaymentMethod = order.PaymentMethod,
            ExpireAt = order.ExpireAt,
            PaidAt = order.PaidAt,
            ShippedAt = order.ShippedAt,
            LogisticsNo = order.LogisticsNo,
            LogisticsCompanyCode = order.LogisticsCompanyCode,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancelReason = order.CancelReason,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(ToItemDto).ToList()
        };

    private static OrderItemDto ToItemDto(OrderItem item)
        => new()
        {
            SkuId = item.SkuId,
            ProductName = item.ProductSnapshot.ProductName,
            SkuName = item.ProductSnapshot.SkuName,
            MainImage = item.ProductSnapshot.MainImage,
            UnitPrice = item.UnitPrice,
            Quantity = item.Quantity,
            DiscountAllocation = item.DiscountAllocation,
            Subtotal = item.Subtotal
        };
}

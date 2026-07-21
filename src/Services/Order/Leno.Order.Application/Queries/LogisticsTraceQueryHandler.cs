using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 物流轨迹查询处理器。
/// 加载订单聚合获取物流单号与物流公司编码，校验物流公司支持轨迹查询后委托
/// <see cref="ILogisticsTrackingService"/>（防腐层服务）调用第三方物流 API 获取实时轨迹。
/// 双发期 2 周内与 <c>OrderAppService.GetLogisticsTraceAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class LogisticsTraceQueryHandler : IQueryHandler<LogisticsTraceQuery, LogisticsTraceResult?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogisticsCompanyRepository _logisticsCompanyRepository;
    private readonly ILogisticsTrackingService _logisticsTrackingService;

    public LogisticsTraceQueryHandler(
        IOrderRepository orderRepository,
        ILogisticsCompanyRepository logisticsCompanyRepository,
        ILogisticsTrackingService logisticsTrackingService)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(logisticsCompanyRepository);
        ArgumentNullException.ThrowIfNull(logisticsTrackingService);
        _orderRepository = orderRepository;
        _logisticsCompanyRepository = logisticsCompanyRepository;
        _logisticsTrackingService = logisticsTrackingService;
    }

    /// <inheritdoc />
    public async Task<LogisticsTraceResult?> HandleAsync(LogisticsTraceQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await _orderRepository.GetByIdAsync(query.OrderId, ct);
        if (order is null)
        {
            return null;
        }

        // 未填写物流单号：返回空轨迹结果
        if (string.IsNullOrWhiteSpace(order.LogisticsNo))
        {
            return new LogisticsTraceResult
            {
                OrderId = order.Id,
                TrackingNo = null,
                LogisticsCompany = null,
                Nodes = Array.Empty<LogisticsTraceNode>()
            };
        }

        // 物流公司编码缺失：返回物流单号但空轨迹
        if (string.IsNullOrWhiteSpace(order.LogisticsCompanyCode))
        {
            return new LogisticsTraceResult
            {
                OrderId = order.Id,
                TrackingNo = order.LogisticsNo,
                LogisticsCompany = null,
                Nodes = Array.Empty<LogisticsTraceNode>()
            };
        }

        // 校验物流公司是否支持轨迹查询（按 Code 精确查询，利用唯一索引）
        var company = await _logisticsCompanyRepository.GetByCodeAsync(order.LogisticsCompanyCode, ct);
        var companyEnabled = company is not null &&
            company.Status == LogisticsCompanyStatus.Enabled &&
            company.SupportTracking;

        if (!companyEnabled)
        {
            return new LogisticsTraceResult
            {
                OrderId = order.Id,
                TrackingNo = order.LogisticsNo,
                LogisticsCompany = order.LogisticsCompanyCode,
                Nodes = Array.Empty<LogisticsTraceNode>()
            };
        }

        // 调用领域服务查询物流轨迹（第三方 API 失败时由防腐层降级返回缓存或空轨迹，不抛异常）
        var traceResult = await _logisticsTrackingService.QueryTraceAsync(
            order.LogisticsNo, order.LogisticsCompanyCode, ct);

        return new LogisticsTraceResult
        {
            OrderId = order.Id,
            TrackingNo = traceResult.LogisticsNo,
            LogisticsCompany = traceResult.CompanyCode,
            Nodes = traceResult.Nodes.Select(n => new LogisticsTraceNode
            {
                Time = n.OccurredAt,
                Description = n.Description,
                Location = string.IsNullOrEmpty(n.Location) ? null : n.Location
            }).ToList()
        };
    }
}

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Leno.Infrastructure.ReadModel;
using Leno.Order.Application.Queries;
using Leno.SharedContracts.Responses;

namespace Leno.Order.Infrastructure.ReadModels;

/// <summary>
/// 订单读模型访问器实现，基于 <see cref="IEsReadModelRepository{T}"/> 查询 ES 读模型。
/// 实现 Application 层定义的 <see cref="IOrderReadModelAccessor"/> 端口，保持分层洁癖。
/// 索引名 <c>orders</c> 与 <see cref="OrderReadModelSyncConsumer"/> 同步侧保持一致。
/// </summary>
public sealed class OrderReadModelAccessor : IOrderReadModelAccessor
{
    /// <summary>订单读模型索引名，与 <c>OrderReadModelSyncConsumer</c> 一致。</summary>
    public const string OrderIndexName = "orders";

    private readonly IEsReadModelRepository<OrderReadModel> _repository;

    public OrderReadModelAccessor(IEsReadModelRepository<OrderReadModel> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<OrderDetailResult?> GetDetailAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            return null;
        }

        var model = await _repository.GetByIdAsync(orderId.ToString(), OrderIndexName, ct);
        return model is null ? null : ToDetailResult(model);
    }

    /// <inheritdoc />
    public async Task<PageResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // PageRequest 基类已归一化 Page/PageSize，直接用 Skip
        var from = query.Skip;
        var safePageSize = query.PageSize;

        var (items, total) = await _repository.SearchAsync(
            OrderIndexName,
            _ => BuildQuery(query),
            from,
            safePageSize,
            ct);

        var summaries = items.Select(ToSummaryDto).ToList();
        return new PageResult<OrderSummaryDto>(summaries, (int)total, query.Page, safePageSize);
    }

    private static Query BuildQuery(OrderListQuery query)
    {
        var filters = new List<Query>();

        if (query.UserId.HasValue)
        {
            filters.Add(new TermQuery(Infer.Field<OrderReadModel>(f => f.UserId))
            {
                Value = query.UserId.Value.ToString()
            });
        }

        if (query.SellerId.HasValue)
        {
            filters.Add(new TermQuery(Infer.Field<OrderReadModel>(f => f.SellerId))
            {
                Value = query.SellerId.Value.ToString()
            });
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(new TermQuery(Infer.Field<OrderReadModel>(f => f.Status))
            {
                Value = query.Status
            });
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNo))
        {
            // 订单号模糊匹配：使用 MatchQuery 对 OrderNo 文本字段做分词级模糊匹配。
            // 放在 Filter 上下文不打分但保留匹配语义，与现有 Status/UserId 等过滤条件组合。
            filters.Add(new MatchQuery(Infer.Field<OrderReadModel>(f => f.OrderNo))
            {
                Query = query.OrderNo
            });
        }

        if (query.StartDate.HasValue || query.EndDate.HasValue)
        {
            var range = new DateRangeQuery(Infer.Field<OrderReadModel>(f => f.CreatedAt));
            if (query.StartDate.HasValue)
            {
                range.Gte = query.StartDate.Value;
            }

            if (query.EndDate.HasValue)
            {
                range.Lte = query.EndDate.Value;
            }

            filters.Add(range);
        }

        if (filters.Count == 0)
        {
            return new MatchAllQuery();
        }

        return new BoolQuery { Filter = filters };
    }

    private static OrderDetailResult ToDetailResult(OrderReadModel model)
        => new()
        {
            OrderId = Guid.TryParse(model.OrderId, out var oid) ? oid : Guid.Empty,
            OrderNo = model.OrderNo,
            UserId = Guid.TryParse(model.UserId, out var uid) ? uid : Guid.Empty,
            SellerId = Guid.TryParse(model.SellerId, out var sid) ? sid : null,
            OrderType = model.OrderType,
            ItemsAmount = model.ItemsAmount,
            DiscountAmount = model.DiscountAmount,
            PointsOffsetAmount = model.PointsOffsetAmount,
            FreightAmount = model.FreightAmount,
            TotalAmount = model.TotalAmount,
            Currency = "CNY",
            Status = model.Status,
            CreatedAt = model.CreatedAt,
            PaidAt = model.PaidAt,
            ShippedAt = model.ShippedAt,
            CompletedAt = model.CompletedAt,
            CancelledAt = model.CancelledAt,
            Items = model.Items.Select(ToItemDto).ToList()
        };

    private static OrderSummaryDto ToSummaryDto(OrderReadModel model)
        => new()
        {
            OrderId = Guid.TryParse(model.OrderId, out var oid) ? oid : Guid.Empty,
            OrderNo = model.OrderNo,
            UserId = Guid.TryParse(model.UserId, out var uid) ? uid : Guid.Empty,
            SellerId = Guid.TryParse(model.SellerId, out var sid) ? sid : null,
            TotalAmount = model.TotalAmount,
            Currency = "CNY",
            Status = model.Status,
            CreatedAt = model.CreatedAt,
            PaidAt = model.PaidAt,
            ShippedAt = model.ShippedAt
        };

    private static OrderItemDto ToItemDto(OrderReadModel.OrderItemReadModel item)
        => new()
        {
            SkuId = Guid.TryParse(item.SkuId, out var skuId) ? skuId : Guid.Empty,
            ProductName = item.ProductName,
            SkuName = item.SkuName,
            UnitPrice = item.UnitPrice,
            Quantity = item.Quantity,
            Subtotal = item.Subtotal
        };
}

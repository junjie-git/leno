using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SellerShop.Application.Services;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.SellerShop.Infrastructure.Services.Grpc;

/// <summary>
/// 订单域 gRPC 防腐层客户端（卖家店铺域视角）。
/// 实现 <see cref="IOrderAntiCorruptionService"/>，用于卖家资源归属校验时反查订单归属卖家。
/// 通过 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 统一异常处理与埋点；
/// 防腐层失败时由本类捕获 <see cref="AntiCorruptionException"/> 返回 null（fail-closed），
/// 避免 Order 域故障阻塞卖家归属校验流程。
/// </summary>
public sealed class GrpcOrderAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IOrderAntiCorruptionService
{
    private const string TargetBc = "Order";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly OrderInternalService.OrderInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcOrderAntiCorruptionClient> _logger;

    protected override string ServiceName => "order";

    public GrpcOrderAntiCorruptionClient(
        OrderInternalService.OrderInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcOrderAntiCorruptionClient> logger,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_order_seller", async token =>
            {
                var request = new GetOrderSellerIdRequest
                {
                    OrderIdStr = orderId.ToString()
                };
                var metadata = BuildMetadata();
                var response = await _client.GetOrderSellerIdAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return Guid.TryParse(response.SellerIdStr, out var sellerId) ? sellerId : (Guid?)null;
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-closed：跨域调用失败时返回 null，由 SellerInternalQueryService 判 false
            // 基类 ExecuteAsync 已记录 "grpc" path 的失败，此处补充记录 "fail-closed" path 的降级触发，
            // 供告警规则按 path=fail-closed 统计降级频率（ACL 失败率 > 5% 触发告警）
            AntiCorruptionMetrics.RecordFailure(ServiceName, "get_order_seller", "fail-closed");
            _logger.LogWarning(ex, "订单域 GetOrderSellerId 调用失败，fail-closed 返回 null OrderId={OrderId}", orderId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetSalesSummaryAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_sales_summary", async token =>
            {
                var request = new ExportDataRequest
                {
                    ShopIdStr = shopId.ToString(),
                    StartDate = startDate.ToString("O"),
                    EndDate = endDate.ToString("O")
                };
                var metadata = BuildMetadata();
                var response = await _client.GetSalesSummaryForExportAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return MapExportResponse(response);
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-soft：跨域调用失败时返回空表头与空行，导出文件为空文件，不阻塞导出流程
            AntiCorruptionMetrics.RecordFailure(ServiceName, "get_sales_summary", "fail-soft");
            _logger.LogWarning(ex, "订单域 GetSalesSummary 调用失败，fail-soft 返回空结果 ShopId={ShopId}", shopId);
            return (new List<string>(), new List<IReadOnlyDictionary<string, object?>>());
        }
    }

    /// <inheritdoc />
    public async Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetOrderDetailForExportAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_order_detail_for_export", async token =>
            {
                var request = new ExportDataRequest
                {
                    ShopIdStr = shopId.ToString(),
                    StartDate = startDate.ToString("O"),
                    EndDate = endDate.ToString("O")
                };
                var metadata = BuildMetadata();
                var response = await _client.GetOrderDetailForExportAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return MapExportResponse(response);
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-soft：跨域调用失败时返回空表头与空行，导出文件为空文件，不阻塞导出流程
            AntiCorruptionMetrics.RecordFailure(ServiceName, "get_order_detail_for_export", "fail-soft");
            _logger.LogWarning(ex, "订单域 GetOrderDetailForExport 调用失败，fail-soft 返回空结果 ShopId={ShopId}", shopId);
            return (new List<string>(), new List<IReadOnlyDictionary<string, object?>>());
        }
    }

    /// <summary>
    /// 将 gRPC <see cref="ExportDataResponse"/> 映射为应用层 (Headers, Rows) 元组。
    /// 行值统一以 string 形式承载（proto map&lt;string,string&gt;），由 ExportFileGenerator 进一步格式化。
    /// </summary>
    private static (List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows) MapExportResponse(ExportDataResponse response)
    {
        var headers = response.Headers.ToList();
        var rows = response.Rows.Select(row =>
        {
            var dict = new Dictionary<string, object?>(row.Values.Count);
            foreach (var kv in row.Values)
            {
                dict[kv.Key] = kv.Value;
            }
            return (IReadOnlyDictionary<string, object?>)dict;
        }).ToList();
        return (headers, rows);
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
}

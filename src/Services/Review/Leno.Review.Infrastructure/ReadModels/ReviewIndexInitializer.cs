using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Leno.Infrastructure.ReadModel;
using Leno.Review.Infrastructure.ReadModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Review.Infrastructure.ReadModels;

/// <summary>
/// 评价域 ES 索引初始化器（评价 BC 独立维护，reviews_v2 索引）。
/// 应用启动时确保 reviews_v2 索引存在，不存在则创建并配置字段映射。
/// 索引名加 _v2 后缀以与旧 ReviewAfterSales BC 的 reviews 索引区分，避免双写期数据混淆。
/// 实现 IHostedService 通过 DI 自动启动，启动失败仅记录日志不阻止应用启动（ES 故障时降级到 DB 查询）。
/// </summary>
public sealed class ReviewIndexInitializer : IHostedService
{
    public const string IndexName = "reviews_v2";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ReviewIndexInitializer> _logger;

    public ReviewIndexInitializer(ElasticsearchClient client, ILogger<ReviewIndexInitializer> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);
            if (existsResponse.IsValidResponse && existsResponse.Exists)
            {
                _logger.LogInformation("评价 ES 索引已存在 Index={Index}", IndexName);
                return;
            }

            var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
                .Mappings(m => m
                    .Properties<ReviewReadModel>(p => p
                        .Keyword(k => k.ReviewId)
                        .Keyword(k => k.OrderId)
                        .Keyword(k => k.SpuId)
                        .Keyword(k => k.SkuId)
                        .Keyword(k => k.UserId)
                        .IntegerNumber(k => k.Rating)
                        .Text(k => k.Content)
                        .Keyword(k => k.Status)
                        .Date(d => d.SubmittedAt)
                        .Date(d => d.SellerReplyAt))), cancellationToken);

            if (!createResponse.IsValidResponse)
            {
                _logger.LogError("评价 ES 索引创建失败 Index={Index} Error={Error}",
                    IndexName, createResponse.DebugInformation);
                return;
            }

            _logger.LogInformation("评价 ES 索引已创建 Index={Index}", IndexName);
        }
        catch (Exception ex)
        {
            // ES 故障不阻止应用启动，下游查询降级到 DB
            _logger.LogError(ex, "评价 ES 索引初始化异常 Index={Index}，降级到 DB 查询", IndexName);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

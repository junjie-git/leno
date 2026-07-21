using System.Net;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// ElasticsearchRebuildTrigger 单元测试，验证 M-08 修复：
/// - GetProgressAsync 通过 description 中的 {taskId:N} 标记精确匹配本任务的 reindex，不误返回其他任务进度
/// - 任务已完成（ES 任务列表中无匹配项）时返回 100 而非 0
/// - StartAsync 返回 ES task 节点的 EsTaskId 供编排器回写
/// </summary>
public sealed class ElasticsearchRebuildTriggerTaskIdMatchingTests
{
    private static readonly Guid TargetTaskId = Guid.Parse("aabbccdd-1122-3344-5566-77889900aabb");
    private static readonly string TaskIdToken = TargetTaskId.ToString("N");

    [Fact]
    public async Task GetProgressAsync_Should_Return_Matching_Task_Progress_When_Description_Contains_TaskIdToken()
    {
        var esResponse = BuildTasksResponse(new[]
        {
            BuildReindexTask("otherNode:1", "reindex from [product_products] to [product_products_reindex_ffffffffeeeeddddccccbbbbaaaa]", 500, 1000),
            BuildReindexTask("matchNode:2", $"reindex from [product_products] to [product_products_reindex_{TaskIdToken}]", 750, 1000)
        });

        var handler = new StubHttpMessageHandler(esResponse, HttpStatusCode.OK);
        var trigger = CreateTrigger(handler);

        var progress = await trigger.GetProgressAsync(TargetTaskId, CancellationToken.None);

        progress.Should().Be(75);
    }

    [Fact]
    public async Task GetProgressAsync_Should_Return_100_When_No_Matching_Task_Found_Completed()
    {
        var esResponse = BuildTasksResponse(new[]
        {
            BuildReindexTask("otherNode:1", "reindex from [product_products] to [product_products_reindex_ffffffffeeeeddddccccbbbbaaaa]", 500, 1000)
        });

        var handler = new StubHttpMessageHandler(esResponse, HttpStatusCode.OK);
        var trigger = CreateTrigger(handler);

        var progress = await trigger.GetProgressAsync(TargetTaskId, CancellationToken.None);

        progress.Should().Be(100, "任务已完成（ES 任务列表中无匹配项）时应返回 100 而非 0");
    }

    [Fact]
    public async Task GetProgressAsync_Should_Return_100_When_No_Tasks_At_All()
    {
        var esResponse = BuildTasksResponse(Array.Empty<(string, string, long, long)>());

        var handler = new StubHttpMessageHandler(esResponse, HttpStatusCode.OK);
        var trigger = CreateTrigger(handler);

        var progress = await trigger.GetProgressAsync(TargetTaskId, CancellationToken.None);

        progress.Should().Be(100, "无任何 reindex 任务时应视为已完成返回 100");
    }

    [Fact]
    public async Task GetProgressAsync_Should_Not_Return_Other_Task_Progress()
    {
        var otherTaskId = Guid.NewGuid();
        var esResponse = BuildTasksResponse(new[]
        {
            BuildReindexTask("otherNode:1", $"reindex from [order_orders] to [order_orders_reindex_{otherTaskId.ToString("N")}]", 999, 1000)
        });

        var handler = new StubHttpMessageHandler(esResponse, HttpStatusCode.OK);
        var trigger = CreateTrigger(handler);

        var progress = await trigger.GetProgressAsync(TargetTaskId, CancellationToken.None);

        progress.Should().Be(100, "不应返回其他任务的 99% 进度");
    }

    [Fact]
    public async Task GetProgressAsync_Should_Return_0_When_Http_Request_Fails()
    {
        var handler = new StubHttpMessageHandler("{}", HttpStatusCode.InternalServerError);
        var trigger = CreateTrigger(handler);

        var progress = await trigger.GetProgressAsync(TargetTaskId, CancellationToken.None);

        progress.Should().Be(0, "HTTP 请求失败时返回 0");
    }

    [Fact]
    public async Task StartAsync_Should_Return_EsTaskId_From_Response()
    {
        var esTaskId = "matchNode:42";
        var esResponse = $"{{\"task\":\"{esTaskId}\"}}";

        // StartAsync 会先创建索引（PUT），再提交 reindex（POST），返回 task 节点
        var handler = new StubHttpMessageHandlerMultiStep(
            ("PUT", "{}", HttpStatusCode.OK),
            ("POST", esResponse, HttpStatusCode.OK));

        var trigger = CreateTrigger(handler);

        var result = await trigger.StartAsync(TargetTaskId, "Product", "products", CancellationToken.None);

        result.Should().Be(esTaskId);
    }

    [Fact]
    public async Task StartAsync_Should_Return_Null_When_No_Task_Node_In_Response()
    {
        var esResponse = "{\"acknowledged\":true}";

        var handler = new StubHttpMessageHandlerMultiStep(
            ("PUT", "{}", HttpStatusCode.OK),
            ("POST", esResponse, HttpStatusCode.OK));

        var trigger = CreateTrigger(handler);

        var result = await trigger.StartAsync(TargetTaskId, "Product", "products", CancellationToken.None);

        result.Should().BeNull();
    }

    private static ElasticsearchRebuildTrigger CreateTrigger(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        return new ElasticsearchRebuildTrigger(
            httpClient,
            configuration,
            NullLogger<ElasticsearchRebuildTrigger>.Instance);
    }

    private static (string Id, string Description, long Created, long Total) BuildReindexTask(
        string taskId, string description, long created, long total)
        => (taskId, description, created, total);

    private static string BuildTasksResponse(IEnumerable<(string Id, string Description, long Created, long Total)> tasks)
    {
        var taskEntries = tasks.Select(t =>
            $@"""{t.Id}"":{{
                ""description"":""{t.Description}"",
                ""status"":{{
                    ""created"":{t.Created},
                    ""total"":{t.Total}
                }}
            }}");

        var tasksJson = string.Join(",", taskEntries);
        return $@"{{""nodes"":{{""node1"":{{""tasks"":{{{tasksJson}}}}}}}}}";
    }

    /// <summary>
    /// 单响应 HttpMessageHandler 桩，对所有请求返回固定内容。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// 多步 HttpMessageHandler 桩，按请求方法顺序返回不同响应。
    /// </summary>
    private sealed class StubHttpMessageHandlerMultiStep : HttpMessageHandler
    {
        private readonly Queue<(string Method, string Content, HttpStatusCode StatusCode)> _responses;

        public StubHttpMessageHandlerMultiStep(params (string Method, string Content, HttpStatusCode StatusCode)[] responses)
        {
            _responses = new Queue<(string, string, HttpStatusCode)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                });
            }

            var (method, content, statusCode) = _responses.Dequeue();
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

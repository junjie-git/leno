using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;

namespace Leno.SystemAdmin.Infrastructure.Tests.Jobs;

/// <summary>
/// 验证 L-04 修复：<see cref="DlqCleanupJob"/> 定期清理 DLQ 队列，避免消息无限堆积。
/// 覆盖：本地副本检查、队列清理、404 处理、HTTP 异常处理、配置读取。
/// </summary>
public sealed class DlqCleanupJobTests
{
    /// <summary>
    /// 场景：本地无已入库死信消息副本（Count=0）。
    /// 验证：跳过清理，不调用 HTTP API，返回 0。
    /// </summary>
    [Fact]
    public async Task ExecuteCleanupAsync_When_No_Persisted_Messages_Should_Skip_Cleanup()
    {
        var mockRepo = new Mock<IDeadLetterMessageRepository>();
        mockRepo.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

        var configuration = CreateConfiguration();
        var result = await DlqCleanupJob.ExecuteCleanupAsync(
            mockRepo.Object, configuration, NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.Equal(0, result);
        mockRepo.Verify(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 场景：本地有已入库死信消息副本（Count>0），PurgeQueuesAsync 使用 stub HttpClient 返回 204。
    /// 验证：成功清理队列，返回 1。
    /// </summary>
    [Fact]
    public async Task PurgeQueuesAsync_When_Delete_Returns_204_Should_Purge_All_Queues()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.NoContent);
        var httpClient = new HttpClient(handler);
        var queueNames = new List<string> { "dead-letter-queue" };

        var result = await DlqCleanupJob.PurgeQueuesAsync(
            httpClient, "http://localhost:15672", "%2F", queueNames,
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// 场景：多个队列，部分返回 204，部分返回 404。
    /// 验证：仅成功清理的队列计入返回值。
    /// </summary>
    [Fact]
    public async Task PurgeQueuesAsync_With_Mixed_Results_Should_Count_Only_Successful_Purges()
    {
        var handler = new SequentialStubHttpMessageHandler(new[]
        {
            (string.Empty, HttpStatusCode.NoContent),
            ("queue not found", HttpStatusCode.NotFound),
            (string.Empty, HttpStatusCode.OK)
        });
        var httpClient = new HttpClient(handler);
        var queueNames = new List<string> { "queue1.dlq", "queue2.dlq", "queue3.dlq" };

        var result = await DlqCleanupJob.PurgeQueuesAsync(
            httpClient, "http://localhost:15672", "%2F", queueNames,
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.Equal(2, result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync 调用成功（204 No Content）。
    /// 验证：返回 true。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_When_Delete_Returns_204_Should_Return_True()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.NoContent);
        var httpClient = new HttpClient(handler);

        var result = await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://localhost:15672", "%2F", "dead-letter-queue",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.True(result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync 调用成功（200 OK）。
    /// 验证：返回 true。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_When_Delete_Returns_200_Should_Return_True()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);

        var result = await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://localhost:15672", "%2F", "dead-letter-queue",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.True(result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync 返回 404（队列不存在）。
    /// 验证：返回 false，不抛异常。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_When_Delete_Returns_404_Should_Return_False()
    {
        var handler = new StubHttpMessageHandler("queue not found", HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handler);

        var result = await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://localhost:15672", "%2F", "nonexistent-queue",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync 返回 500（服务器错误）。
    /// 验证：返回 false，不抛异常。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_When_Delete_Returns_500_Should_Return_False()
    {
        var handler = new StubHttpMessageHandler("internal server error", HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler);

        var result = await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://localhost:15672", "%2F", "dead-letter-queue",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync HTTP 请求抛出异常（如连接失败）。
    /// 验证：返回 false，不抛异常，异常被捕获并记录。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_When_Http_Throws_Exception_Should_Return_False()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler);

        var result = await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://localhost:15672", "%2F", "dead-letter-queue",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// 场景：PurgeDlqQueueAsync 验证 URL 拼接正确，vhost 和 queueName 被正确转义。
    /// </summary>
    [Fact]
    public async Task PurgeDlqQueueAsync_Should_Construct_Correct_Url_With_Escaped_VHost_And_Queue()
    {
        var handler = new CapturingHttpMessageHandler(string.Empty, HttpStatusCode.NoContent);
        var httpClient = new HttpClient(handler);

        await DlqCleanupJob.PurgeDlqQueueAsync(
            httpClient, "http://rabbitmq:15672", "%2F", "order-service.dlq",
            NullLogger<DlqCleanupJob>.Instance, CancellationToken.None);

        Assert.NotNull(handler.LastRequestUrl);
        Assert.Equal("http://rabbitmq:15672/api/queues/%2F/order-service.dlq/contents", handler.LastRequestUrl);
        Assert.Equal(HttpMethod.Delete, handler.LastRequestMethod);
    }

    /// <summary>
    /// 场景：GetQueueNames 未配置，应返回默认 dead-letter-queue。
    /// </summary>
    [Fact]
    public void GetQueueNames_With_No_Config_Should_Return_Default_Queue_Name()
    {
        var configuration = new ConfigurationBuilder().Build();
        var queueNames = DlqCleanupJob.GetQueueNames(configuration);

        Assert.Single(queueNames);
        Assert.Equal("dead-letter-queue", queueNames[0]);
    }

    /// <summary>
    /// 场景：GetQueueNames 配置单个队列名。
    /// </summary>
    [Fact]
    public void GetQueueNames_With_Single_Queue_Should_Return_Single_Queue()
    {
        var configuration = CreateConfiguration(queueNames: "order-service.dlq");
        var queueNames = DlqCleanupJob.GetQueueNames(configuration);

        Assert.Single(queueNames);
        Assert.Equal("order-service.dlq", queueNames[0]);
    }

    /// <summary>
    /// 场景：GetQueueNames 配置多个队列名（逗号分隔）。
    /// </summary>
    [Fact]
    public void GetQueueNames_With_Multiple_Queues_Should_Return_All_Queues()
    {
        var configuration = CreateConfiguration(queueNames: "order-service.dlq, payment-service.dlq, dead-letter-queue");
        var queueNames = DlqCleanupJob.GetQueueNames(configuration);

        Assert.Equal(3, queueNames.Count);
        Assert.Equal("order-service.dlq", queueNames[0]);
        Assert.Equal("payment-service.dlq", queueNames[1]);
        Assert.Equal("dead-letter-queue", queueNames[2]);
    }

    /// <summary>
    /// 场景：GetQueueNames 配置空字符串，应返回默认。
    /// </summary>
    [Fact]
    public void GetQueueNames_With_Empty_String_Should_Return_Default()
    {
        var configuration = CreateConfiguration(queueNames: string.Empty);
        var queueNames = DlqCleanupJob.GetQueueNames(configuration);

        Assert.Single(queueNames);
        Assert.Equal("dead-letter-queue", queueNames[0]);
    }

    /// <summary>
    /// 场景：GetQueueNames 配置仅含空白的逗号分隔，应返回默认。
    /// </summary>
    [Fact]
    public void GetQueueNames_With_Only_Whitespace_And_Commas_Should_Return_Default()
    {
        var configuration = CreateConfiguration(queueNames: "  ,  ,  ");
        var queueNames = DlqCleanupJob.GetQueueNames(configuration);

        Assert.Single(queueNames);
        Assert.Equal("dead-letter-queue", queueNames[0]);
    }

    /// <summary>
    /// 场景：GetManagementApiBaseUrl 未配置，应返回默认 http://localhost:15672。
    /// </summary>
    [Fact]
    public void GetManagementApiBaseUrl_With_No_Config_Should_Return_Default()
    {
        var configuration = new ConfigurationBuilder().Build();
        var baseUrl = DlqCleanupJob.GetManagementApiBaseUrl(configuration);

        Assert.Equal("http://localhost:15672", baseUrl);
    }

    /// <summary>
    /// 场景：GetManagementApiBaseUrl 配置带尾部斜杠，应被去除。
    /// </summary>
    [Fact]
    public void GetManagementApiBaseUrl_With_Trailing_Slash_Should_Trim()
    {
        var configuration = CreateConfiguration(host: "http://rabbitmq:15672/");
        var baseUrl = DlqCleanupJob.GetManagementApiBaseUrl(configuration);

        Assert.Equal("http://rabbitmq:15672", baseUrl);
    }

    /// <summary>
    /// 场景：GetVHost 未配置，应返回默认 %2F。
    /// </summary>
    [Fact]
    public void GetVHost_With_No_Config_Should_Return_Default()
    {
        var configuration = new ConfigurationBuilder().Build();
        var vhost = DlqCleanupJob.GetVHost(configuration);

        Assert.Equal("%2F", vhost);
    }

    /// <summary>
    /// 场景：CreateHttpClient 应配置 Basic Auth 头。
    /// </summary>
    [Fact]
    public void CreateHttpClient_Should_Configure_Basic_Auth_Header()
    {
        var configuration = CreateConfiguration(username: "admin", password: "secret");
        using var httpClient = DlqCleanupJob.CreateHttpClient(configuration);

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("Basic", httpClient.DefaultRequestHeaders.Authorization!.Scheme);
        // 验证 Base64 编码的 admin:secret
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        Assert.Equal(expectedBase64, httpClient.DefaultRequestHeaders.Authorization!.Parameter);
    }

    /// <summary>
    /// 场景：CreateHttpClient 未配置用户名密码，应使用默认 guest:guest。
    /// </summary>
    [Fact]
    public void CreateHttpClient_With_No_Config_Should_Use_Default_Credentials()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var httpClient = DlqCleanupJob.CreateHttpClient(configuration);

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest"));
        Assert.Equal(expectedBase64, httpClient.DefaultRequestHeaders.Authorization!.Parameter);
    }

    private static IConfiguration CreateConfiguration(
        string? host = null,
        string? username = null,
        string? password = null,
        string? vhost = null,
        string? queueNames = null)
    {
        var dict = new Dictionary<string, string?>();

        if (host is not null) dict["RabbitMQ:ManagementApi:Host"] = host;
        if (username is not null) dict["RabbitMQ:ManagementApi:Username"] = username;
        if (password is not null) dict["RabbitMQ:ManagementApi:Password"] = password;
        if (vhost is not null) dict["RabbitMQ:ManagementApi:VHost"] = vhost;
        if (queueNames is not null) dict["DlqCleanup:QueueNames"] = queueNames;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    /// <summary>
    /// 简单的 <see cref="HttpMessageHandler"/> 桩，对所有请求返回固定响应。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// 捕获请求 URL 和方法的 <see cref="HttpMessageHandler"/> 桩。
    /// </summary>
    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public string? LastRequestUrl { get; private set; }
        public HttpMethod? LastRequestMethod { get; private set; }

        public CapturingHttpMessageHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUrl = request.RequestUri?.ToString();
            LastRequestMethod = request.Method;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// 总是抛出异常的 <see cref="HttpMessageHandler"/> 桩。
    /// </summary>
    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }

    /// <summary>
    /// 按顺序返回不同响应的 <see cref="HttpMessageHandler"/> 桩，用于测试多队列混合结果。
    /// </summary>
    private sealed class SequentialStubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(string Body, HttpStatusCode StatusCode)> _responses;

        public SequentialStubHttpMessageHandler(IEnumerable<(string, HttpStatusCode)> responses)
        {
            _responses = new Queue<(string, HttpStatusCode)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (body, statusCode) = _responses.Count > 0
                ? _responses.Dequeue()
                : (string.Empty, HttpStatusCode.OK);

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

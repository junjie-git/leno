using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// CartPriceService 远程失败处理测试。
/// 验证 HTTP 调用失败/异常时抛出 <see cref="AntiCorruptionException"/>，
/// 不再静默返回空集合掩盖故障。
/// </summary>
public class CartPriceServiceTests
{
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task GetSkuPricesAsync_WhenHttpReturnsNonSuccess_ShouldThrowAntiCorruptionException()
    {
        // Arrange：商品域返回 500
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError);
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.GetSkuPricesAsync(new[] { SkuId }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task GetSkuPricesAsync_WhenHttpRequestThrows_ShouldThrowAntiCorruptionException()
    {
        // Arrange：网络异常
        var handler = new StubHttpMessageHandler(throwException: new HttpRequestException("网络不可达"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.GetSkuPricesAsync(new[] { SkuId }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task GetSkuPricesAsync_WhenHttpRequestTimesOut_ShouldThrowAntiCorruptionException()
    {
        // Arrange：HttpClient 超时表现为 TaskCanceledException（无 CancellationToken 取消请求）
        var handler = new StubHttpMessageHandler(throwException: new TaskCanceledException("请求超时"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.GetSkuPricesAsync(new[] { SkuId }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task GetSkuPricesAsync_WhenEmptyInput_ShouldReturnEmptyWithoutCallingHttp()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler);

        // Act
        var result = await sut.GetSkuPricesAsync(Array.Empty<Guid>(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSkuPricesAsync_WhenSuccessButDataEmpty_ShouldReturnEmpty()
    {
        // Arrange：HTTP 200 但 Data 为空
        var payload = new { code = 200, message = "success", data = Array.Empty<object>() };
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
        var sut = CreateSut(handler);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId }, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSkuPricesAsync_WhenSuccessWithData_ShouldReturnSnapshots()
    {
        // Arrange
        var payload = new
        {
            code = 200,
            message = "success",
            data = new[]
            {
                new
                {
                    skuId = SkuId,
                    price = 12.5m,
                    currency = "CNY",
                    available = true,
                    title = "测试商品",
                    mainImageUrl = "https://img.test/1.png",
                    sellerId = SellerId
                }
            }
        };
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
        var sut = CreateSut(handler);

        // Act
        var result = await sut.GetSkuPricesAsync(new[] { SkuId }, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(SkuId);
        result[0].Price.Should().Be(12.5m);
        result[0].Available.Should().BeTrue();
        result[0].Title.Should().Be("测试商品");
        result[0].SellerId.Should().Be(SellerId);
    }

    private static CartPriceService CreateSut(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://test-product-api/") };
        var options = Options.Create(new InternalApiKeyOptions { ApiKey = "test-internal-key" });
        var logger = new Mock<ILogger<CartPriceService>>();
        return new CartPriceService(client, options, logger.Object);
    }

    /// <summary>
    /// 简单的 HttpMessageHandler 桩，可控制状态码、响应体或抛出异常，
    /// 用于隔离 CartPriceService 对 HttpClient 的依赖，避免真实网络调用。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _content;
        private readonly Exception? _throwException;

        public int CallCount { get; private set; }

        public StubHttpMessageHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? content = null,
            Exception? throwException = null)
        {
            _statusCode = statusCode;
            _content = content;
            _throwException = throwException;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_throwException is not null)
            {
                return Task.FromException<HttpResponseMessage>(_throwException);
            }

            var response = new HttpResponseMessage(_statusCode);
            if (_content is not null)
            {
                response.Content = new StringContent(_content, Encoding.UTF8, "application/json");
            }
            return Task.FromResult(response);
        }
    }
}

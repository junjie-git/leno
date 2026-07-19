using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Grpc;

public class GrpcOrderStatusProviderTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { { "Order", "test-key" } }
        });
        return mock.Object;
    }

    [Fact]
    public async Task GetOrderStatus_Success_ReturnsMappedInfo()
    {
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var response = new OrderStatus
        {
            OrderId = orderId.ToString(),
            Status = "3",  // Completed
            UserId = userId.ToString(),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds()
        };

        clientMock.Setup(c => c.GetOrderStatusAsync(
                It.IsAny<GetOrderStatusRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<OrderStatus>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcOrderStatusProvider(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderStatusProvider>.Instance);

        var result = await client.GetOrderStatusAsync(orderId);

        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be(3);
    }

    [Fact]
    public async Task GetOrderStatus_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetOrderStatusAsync(
                It.IsAny<GetOrderStatusRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcOrderStatusProvider(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderStatusProvider>.Instance);

        var act = async () => await client.GetOrderStatusAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("ORDER_UNAVAILABLE");
    }

    [Fact]
    public async Task GetOrderStatus_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "order missing"));

        clientMock.Setup(c => c.GetOrderStatusAsync(
                It.IsAny<GetOrderStatusRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcOrderStatusProvider(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderStatusProvider>.Instance);

        var act = async () => await client.GetOrderStatusAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("ORDER_REMOTE_FAILED");
    }
}

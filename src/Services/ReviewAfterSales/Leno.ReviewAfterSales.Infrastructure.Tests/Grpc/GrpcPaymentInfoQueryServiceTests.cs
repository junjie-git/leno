using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Payment.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Grpc;

public class GrpcPaymentInfoQueryServiceTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { { "Payment", "test-key" } }
        });
        return mock.Object;
    }

    [Fact]
    public async Task GetByOrderId_Success_ReturnsMappedResult()
    {
        var clientMock = new Mock<PaymentInternalService.PaymentInternalServiceClient>();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var response = new PaymentInfo
        {
            PaymentId = paymentId.ToString(),
            OrderId = orderId.ToString(),
            AmountCents = 10000,
            Status = "1",
            Channel = "Alipay"
        };

        clientMock.Setup(c => c.GetPaymentInfoAsync(
                It.IsAny<GetPaymentInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<PaymentInfo>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcPaymentInfoQueryService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPaymentInfoQueryService>.Instance);

        var result = await client.GetByOrderIdAsync(orderId);

        result.Should().NotBeNull();
        result!.PaymentId.Should().Be(paymentId);
        result.Channel.Should().Be("Alipay");
    }

    [Fact]
    public async Task GetByOrderId_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<PaymentInternalService.PaymentInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetPaymentInfoAsync(
                It.IsAny<GetPaymentInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcPaymentInfoQueryService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPaymentInfoQueryService>.Instance);

        var act = async () => await client.GetByOrderIdAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PAYMENT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetByOrderId_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<PaymentInternalService.PaymentInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "payment missing"));

        clientMock.Setup(c => c.GetPaymentInfoAsync(
                It.IsAny<GetPaymentInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcPaymentInfoQueryService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPaymentInfoQueryService>.Instance);

        var act = async () => await client.GetByOrderIdAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("PAYMENT_REMOTE_FAILED");
    }
}

using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.Order.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Grpc;

public class GrpcPromotionAntiCorruptionClientTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { ["Promotion"] = "test-key" }
        });
        return mock.Object;
    }

    [Fact]
    public async Task CalculateDiscount_Success_ReturnsDecimal()
    {
        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        var response = new CalculateDiscountResponse { DiscountCents = 12345 };

        clientMock.Setup(c => c.CalculateDiscountAsync(
                It.IsAny<CalculateDiscountRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<CalculateDiscountResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcPromotionAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPromotionAntiCorruptionClient>.Instance);

        var result = await client.CalculateDiscountAsync(Guid.NewGuid(), new List<(Guid, decimal)> { (Guid.NewGuid(), 100m) });

        result.Should().Be(123.45m);
    }

    [Fact]
    public async Task CalculateDiscount_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.CalculateDiscountAsync(
                It.IsAny<CalculateDiscountRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcPromotionAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPromotionAntiCorruptionClient>.Instance);

        var act = async () => await client.CalculateDiscountAsync(Guid.NewGuid(), new List<(Guid, decimal)>());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PROMOTION_UNAVAILABLE");
    }

    [Fact]
    public async Task LockCoupon_Success_Completes()
    {
        var clientMock = new Mock<PromotionInternalService.PromotionInternalServiceClient>();
        clientMock.Setup(c => c.LockCouponAsync(
                It.IsAny<LockCouponRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<LockCouponResponse>(
                Task.FromResult(new LockCouponResponse { Success = true }),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcPromotionAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPromotionAntiCorruptionClient>.Instance);

        var act = async () => await client.LockCouponAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}

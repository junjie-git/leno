using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.Order.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Points.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Grpc;

public class GrpcPointsAntiCorruptionClientTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { ["PointsMembership"] = "test-key" }
        });
        return mock.Object;
    }

    [Fact]
    public async Task TryOffset_Success_ReturnsDecimal()
    {
        var clientMock = new Mock<PointsInternalService.PointsInternalServiceClient>();
        var response = new TrialOffsetResponse { OffsetCents = 500, Success = true };

        clientMock.Setup(c => c.TrialOffsetAsync(
                It.IsAny<TrialOffsetRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<TrialOffsetResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcPointsAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPointsAntiCorruptionClient>.Instance);

        var result = await client.TryOffsetAsync(Guid.NewGuid(), 100);

        result.Should().Be(5m);
    }

    [Fact]
    public async Task Freeze_Unavailable_ThrowsAntiCorruptionException()
    {
        var clientMock = new Mock<PointsInternalService.PointsInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.FreezeAsync(
                It.IsAny<FreezeRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcPointsAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPointsAntiCorruptionClient>.Instance);

        var act = async () => await client.FreezeAsync(Guid.NewGuid(), Guid.NewGuid(), 100);
        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("POINTS_UNAVAILABLE");
    }

    [Fact]
    public async Task Release_Success_Completes()
    {
        var clientMock = new Mock<PointsInternalService.PointsInternalServiceClient>();
        clientMock.Setup(c => c.ReleaseAsync(
                It.IsAny<ReleaseRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<ReleaseResponse>(
                Task.FromResult(new ReleaseResponse { Success = true }),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcPointsAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcPointsAntiCorruptionClient>.Instance);

        var act = async () => await client.ReleaseAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}

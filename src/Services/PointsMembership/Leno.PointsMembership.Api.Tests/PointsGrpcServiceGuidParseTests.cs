using Grpc.Core;
using Leno.PointsMembership.Api.GrpcServices;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Grpc.Points.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Api.Tests;

/// <summary>
/// 验证 <see cref="PointsGrpcService"/> 的 TrialOffset/Freeze/Release 三个 RPC
/// 使用 <c>Guid.TryParse</c> 校验入参，非法 Guid 格式时抛 <see cref="RpcException"/>（InvalidArgument）。
/// 关联审计 PM-L05：原先使用 <c>new Guid(request.UserId)</c> 在格式非法时抛 <see cref="ArgumentException"/>，
/// 导致 gRPC 客户端收到 Unknown 状态码而非 InvalidArgument。
/// </summary>
public class PointsGrpcServiceGuidParseTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    private static PointsGrpcService CreateSut(Mock<IPointsInternalAppService>? appServiceMock = null)
    {
        appServiceMock ??= new Mock<IPointsInternalAppService>();
        return new PointsGrpcService(
            appServiceMock.Object,
            Mock.Of<IPointsAccountRepository>(),
            NullLogger<PointsGrpcService>.Instance);
    }

    #region TrialOffset

    [Fact]
    public async Task TrialOffset_InvalidUserId_Should_Throw_InvalidArgument()
    {
        // Arrange：Strict 模式确保 TrialOffsetAsync 不被调用
        var appServiceMock = new Mock<IPointsInternalAppService>(MockBehavior.Strict);
        var sut = CreateSut(appServiceMock);
        var request = new TrialOffsetRequest { UserId = "not-a-guid", PointsToUse = 100 };

        // Act & Assert
        var act = async () => await sut.TrialOffset(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        appServiceMock.Verify(
            s => s.TrialOffsetAsync(It.IsAny<TrialOffsetDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TrialOffset_ValidUserId_Should_Pass_Guid_To_AppService()
    {
        // Arrange
        var appServiceMock = new Mock<IPointsInternalAppService>();
        appServiceMock
            .Setup(s => s.TrialOffsetAsync(It.IsAny<TrialOffsetDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrialOffsetResultDto { OffsetAmount = 1.5m, Currency = "CNY" });
        var sut = CreateSut(appServiceMock);
        var request = new TrialOffsetRequest { UserId = UserId.ToString(), PointsToUse = 150 };

        // Act
        var result = await sut.TrialOffset(request, new TestServerCallContext());

        // Assert：Guid 正确解析并传递给应用服务
        result.Success.Should().BeTrue();
        result.OffsetCents.Should().Be(150);
        appServiceMock.Verify(
            s => s.TrialOffsetAsync(
                It.Is<TrialOffsetDto>(d => d.UserId == UserId && d.PointsToUse == 150),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Freeze

    [Fact]
    public async Task Freeze_InvalidUserId_Should_Throw_InvalidArgument()
    {
        var appServiceMock = new Mock<IPointsInternalAppService>(MockBehavior.Strict);
        var sut = CreateSut(appServiceMock);
        var request = new FreezeRequest
        {
            UserId = "bad-user",
            OrderId = OrderId.ToString(),
            PointsToUse = 100
        };

        var act = async () => await sut.Freeze(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        appServiceMock.Verify(
            s => s.FreezeAsync(It.IsAny<FreezePointsDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Freeze_InvalidOrderId_Should_Throw_InvalidArgument()
    {
        var appServiceMock = new Mock<IPointsInternalAppService>(MockBehavior.Strict);
        var sut = CreateSut(appServiceMock);
        var request = new FreezeRequest
        {
            UserId = UserId.ToString(),
            OrderId = "bad-order",
            PointsToUse = 100
        };

        var act = async () => await sut.Freeze(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        appServiceMock.Verify(
            s => s.FreezeAsync(It.IsAny<FreezePointsDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Freeze_ValidUserAndOrder_Should_Pass_Guids_To_AppService()
    {
        var appServiceMock = new Mock<IPointsInternalAppService>();
        appServiceMock
            .Setup(s => s.FreezeAsync(It.IsAny<FreezePointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appServiceMock);
        var request = new FreezeRequest
        {
            UserId = UserId.ToString(),
            OrderId = OrderId.ToString(),
            PointsToUse = 200
        };

        var result = await sut.Freeze(request, new TestServerCallContext());

        result.Success.Should().BeTrue();
        appServiceMock.Verify(
            s => s.FreezeAsync(
                It.Is<FreezePointsDto>(d => d.UserId == UserId && d.OrderId == OrderId && d.PointsToUse == 200),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Release

    [Fact]
    public async Task Release_InvalidOrderId_Should_Throw_InvalidArgument()
    {
        var appServiceMock = new Mock<IPointsInternalAppService>(MockBehavior.Strict);
        var sut = CreateSut(appServiceMock);
        var request = new ReleaseRequest { OrderId = "not-a-guid" };

        var act = async () => await sut.Release(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        appServiceMock.Verify(
            s => s.ReleaseAsync(It.IsAny<ReleasePointsDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Release_ValidOrderId_Should_Pass_Guid_To_AppService()
    {
        var appServiceMock = new Mock<IPointsInternalAppService>();
        appServiceMock
            .Setup(s => s.ReleaseAsync(It.IsAny<ReleasePointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appServiceMock);
        var request = new ReleaseRequest { OrderId = OrderId.ToString() };

        var result = await sut.Release(request, new TestServerCallContext());

        result.Success.Should().BeTrue();
        appServiceMock.Verify(
            s => s.ReleaseAsync(
                It.Is<ReleasePointsDto>(d => d.OrderId == OrderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

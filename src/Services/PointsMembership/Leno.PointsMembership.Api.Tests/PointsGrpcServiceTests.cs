using Grpc.Core;
using Leno.PointsMembership.Api.GrpcServices;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Grpc.Points.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Api.Tests;

/// <summary>
/// PointsGrpcService.Confirm 单元测试。
/// 验证 gRPC handler 将 proto 字段正确解析为 Guid 并调用对应 AppService 方法，
/// 非法 Guid 时抛 InvalidArgument。范本：PromotionGrpcServiceTests。
/// </summary>
public class PointsGrpcServiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task Confirm_ValidOrderId_ReturnsSuccess()
    {
        // Arrange
        var internalAppServiceMock = new Mock<IPointsInternalAppService>();
        var sut = new PointsGrpcService(
            internalAppServiceMock.Object,
            Mock.Of<IPointsAccountRepository>(),
            NullLogger<PointsGrpcService>.Instance);

        var request = new ConfirmRequest { OrderId = OrderId.ToString() };

        // Act
        var result = await sut.Confirm(request, new TestServerCallContext());

        // Assert：返回 Success=true 且调用一次 ConfirmAsync
        result.Success.Should().BeTrue();
        internalAppServiceMock.Verify(
            s => s.ConfirmAsync(It.Is<ConfirmPointsDto>(d => d.OrderId == OrderId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Confirm_InvalidGuid_ThrowsInvalidArgument()
    {
        // Arrange：使用 Strict 模式以确保 ConfirmAsync 不被调用
        var internalAppServiceMock = new Mock<IPointsInternalAppService>(MockBehavior.Strict);
        var sut = new PointsGrpcService(
            internalAppServiceMock.Object,
            Mock.Of<IPointsAccountRepository>(),
            NullLogger<PointsGrpcService>.Instance);

        var request = new ConfirmRequest { OrderId = "not-a-guid" };

        // Act & Assert：抛 RpcException 且状态码为 InvalidArgument
        var act = async () => await sut.Confirm(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        internalAppServiceMock.Verify(
            s => s.ConfirmAsync(It.IsAny<ConfirmPointsDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

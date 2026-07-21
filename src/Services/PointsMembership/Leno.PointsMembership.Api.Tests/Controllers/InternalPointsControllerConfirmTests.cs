using Leno.PointsMembership.Api.Controllers;
using Leno.PointsMembership.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.PointsMembership.Api.Tests.Controllers;

/// <summary>
/// 验证 <see cref="InternalPointsController.ConfirmAsync"/> HTTP 端点存在且调用应用服务。
/// 该端点与 gRPC <c>ConfirmPointsAsync</c> 能力对齐，供订单域通过 HTTP 调用积分确认。
/// </summary>
public class InternalPointsControllerConfirmTests
{
    [Fact]
    public async Task ConfirmAsync_ShouldReturnSuccess_WhenServiceSucceeds()
    {
        // Arrange
        var mockService = new Mock<IPointsInternalAppService>();
        mockService.Setup(x => x.ConfirmAsync(It.IsAny<ConfirmPointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new InternalPointsController(mockService.Object);
        var input = new ConfirmPointsDto(OrderId: Guid.NewGuid());

        // Act
        var result = await controller.ConfirmAsync(input, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue("积分确认应成功");
        mockService.Verify(x => x.ConfirmAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConfirmAsync_ShouldHaveInternalRouteAttribute()
    {
        // Arrange
        var method = typeof(InternalPointsController)
            .GetMethod("ConfirmAsync");

        // Assert — 应有 internal/v1/points/confirm 路由特性
        method.Should().NotBeNull("ConfirmAsync 方法应存在");
        var httpPostAttrs = method!.GetCustomAttributes(typeof(HttpPostAttribute), false);
        httpPostAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPost 特性");
        var routeTemplates = httpPostAttrs.Cast<HttpPostAttribute>().Select(a => a.Template).ToList();
        routeTemplates.Should().Contain("internal/v1/points/confirm",
            "Confirm 端点路由应为 internal/v1/points/confirm，与 Freeze/Release 对齐");
    }
}

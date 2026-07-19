using FluentAssertions;
using Grpc.Core;
using Leno.SellerShop.Api.GrpcServices;
using Leno.SellerShop.Application;
using Leno.SharedContracts.Grpc.Seller.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SellerShop.Infrastructure.Tests.Grpc;

public class SellerGrpcServiceTests
{
    [Fact]
    public async Task GetSellerInfo_Success_ReturnsMappedInfo()
    {
        var queryMock = new Mock<ISellerInternalQueryService>();
        var sellerId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        queryMock.Setup(q => q.GetSellerInfoAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SellerInfoDto
            {
                SellerId = sellerId,
                Name = "张三店铺",
                Status = "Approved",
                ShopId = shopId
            });

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var result = await svc.GetSellerInfo(
            new GetSellerInfoRequest { SellerId = sellerId.ToString() },
            new TestServerCallContext());

        result.SellerId.Should().Be(sellerId.ToString());
        result.Name.Should().Be("张三店铺");
        result.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task GetSellerInfo_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ISellerInternalQueryService>();
        queryMock.Setup(q => q.GetSellerInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SellerInfoDto?)null);

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.GetSellerInfo(
            new GetSellerInfoRequest { SellerId = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GetSellerInfo_InvalidArgument_ThrowsRpcException()
    {
        var queryMock = new Mock<ISellerInternalQueryService>(MockBehavior.Strict);
        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.GetSellerInfo(
            new GetSellerInfoRequest { SellerId = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GetShopInfo_Success_ReturnsMappedInfo()
    {
        var queryMock = new Mock<ISellerInternalQueryService>();
        var shopId = new Guid(42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var sellerId = Guid.NewGuid();
        queryMock.Setup(q => q.GetShopInfoAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopInfoDto
            {
                ShopId = shopId,
                Name = "测试店铺",
                Status = "Active",
                SellerId = sellerId
            });

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var result = await svc.GetShopInfo(
            new GetShopInfoRequest { ShopId = 42L },
            new TestServerCallContext());

        result.Name.Should().Be("测试店铺");
        result.Status.Should().Be("Active");
        result.SellerId.Should().Be(sellerId.ToString());
    }

    [Fact]
    public async Task GetShopInfo_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ISellerInternalQueryService>();
        queryMock.Setup(q => q.GetShopInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopInfoDto?)null);

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.GetShopInfo(
            new GetShopInfoRequest { ShopId = 42L },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateSellerOwnership_AlwaysThrows_Unimplemented()
    {
        var queryMock = new Mock<ISellerInternalQueryService>(MockBehavior.Strict);
        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.ValidateSellerOwnership(
            new ValidateSellerOwnershipRequest
            {
                SellerId = Guid.NewGuid().ToString(),
                ResourceType = "shop",
                ResourceId = "1"
            },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.Unimplemented);
    }
}

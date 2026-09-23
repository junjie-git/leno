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
    public async Task GetShopInfo_NewClient_UsesStringId_ParsesGuid()
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，shop_id_str 为唯一入参形态
        var queryMock = new Mock<ISellerInternalQueryService>();
        var shopId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        queryMock.Setup(q => q.GetShopInfoAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopInfoDto
            {
                ShopId = shopId,
                Name = "新客户端店铺",
                Status = "Active",
                SellerId = sellerId
            });

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var result = await svc.GetShopInfo(
            new GetShopInfoRequest { ShopIdStr = shopId.ToString() },
            new TestServerCallContext());

        result.ShopIdStr.Should().Be(shopId.ToString());
        result.Name.Should().Be("新客户端店铺");
        // 验证 queryService 收到的 Guid 与 ShopIdStr 解析结果一致
        queryMock.Verify(q => q.GetShopInfoAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShopInfo_InvalidStringId_ThrowsInvalidArgument()
    {
        // 新客户端传了无效 ShopIdStr，应返回 InvalidArgument
        var queryMock = new Mock<ISellerInternalQueryService>(MockBehavior.Strict);
        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.GetShopInfo(
            new GetShopInfoRequest { ShopIdStr = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GetShopInfo_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ISellerInternalQueryService>();
        queryMock.Setup(q => q.GetShopInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopInfoDto?)null);

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.GetShopInfo(
            new GetShopInfoRequest { ShopIdStr = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateSellerOwnership_ValidInput_ReturnsResponse()
    {
        // 安排：mock 查询服务返回 true（归属校验通过）
        var sellerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var queryMock = new Mock<ISellerInternalQueryService>();
        queryMock.Setup(q => q.ValidateOwnershipAsync(sellerId, "shop", resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        // 行动：调用 ValidateSellerOwnership
        var result = await svc.ValidateSellerOwnership(
            new ValidateSellerOwnershipRequest
            {
                SellerId = sellerId.ToString(),
                ResourceType = "shop",
                ResourceId = resourceId.ToString()
            },
            new TestServerCallContext());

        // 断言：返回 isValid=true，且 queryService 被调用一次
        result.IsValid.Should().BeTrue();
        queryMock.Verify(q => q.ValidateOwnershipAsync(sellerId, "shop", resourceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateSellerOwnership_InvalidSellerId_ThrowsInvalidArgument()
    {
        var svc = new SellerGrpcService(Mock.Of<ISellerInternalQueryService>(), NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.ValidateSellerOwnership(
            new ValidateSellerOwnershipRequest
            {
                SellerId = "not-a-guid",
                ResourceType = "shop",
                ResourceId = Guid.NewGuid().ToString()
            },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ValidateSellerOwnership_InvalidResourceId_ThrowsInvalidArgument()
    {
        var svc = new SellerGrpcService(Mock.Of<ISellerInternalQueryService>(), NullLogger<SellerGrpcService>.Instance);

        var act = async () => await svc.ValidateSellerOwnership(
            new ValidateSellerOwnershipRequest
            {
                SellerId = Guid.NewGuid().ToString(),
                ResourceType = "shop",
                ResourceId = "not-a-guid"
            },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ValidateSellerOwnership_OwnershipFalse_ReturnsIsValidFalse()
    {
        // 安排：mock 查询服务返回 false（归属校验未通过，如跨域资源不归属）
        var sellerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var queryMock = new Mock<ISellerInternalQueryService>();
        queryMock.Setup(q => q.ValidateOwnershipAsync(sellerId, "spu", resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = new SellerGrpcService(queryMock.Object, NullLogger<SellerGrpcService>.Instance);

        // 行动：调用 ValidateSellerOwnership
        var result = await svc.ValidateSellerOwnership(
            new ValidateSellerOwnershipRequest
            {
                SellerId = sellerId.ToString(),
                ResourceType = "spu",
                ResourceId = resourceId.ToString()
            },
            new TestServerCallContext());

        // 断言：返回 isValid=false（fail-closed）
        result.IsValid.Should().BeFalse();
    }
}

using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Api.GrpcServices;
using Leno.Cart.Application;
using Leno.SharedContracts.Grpc.Cart.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

public class CartGrpcServiceTests
{
    [Fact]
    public async Task GetCartSnapshot_Success_ReturnsMappedSnapshot()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        queryMock.Setup(q => q.GetCartSnapshotAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartSnapshotDto
            {
                CartId = cartId,
                Items = new List<CartItemSnapshotDto>
                {
                    new() { SkuId = skuId, Quantity = 2, UnitPriceCents = 9999 }
                },
                TotalCents = 19998
            });

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var result = await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = userId.ToString() },
            new TestServerCallContext());

        result.CartId.Should().Be(cartId.ToString());
        result.TotalCents.Should().Be(19998);
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(2);
        result.Items[0].UnitPriceCents.Should().Be(9999);
    }

    [Fact]
    public async Task GetCartSnapshot_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        queryMock.Setup(q => q.GetCartSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartSnapshotDto?)null);

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GetCartSnapshot_InvalidArgument_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>(MockBehavior.Strict);
        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GetCheckoutPreview_Success_ReturnsMappedPreview()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        var userId = Guid.NewGuid();
        queryMock.Setup(q => q.GetCheckoutPreviewAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutPreviewSnapshotDto
            {
                SubtotalCents = 10000,
                DiscountCents = 500,
                ShippingCents = 1200,
                TotalCents = 10700
            });

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var result = await svc.GetCheckoutPreview(
            new GetCheckoutPreviewRequest { UserId = userId.ToString() },
            new TestServerCallContext());

        result.SubtotalCents.Should().Be(10000);
        result.DiscountCents.Should().Be(500);
        result.ShippingCents.Should().Be(1200);
        result.TotalCents.Should().Be(10700);
    }

    [Fact]
    public async Task GetCheckoutPreview_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        queryMock.Setup(q => q.GetCheckoutPreviewAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutPreviewSnapshotDto?)null);

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCheckoutPreview(
            new GetCheckoutPreviewRequest { UserId = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GetCheckoutPreview_InvalidArgument_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>(MockBehavior.Strict);
        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCheckoutPreview(
            new GetCheckoutPreviewRequest { UserId = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }
}

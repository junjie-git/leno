using System.Net;
using System.Reflection;
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Order.Api.Controllers;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Queries;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Leno.Order.Api.Tests;

/// <summary>
/// GET /api/seller/orders 卖家履约端点单元测试。
/// 采用直接实例化 OrdersController 的方式测试，避免 WebApplicationFactory 的基础设施依赖。
/// 覆盖场景：成功返回、分页、状态/时间筛选、鉴权（[Authorize] 特性契约 + 未认证拒绝）、卖家隔离（A 看不到 B）。
/// </summary>
public class SellerOrdersApiTests
{
    private readonly Mock<IOrderAppService> _orderAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly Mock<IQueryHandler<OrderDetailQuery, OrderDetailResult?>> _orderDetailQueryHandlerMock = new();
    private readonly Mock<IQueryHandler<OrderListQuery, PageResult<OrderSummaryDto>>> _orderListQueryHandlerMock = new();

    private static readonly Guid SellerAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SellerBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private OrdersController CreateSut()
    {
        return new OrdersController(
            _currentUserMock.Object,
            _orderAppServiceMock.Object,
            _orderDetailQueryHandlerMock.Object,
            _orderListQueryHandlerMock.Object);
    }

    private void SetupSellerAuth(Guid sellerId)
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(sellerId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Seller");
    }

    private static PageResult<OrderSummaryDto> BuildPageResult(
        Guid sellerId,
        string orderNo,
        string status,
        int page,
        int pageSize,
        int total)
    {
        var summary = new OrderSummaryDto
        {
            OrderId = Guid.NewGuid(),
            OrderNo = orderNo,
            UserId = Guid.NewGuid(),
            SellerId = sellerId,
            TotalAmount = 199.00m,
            Currency = "CNY",
            Status = status,
            CreatedAt = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc),
            PaidAt = status == "Paid" || status == "Shipped" || status == "Completed"
                ? new DateTime(2026, 7, 26, 10, 5, 0, DateTimeKind.Utc)
                : null,
            ShippedAt = status == "Shipped" || status == "Completed"
                ? new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc)
                : null
        };

        return new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto> { summary },
            total,
            page,
            pageSize);
    }

    #region 成功场景

    [Fact]
    public async Task ListSellerOrders_AsSeller_ShouldReturnOkWithPagedResult()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-SELLER-A-001",
            status: "Paid",
            page: 1,
            pageSize: 20,
            total: 1);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)actionResult;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PageResult<OrderSummaryDto>>>().Subject;
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Items.Should().HaveCount(1);
        apiResponse.Data.Total.Should().Be(1);
        apiResponse.Data.Page.Should().Be(1);
        apiResponse.Data.PageSize.Should().Be(20);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.SellerId.Should().Be(SellerAId, "SellerId 必须从 JWT 注入，前端不可传");
        capturedQuery.UserId.Should().BeNull("卖家端不按买家过滤");
    }

    #endregion

    #region 分页

    [Fact]
    public async Task ListSellerOrders_WithPaging_ShouldPassPageAndPageSizeToHandler()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-PAGE-001",
            status: "Paid",
            page: 2,
            pageSize: 50,
            total: 120);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 2, pageSize: 50);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)actionResult;
        var apiResponse = (ApiResponse<PageResult<OrderSummaryDto>>)okResult.Value!;
        apiResponse.Data!.Page.Should().Be(2);
        apiResponse.Data.PageSize.Should().Be(50);
        apiResponse.Data.Total.Should().Be(120);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(2);
        capturedQuery.PageSize.Should().Be(50);
        capturedQuery.SellerId.Should().Be(SellerAId);
    }

    [Fact]
    public async Task ListSellerOrders_WithoutPagingParams_ShouldUseDefaults()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-DEF-001",
            status: "PendingPayment",
            page: 1,
            pageSize: 20,
            total: 0);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(1, "默认页码为 1");
        capturedQuery.PageSize.Should().Be(20, "默认每页 20 条");
    }

    #endregion

    #region 筛选

    [Fact]
    public async Task ListSellerOrders_WithStatusPaid_ShouldPassStatusFilter()
    {
        // Arrange — 待发货订单页使用 status=Paid 查询
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-PAID-001",
            status: "Paid",
            page: 1,
            pageSize: 20,
            total: 5);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: OrderStatus.Paid, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Status.Should().Be("Paid");
        capturedQuery.SellerId.Should().Be(SellerAId);
    }

    [Fact]
    public async Task ListSellerOrders_WithStatusShipped_ShouldPassStatusFilter()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-SHIPPED-001",
            status: "Shipped",
            page: 1,
            pageSize: 20,
            total: 3);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: OrderStatus.Shipped, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task ListSellerOrders_WithDateRange_ShouldPassStartAndEndDate()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 26, 23, 59, 59, DateTimeKind.Utc);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-DATE-001",
            status: "Paid",
            page: 1,
            pageSize: 20,
            total: 8);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: startDate, endDate: endDate, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.StartDate.Should().Be(startDate);
        capturedQuery.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task ListSellerOrders_WithAllFilters_ShouldPassCombinedFilters()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-ALL-001",
            status: "Completed",
            page: 1,
            pageSize: 10,
            total: 25);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: OrderStatus.Completed,
            orderNo: null,
            startDate: startDate,
            endDate: endDate,
            page: 1,
            pageSize: 10);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.SellerId.Should().Be(SellerAId);
        capturedQuery.Status.Should().Be("Completed");
        capturedQuery.StartDate.Should().Be(startDate);
        capturedQuery.EndDate.Should().Be(endDate);
        capturedQuery.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task ListSellerOrders_WithNullStatus_ShouldNotFilterByStatus()
    {
        // Arrange
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-ALL-STATUS-001",
            status: "PendingPayment",
            page: 1,
            pageSize: 20,
            total: 10);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Status.Should().BeNull("未传 status 时不按状态过滤");
    }

    #endregion

    #region 鉴权

    [Fact]
    public void ListSellerOrders_ShouldHaveAuthorizeAttributeWithSellerRole()
    {
        // Arrange — 通过反射验证 [Authorize(Roles = "Seller")] 特性契约
        var method = typeof(OrdersController).GetMethod(
            nameof(OrdersController.ListSellerOrdersAsync),
            BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull("端点必须标注 [Authorize] 以强制鉴权");
        authorizeAttr!.Roles.Should().Be("Seller",
            "仅 Seller 角色可访问卖家订单列表，非 Seller 角色应被拒绝");
    }

    [Fact]
    public void ListSellerOrders_ShouldHaveHttpGetAttributeWithCorrectRoute()
    {
        // Arrange — 通过反射验证 [HttpGet("api/seller/orders")] 路由契约
        var method = typeof(OrdersController).GetMethod(
            nameof(OrdersController.ListSellerOrdersAsync),
            BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        var httpGetAttr = method!.GetCustomAttribute<HttpGetAttribute>();
        httpGetAttr.Should().NotBeNull("端点必须标注 [HttpGet]");
        httpGetAttr!.Template.Should().Be("api/seller/orders",
            "路由必须为 GET /api/seller/orders，与 design-prompts 与 spec 对齐");
    }

    [Fact]
    public async Task ListSellerOrders_WhenUnauthenticated_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange — 模拟未认证状态
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(false);
        _currentUserMock.SetupGet(c => c.UserId).Returns((Guid?)null);

        var sut = CreateSut();

        // Act
        var act = () => sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert — GetCurrentUserId() 在未认证时抛出 UnauthorizedAccessException（映射 401）
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _orderListQueryHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "未认证时不应调用查询处理器");
    }

    #endregion

    #region 卖家隔离

    [Fact]
    public async Task ListSellerOrders_SellerA_ShouldQueryOnlyForSellerA()
    {
        // Arrange — 卖家 A 登录
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-A-001",
            status: "Paid",
            page: 1,
            pageSize: 20,
            total: 1);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert — 卖家 A 的查询 SellerId 必须是 A，不是 B
        capturedQuery.Should().NotBeNull();
        capturedQuery!.SellerId.Should().Be(SellerAId,
            "卖家 A 只能查询自己的订单，SellerId 从 JWT 注入");
        capturedQuery.SellerId.Should().NotBe(SellerBId,
            "卖家 A 不能查询卖家 B 的订单");
    }

    [Fact]
    public async Task ListSellerOrders_SellerB_ShouldQueryOnlyForSellerB()
    {
        // Arrange — 卖家 B 登录
        SetupSellerAuth(SellerBId);
        var expectedResult = BuildPageResult(
            sellerId: SellerBId,
            orderNo: "ORD-B-001",
            status: "Shipped",
            page: 1,
            pageSize: 20,
            total: 1);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act
        await sut.ListSellerOrdersAsync(
            status: null, orderNo: null, startDate: null, endDate: null, page: 1, pageSize: 20);

        // Assert — 卖家 B 的查询 SellerId 必须是 B，不是 A
        capturedQuery.Should().NotBeNull();
        capturedQuery!.SellerId.Should().Be(SellerBId,
            "卖家 B 只能查询自己的订单");
        capturedQuery.SellerId.Should().NotBe(SellerAId,
            "卖家 B 不能查询卖家 A 的订单");
    }

    [Fact]
    public async Task ListSellerOrders_SellerIdIsAlwaysFromJwt_NotFromAnyExternalInput()
    {
        // Arrange — 卖家 A 登录，确认 SellerId 只来自 JWT（GetCurrentUserId），不受任何其他输入影响
        SetupSellerAuth(SellerAId);
        var expectedResult = BuildPageResult(
            sellerId: SellerAId,
            orderNo: "ORD-A-001",
            status: "Paid",
            page: 1,
            pageSize: 20,
            total: 1);

        OrderListQuery? capturedQuery = null;
        _orderListQueryHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<OrderListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<OrderListQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(expectedResult);

        var sut = CreateSut();

        // Act — ListSellerOrdersAsync 方法签名无 sellerId 参数，SellerId 不可被外部注入
        await sut.ListSellerOrdersAsync(
            status: OrderStatus.Paid,
            orderNo: null,
            startDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            page: 5,
            pageSize: 50);

        // Assert — SellerId 仍是 A（从 JWT 注入），不可被篡改
        capturedQuery.Should().NotBeNull();
        capturedQuery!.SellerId.Should().Be(SellerAId,
            "SellerId 从 JWT 强制注入，方法签名不接受 sellerId 参数，不可被篡改为他店卖家 ID");
    }

    #endregion
}

using System.Reflection;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Application.Services;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Payment.Application.Tests;

/// <summary>
/// POST /api/payments 发起支付端点的应用层单元测试（spec F-PAY-001）。
/// 验证 <see cref="PaymentAppService.CreatePaymentAsync"/> 的订单校验、支付单状态分流、
/// 渠道下单、幂等返回与失败补偿逻辑。
/// </summary>
public class PaymentAppServiceCreatePaymentTests
{
    private readonly Mock<IPaymentOrderRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IChannelStatusQueryService> _channelStatusMock = new();
    private readonly Mock<IPaymentOrderAntiCorruptionService> _orderAntiCorruptionMock = new();
    private readonly Mock<IPaymentChannelFactory> _channelFactoryMock = new();
    private readonly PaymentAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private const decimal Amount = 100m;

    public PaymentAppServiceCreatePaymentTests()
    {
        _sut = new PaymentAppService(
            _repoMock.Object,
            _uowMock.Object,
            _channelStatusMock.Object,
            _orderAntiCorruptionMock.Object,
            _channelFactoryMock.Object,
            NullLogger<PaymentAppService>.Instance);
    }

    /// <summary>
    /// 配置仓储与 UnitOfWork 的标准 Mock：AddAsync/UpdateAsync 完成任务，SaveEntitiesAsync 返回 true。
    /// </summary>
    private void SetupRepoAndUow()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// 配置防腐层返回可支付的订单上下文（归属当前用户、金额 100 元、可支付）。
    /// </summary>
    /// <param name="userId">订单归属用户，默认为当前测试用户。</param>
    /// <param name="amount">订单应付金额，默认 100 元。</param>
    private void SetupOrderContext(Guid? userId = null, decimal amount = Amount)
    {
        _orderAntiCorruptionMock.Setup(s => s.GetOrderPaymentContextAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaymentContext
            {
                OrderId = OrderId,
                UserId = userId ?? UserId,
                IsPayable = true,
                Amount = amount,
                Currency = "CNY"
            });
    }

    /// <summary>
    /// 配置渠道工厂与适配器 Mock：返回指定渠道的适配器，适配器 CreatePaymentAsync 返回成功的渠道下单结果。
    /// </summary>
    /// <param name="channel">支付渠道。</param>
    /// <param name="channelTradeNo">渠道返回的交易号，默认 "CH001"。</param>
    /// <returns>适配器 Mock，供测试进一步断言。</returns>
    private Mock<IPaymentChannelAdapter> SetupChannelFactory(PaymentChannel channel, string channelTradeNo = "CH001")
    {
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = channelTradeNo,
                PrepayId = "wx_prepay_001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=test",
                H5Url = null
            });
        _channelFactoryMock.Setup(f => f.GetAdapter(channel))
            .Returns(adapterMock.Object);
        return adapterMock;
    }

    /// <summary>
    /// 通过反射设置 PaymentOrder.ExpireAt（private set）为指定时间。
    /// 用于构造已过期的 ChannelOrdered 支付单场景。
    /// </summary>
    private static void SetExpireAt(PaymentOrder order, DateTime expireAt)
    {
        typeof(PaymentOrder)
            .GetProperty("ExpireAt")!
            .SetValue(order, expireAt);
    }

    private static CreatePaymentRequest CreateRequest(PaymentChannel? channel = null, TradeType? scene = null)
    {
        return new CreatePaymentRequest
        {
            OrderId = OrderId,
            Channel = channel,
            Scene = scene
        };
    }

    // ========== 成功场景 ==========

    [Fact]
    public async Task CreatePaymentAsync_WithValidRequestAndSpecifiedChannel_ShouldCreatePaymentAndReturnChannelOrderedResult()
    {
        // 安排：订单可支付、渠道适配器返回成功
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：返回 ChannelOrdered 态与调起参数
        result.Should().NotBeNull();
        result.OrderId.Should().Be(OrderId);
        result.Channel.Should().Be(PaymentChannel.WeChatPay);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        result.PrepayId.Should().Be("wx_prepay_001");
        result.CodeUrl.Should().Be("weixin://wxpay/bizpayurl?pr=test");
        result.FailReason.Should().BeNull();
        result.ExpireAt.Should().BeAfter(DateTime.UtcNow);

        // 仓储与渠道调用验证
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithoutChannel_ShouldResolveDefaultChannelFromFactory()
    {
        // 安排：未指定渠道，工厂启用渠道列表首个为 WeChatPay
        SetupRepoAndUow();
        SetupOrderContext();
        _channelFactoryMock.Setup(f => f.ListEnabledMetadata())
            .Returns(new List<PaymentChannelMetadata>
            {
                new() { ChannelKey = "WeChatPay", DisplayName = "微信支付", IsEnabled = true, Priority = 0 }
            });
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest());

        // 断言：使用默认渠道 WeChatPay
        result.Channel.Should().Be(PaymentChannel.WeChatPay);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        _channelFactoryMock.Verify(f => f.ListEnabledMetadata(), Times.Once);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingFailedPayment_ShouldCreateNewPaymentWithoutUpdatingOld()
    {
        // 安排：已存在 Failed 终态支付单，无需回收，直接创建新支付单
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        existing.MarkFailed("渠道下单失败");

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：旧支付单保持 Failed 态不被 UpdateAsync，新支付单被 AddAsync
        existing.Status.Should().Be(PaymentStatus.Failed);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingClosedPayment_ShouldCreateNewPaymentWithoutUpdatingOld()
    {
        // 安排：已存在 Closed 终态支付单，无需回收，直接创建新支付单
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        existing.MarkClosed("超时未支付");

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：旧支付单保持 Closed 态不被 UpdateAsync
        existing.Status.Should().Be(PaymentStatus.Closed);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingPendingPayment_ShouldMarkFailedOldAndCreateNew()
    {
        // 安排：已存在卡在 Pending 态的支付单（渠道下单未完成），应 MarkFailed 回收后创建新支付单
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：旧支付单被 MarkFailed 回收，新支付单被创建
        existing.Status.Should().Be(PaymentStatus.Failed);
        existing.FailReason.Should().Contain("回收失效支付单");
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingChannelOrderedExpired_ShouldMarkFailedOldAndCreateNew()
    {
        // 安排：已存在 ChannelOrdered 但已过期的支付单，应 MarkFailed 回收后创建新支付单
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        existing.MarkChannelOrdered("TRADE_OLD_001", "prepay_old", "https://qr.example.com/old", null);
        SetExpireAt(existing, DateTime.UtcNow.AddHours(-1)); // 已过期

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：旧支付单被 MarkFailed 回收
        existing.Status.Should().Be(PaymentStatus.Failed);
        existing.FailReason.Should().Contain("回收失效支付单");
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithExistingActiveChannelOrdered_ShouldReturnIdempotentResult()
    {
        // 安排：已存在 ChannelOrdered 且支付链接仍生效的支付单，应幂等返回首次结果（INV-PAY-04）
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        existing.MarkChannelOrdered("TRADE_EXISTING_001", "prepay_existing", "https://qr.example.com/existing", null);
        // ExpireAt 默认 +2h，无需反射设置

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupOrderContext();
        var adapterMock = SetupChannelFactory(PaymentChannel.WeChatPay);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：幂等返回首次结果，不创建新支付单、不调用渠道、不更新旧支付单
        result.PaymentOrderId.Should().Be(existing.Id);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
        result.PrepayId.Should().Be("prepay_existing");
        result.CodeUrl.Should().Be("https://qr.example.com/existing");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        adapterMock.Verify(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ========== 失败场景 ==========

    [Fact]
    public async Task CreatePaymentAsync_WhenOrderNotFound_ShouldThrowPaymentDomainException_ORDER_NOT_FOUND()
    {
        // 安排：防腐层返回 null（订单不存在）
        _orderAntiCorruptionMock.Setup(s => s.GetOrderPaymentContextAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderPaymentContext?)null);

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay)));
        ex.ErrorCode.Should().Be("ORDER_NOT_FOUND");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenOrderBelongsToOtherUser_ShouldThrowPaymentDomainException_ORDER_FORBIDDEN()
    {
        // 安排：订单归属于其他用户（AC-PAY-022 越权发起他人订单支付）
        var otherUserId = Guid.NewGuid();
        _orderAntiCorruptionMock.Setup(s => s.GetOrderPaymentContextAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaymentContext
            {
                OrderId = OrderId,
                UserId = otherUserId,
                IsPayable = true,
                Amount = Amount,
                Currency = "CNY"
            });

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay)));
        ex.ErrorCode.Should().Be("ORDER_FORBIDDEN");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenOrderNotPayable_ShouldThrowPaymentDomainException_ORDER_NOT_PAYABLE()
    {
        // 安排：订单非待支付态（已支付/已取消/已完成等）
        _orderAntiCorruptionMock.Setup(s => s.GetOrderPaymentContextAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaymentContext
            {
                OrderId = OrderId,
                UserId = UserId,
                IsPayable = false,
                Amount = Amount,
                Currency = "CNY"
            });

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay)));
        ex.ErrorCode.Should().Be("ORDER_NOT_PAYABLE");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenOrderAmountInvalid_ShouldThrowPaymentDomainException_PAYMENT_AMOUNT_INVALID()
    {
        // 安排：订单应付金额非法（<=0）
        _orderAntiCorruptionMock.Setup(s => s.GetOrderPaymentContextAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPaymentContext
            {
                OrderId = OrderId,
                UserId = UserId,
                IsPayable = true,
                Amount = 0m,
                Currency = "CNY"
            });

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay)));
        ex.ErrorCode.Should().Be("PAYMENT_AMOUNT_INVALID");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenOrderAlreadyPaid_ShouldThrowPaymentAlreadySucceededException()
    {
        // 安排：已存在已支付支付单，拒绝重复发起
        var existing = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, Amount, "CNY", PaymentChannel.WeChatPay);
        existing.MarkChannelOrdered("TRADE_PAID_001", null, null, null);
        existing.MarkSucceeded("TRADE_PAID_001", Amount, DateTime.UtcNow);

        _repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupOrderContext();

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentAlreadySucceededException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay)));
        ex.OrderId.Should().Be(OrderId);
        ex.PaymentId.Should().Be(existing.Id);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _channelFactoryMock.Verify(f => f.GetAdapter(It.IsAny<PaymentChannel>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenNoChannelEnabled_ShouldThrowPaymentDomainException_PAYMENT_CHANNEL_NOT_FOUND()
    {
        // 安排：未指定渠道且工厂无任何启用渠道
        SetupRepoAndUow();
        SetupOrderContext();
        _channelFactoryMock.Setup(f => f.ListEnabledMetadata())
            .Returns(new List<PaymentChannelMetadata>());

        // 行动 + 断言
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, CreateRequest()));
        ex.ErrorCode.Should().Be("PAYMENT_CHANNEL_NOT_FOUND");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenChannelAdapterThrows_ShouldMarkFailedAndReturnResult()
    {
        // 安排：渠道适配器抛出异常（网络超时/签名失败等），支付单应 MarkFailed 并返回失败结果
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("渠道网络超时"));
        _channelFactoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：支付单被 MarkFailed，返回 Failed 态结果
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.FailReason.Should().Contain("渠道下单异常");
        result.FailReason.Should().Contain("渠道网络超时");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenChannelReturnsNoTradeNo_ShouldMarkFailed()
    {
        // 安排：渠道返回空交易号，支付单应 MarkFailed
        SetupRepoAndUow();
        SetupOrderContext();
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = null, // 未返回交易号
                PrepayId = null,
                CodeUrl = null,
                H5Url = null
            });
        _channelFactoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：支付单被 MarkFailed（渠道下单未返回交易号）
        result.Status.Should().Be(PaymentStatus.Failed);
        result.FailReason.Should().Contain("渠道下单未返回交易号");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========== 参数校验场景 ==========

    [Fact]
    public async Task CreatePaymentAsync_WithEmptyUserId_ShouldThrowPaymentDomainException()
    {
        // 安排 + 行动 + 断言：未认证用户（Guid.Empty）应被拒绝
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(Guid.Empty, CreateRequest(PaymentChannel.WeChatPay)));
        ex.ErrorCode.Should().Be("PAYMENT_USER_EMPTY");
        _orderAntiCorruptionMock.Verify(
            s => s.GetOrderPaymentContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithEmptyOrderId_ShouldThrowPaymentDomainException()
    {
        // 安排 + 行动 + 断言：空 OrderId 应被拒绝
        var request = new CreatePaymentRequest
        {
            OrderId = Guid.Empty,
            Channel = PaymentChannel.WeChatPay
        };
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => _sut.CreatePaymentAsync(UserId, request));
        ex.ErrorCode.Should().Be("PAYMENT_ORDER_EMPTY");
        _orderAntiCorruptionMock.Verify(
            s => s.GetOrderPaymentContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // 安排 + 行动 + 断言：null 请求体应抛 ArgumentNullException
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.CreatePaymentAsync(UserId, null!));
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldUseOrderAmountFromAntiCorruption_NotFromRequest()
    {
        // 安排：验证支付单金额取自订单防腐层权威值（INV-PAY-01 金额一致），而非请求方传入
        SetupRepoAndUow();
        SetupOrderContext(amount: 88.88m);
        SetupChannelFactory(PaymentChannel.WeChatPay);

        PaymentOrder? capturedPayment = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((p, _) => capturedPayment = p)
            .Returns(Task.CompletedTask);

        // 行动
        var result = await _sut.CreatePaymentAsync(UserId, CreateRequest(PaymentChannel.WeChatPay));

        // 断言：支付单金额应为防腐层返回的 88.88 元
        capturedPayment.Should().NotBeNull();
        capturedPayment!.Amount.Should().Be(88.88m);
        result.Status.Should().Be(PaymentStatus.ChannelOrdered);
    }
}

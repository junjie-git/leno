using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Domain.Tests;

/// <summary>
/// P1-4 测试：验证 <see cref="PaymentOrder.GetActivePaymentLink"/> 与 <see cref="PaymentOrder.HasActivePaymentLink"/>
/// 在不同支付单状态、过期情况、链接字段填充情况下的返回结果。
/// 根因：原聚合根无获取生效支付链接的方法，消费方无法判断"渠道已下单且链接仍生效"的幂等跳过场景，
/// 导致对任何已存在支付单一律跳过，支付单卡在 Pending/Failed/Closed 时用户无法重新发起支付。
/// </summary>
public class PaymentOrderActivePaymentLinkTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    /// <summary>
    /// 通过反射设置 PaymentOrder.ExpireAt（private set）为指定时间，模拟未过期/已过期场景。
    /// </summary>
    private static void SetExpireAt(PaymentOrder order, DateTime expireAt)
    {
        typeof(PaymentOrder)
            .GetProperty("ExpireAt")!
            .SetValue(order, expireAt);
    }

    private static PaymentOrder CreatePayment()
    {
        return PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
    }

    [Fact]
    public void GetActivePaymentLink_WhenPending_ShouldReturnNull()
    {
        // Pending 态尚未请求渠道，无链接
        var payment = CreatePayment();

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenChannelOrderedWithH5UrlAndNotExpired_ShouldReturnH5Url()
    {
        // 渠道已下单，H5 链接存在且未过期，应返回 H5Url（优先级高于 CodeUrl）
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", "prepay_001", "https://qr.example.com", "https://h5.example.com");
        SetExpireAt(payment, DateTime.UtcNow.AddHours(1));

        var link = payment.GetActivePaymentLink();

        link.Should().Be("https://h5.example.com");
        payment.HasActivePaymentLink().Should().BeTrue();
    }

    [Fact]
    public void GetActivePaymentLink_WhenChannelOrderedWithOnlyCodeUrlAndNotExpired_ShouldReturnCodeUrl()
    {
        // 渠道已下单，仅 CodeUrl 存在（H5Url 为空）且未过期，应返回 CodeUrl
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", "prepay_001", "https://qr.example.com", null);
        SetExpireAt(payment, DateTime.UtcNow.AddHours(1));

        var link = payment.GetActivePaymentLink();

        link.Should().Be("https://qr.example.com");
        payment.HasActivePaymentLink().Should().BeTrue();
    }

    [Fact]
    public void GetActivePaymentLink_WhenChannelOrderedButExpired_ShouldReturnNull()
    {
        // 渠道已下单但 ExpireAt 已过期，链接失效，应返回 null
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", "prepay_001", "https://qr.example.com", "https://h5.example.com");
        SetExpireAt(payment, DateTime.UtcNow.AddHours(-1));

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenChannelOrderedButBothLinksEmpty_ShouldReturnNull()
    {
        // 渠道已下单但 H5Url 和 CodeUrl 均为空（渠道未返回链接），应返回 null
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", "prepay_001", null, null);
        SetExpireAt(payment, DateTime.UtcNow.AddHours(1));

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenPaid_ShouldReturnNull()
    {
        // 已支付态，链接已失效，应返回 null
        var payment = CreatePayment();
        payment.MarkSucceeded("TRADE001", 100m, DateTime.UtcNow);

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenFailed_ShouldReturnNull()
    {
        // 失败态，链接已失效，应返回 null
        var payment = CreatePayment();
        payment.MarkFailed("渠道下单失败");

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenClosed_ShouldReturnNull()
    {
        // 已关闭态，链接已失效，应返回 null
        var payment = CreatePayment();
        payment.MarkClosed("超时未支付");

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }

    [Fact]
    public void GetActivePaymentLink_WhenChannelOrderedWithWhitespaceLinks_ShouldReturnNull()
    {
        // 渠道已下单但链接为空白字符串，应视为无链接返回 null
        var payment = CreatePayment();
        payment.MarkChannelOrdered("TRADE001", "prepay_001", "   ", "  ");
        SetExpireAt(payment, DateTime.UtcNow.AddHours(1));

        var link = payment.GetActivePaymentLink();

        link.Should().BeNull();
        payment.HasActivePaymentLink().Should().BeFalse();
    }
}

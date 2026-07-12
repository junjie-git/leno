namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付场景枚举，用于区分不同的支付方式，映射到支付宝不同的 API 方法。
/// </summary>
public enum PaymentScene
{
    /// <summary>扫码支付（当面付），对应 alipay.trade.precreate。</summary>
    QrCode = 0,

    /// <summary>PC 网页支付，对应 alipay.trade.page.pay。</summary>
    Page = 1,

    /// <summary>手机网页支付，对应 alipay.trade.wap.pay。</summary>
    Wap = 2,

    /// <summary>App 支付，对应 alipay.trade.app.pay。</summary>
    App = 3
}
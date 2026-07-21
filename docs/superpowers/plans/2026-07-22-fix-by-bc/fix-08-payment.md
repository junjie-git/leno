# Payment（支付域）修复实施计划

## 元数据

- **审计报告**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md]
- **摘要 F 章优先级矩阵**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md]
- **架构评估 G4/G5 技术债象限**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md]
- **问题总数**：🔴 6 / 🟡 9 / 🟢 5（合计 20 项）
- **已修复（跳过）**：5 项
- **本计划覆盖**：20 项（6 P0 + 9 P1 + 5 P2）

## 架构与技术栈

- **限界上下文**：Payment BC（支付域），DDD 四层架构（Domain / Application / Infrastructure / Api）
- **运行时**：.NET 10 + EF Core + MassTransit + RabbitMQ + Redis + gRPC
- **渠道**：微信支付 V3（RSA-SHA256 签名、AES-GCM 解密、APIv3 Key、平台公钥验签）、支付宝 RSA2（私钥签名、公钥验签）
- **聚合根**：PaymentOrder、RefundOrder、PaymentChannelConfig、ReconciliationDiff
- **模式**：Outbox（IntegrationEventMapper）、Redis SET NX 幂等、乐观并发（RowVersion）、gRPC 双轨（HTTP fallback）
- **测试框架**：xUnit + Moq + FluentAssertions

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 | 微信支付通知 ParseXml 在验签前解析未授信报文 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L55] | P0 | 待修复 |
| 2 | 🔴 | 微信 V3 回调验签误用 ApiKey 作为平台公钥 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L158-L159] | P0 | 待修复 |
| 3 | 🔴 | 支付宝回调验签误用 ApiKey（私钥）作为公钥 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs#L165] | P0 | 待修复 |
| 4 | 🔴 | PaymentsController 买家端三个接口缺失用户归属校验（IDOR） | [file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L42-L66] | P0 | 待修复 |
| 5 | 🔴 | 微信 V3 回调 ChannelNotifyResult 缺失 OutTradeNo 字段 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L200-L248] | P0 | 待修复 |
| 6 | 🔴 | PaymentRequestedEventConsumer 先调渠道下单再保存支付单，渠道成功但保存失败时丢单 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L58-L79] | P0 | 待修复 |
| 7 | 🟡 | ReconciliationService 下次对账时间计算逻辑错误 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L48-L49] | P1 | 待修复 |
| 8 | 🟡 | 对账查询按 CreatedAt 过滤而非 PaidAt，跨日支付漏对账 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L157-L160] | P1 | 待修复 |
| 9 | 🟡 | PaymentChannelConfig.Description 公共 setter 绕过聚合封装 | [file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentChannelConfig.cs#L28] | P1 | 待修复 |
| 10 | 🟡 | RefundRequestedEventConsumer 未校验原支付单状态为 Paid | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Consumers/RefundRequestedEventConsumer.cs#L60-L64] | P1 | 待修复 |
| 11 | 🟡 | PaymentStatusCheckJob 未检查 ExpireAt 超时关单 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L41-L63] | P1 | 待修复 |
| 12 | 🟡 | PaymentGrpcService 返回 AmountCents=0 / PaidAt=空，DTO 字段缺失 | [file:///workspace/src/Services/Payment/Leno.Payment.Api/GrpcServices/PaymentGrpcService.cs#L48-L63] | P1 | 待修复 |
| 13 | 🟡 | PaymentOrder/RefundOrder EF 配置缺失 RowVersion 乐观并发标记 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/PaymentOrderConfiguration.cs#L12-L42] | P1 | 待修复 |
| 14 | 🟡 | ReconciliationDiffConfiguration 表名 PascalCase 且枚举 HasConversion 不一致 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/ReconciliationDiffConfiguration.cs#L14] | P1 | 待修复 |
| 15 | 🟡 | AlipayNotifyHandler 退款通知误用 trade_no 作为渠道退款单号 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs#L163] | P1 | 待修复 |
| 16 | 🟢 | PaymentOrder/RefundOrder OutTradeNo/OutRefundNo 生成用时间戳+随机数，高并发碰撞风险 | [file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L101] | P2 | 待修复 |
| 17 | 🟢 | NotifyController StreamReader 未 using，依赖 GC 回收 | [file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L51] | P2 | 待修复 |
| 18 | 🟢 | InternalPaymentsController 双路由标注 [Obsolete] 但未下线 | [file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs#L24-L25] | P2 | 待修复 |
| 19 | 🟢 | WeChatPayAdapter tradeType 硬编码 NATIVE，不支持 H5/JSAPI | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L43] | P2 | 待修复 |
| 20 | 🟢 | PaymentStatusCheckJob BatchSize 硬编码 100，不可配置 | [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L17-L18] | P2 | 待修复 |

## 统计概览

| 严重度 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|--------|------|---------------|--------------------------|--------|
| 🔴 P0 | 6 | 0 | 0 | 6 |
| 🟡 P1 | 9 | 0 | 0 | 9 |
| 🟢 P2 | 5 | 0 | 0 | 5 |
| **合计** | **20** | **0** | **0** | **20** |

> **注**：审计报告外另有 5 项已修复问题（T1/T2/T19 系列），列入下方 [ALREADY-FIXED] 章节跳过处理，不计入上表 20 项审计问题。

---

## [ALREADY-FIXED] 已修复问题（跳过）

以下 5 项问题在前期修复中已完成，经代码校验确认修复到位，本计划不再重复处理。

### AF-1：T1 微信防重放 Nonce fail-closed

- **修复位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayChannel.cs#L149-L171]
- **修复内容**：`ValidateNonceAsync` 使用 Redis `StringSetAsync` + `When.NotExists` 实现原子防重放；Redis 故障时抛出异常（fail-closed），不降级放行。
- **验证**：已由 `NotifyHandlerRedisFailoverTests` 覆盖。

### AF-2：T2 支付宝验签异常分类

- **修复位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipaySignatureHelper.cs#L42-L84]
- **修复内容**：`VerifySign` 分别捕获 `ArgumentException`、`FormatException`、`CryptographicException`，不再吞掉非预期异常。
- **验证**：已由 `NotifyHandlerRedisFailoverTests` 中 Alipay 验签路径覆盖。

### AF-3：T1 支付回调金额强校验

- **修复位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L146-L171]
- **修复内容**：`MarkSucceeded(string channelTradeNo, decimal amount, DateTime paidAt)` 校验 `amount == Amount`，不一致抛出 `PaymentDomainException`。
- **验证**：已由 `NotifyHandlerRedisFailoverTests` 中金额一致/不一致路径覆盖。

### AF-4：T2 主动查询补偿金额校验

- **修复位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Application/Services/PaymentAppService.cs#L70-L87]
- **修复内容**：`QueryPaymentStatusAsync` 检查 `result.Amount.Value != payment.Amount`，不一致时记录告警并进入人工对账队列，不调用 `MarkSucceeded`。
- **验证**：已由 `PaymentStatusCheckJob` 中相同校验逻辑交叉验证。

### AF-5：T19 支付回调 Redis 故障 fail-closed

- **修复位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L186-L206] 与 [file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs#L184-L204]
- **修复内容**：`MarkCallbackProcessedAsync` 在 Redis 故障时抛出异常，由外层 `HandleAsync` catch 返回 `FAIL`/`fail` 让渠道重试；Redis 为 null（开发环境）时保留放行语义。
- **验证**：已由 `NotifyHandlerRedisFailoverTests` 中 `Alipay_RedisFailure_ShouldReturnFailAndNotMarkPaid` 与 `Alipay_RedisNull_ShouldProceedAndMarkPaid` 覆盖。

---

## P0 详细修复计划（TDD 5 步：测试→验证失败→实现→验证通过→提交）

### P0-1 修复微信支付通知 ParseXml 在验签前解析未授信报文

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L55]

**根因**：`HandleAsync` 方法在第 55 行无条件调用 `var fields = ParseXml(rawBody)`，此时尚未执行 `adapter.VerifyNotifyAsync`（第 56 行）。微信 V3 回调报文为 JSON 格式而非 XML，`ParseXml` 对 JSON 输入会抛出 `XmlException`，被外层 catch 捕获后返回 `"FAIL"`，导致所有 V3 回调永远无法处理。此外，对未验签报文执行 XML 解析存在 XXE 等安全风险。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Infrastructure.Tests/Notify/WeChatPayNotifyHandlerParseXmlTests.cs`

```csharp
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Notify;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Notify;

/// <summary>
/// P0-1 测试：验证 WeChatPayNotifyHandler 不再在验签前调用 ParseXml。
/// 微信 V3 回调为 JSON 格式，ParseXml(XML 解析) 在验签前执行会导致 JSON 报文抛 XmlException，
/// 被外层 catch 吞掉返回 FAIL，所有 V3 回调无法处理。
/// 修复后：ParseXml 不再被调用，验签失败直接返回 FAIL，验签成功后使用 ChannelNotifyResult 字段。
/// </summary>
public class WeChatPayNotifyHandlerParseXmlTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string OutTradeNo = "PAY20260722000001";
    private const string ChannelTradeNo = "4200000000202607220000000001";

    /// <summary>
    /// 构造 handler，使用 Mock 的 IPaymentChannelAdapter 替代真实 WeChatPayAdapter。
    /// 修复前提：WeChatPayNotifyHandler 构造函数需改为接收 IPaymentChannelAdapter。
    /// </summary>
    private static WeChatPayNotifyHandler CreateHandler(
        Mock<IPaymentChannelAdapter> adapterMock,
        Mock<IPaymentOrderRepository>? orderRepoMock = null,
        Mock<IRefundOrderRepository>? refundRepoMock = null,
        Mock<IUnitOfWork>? uowMock = null,
        IConnectionMultiplexer? redis = null)
    {
        orderRepoMock ??= new Mock<IPaymentOrderRepository>();
        refundRepoMock ??= new Mock<IRefundOrderRepository>();
        uowMock ??= new Mock<IUnitOfWork>();

        return new WeChatPayNotifyHandler(
            adapterMock.Object,
            orderRepoMock.Object,
            refundRepoMock.Object,
            uowMock.Object,
            redis,
            NullLogger<WeChatPayNotifyHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_VerifyFailed_ShouldReturnFail_WithoutThrowingXmlException()
    {
        // Arrange：V3 JSON 报文（非 XML），验签返回 false
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\",\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "invalid_sig",
            ["Wechatpay-Serial"] = "serial_001"
        };

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock
            .Setup(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelNotifyResult { Verified = false });

        var sut = CreateHandler(adapterMock);

        // Act：验签失败应直接返回 FAIL，不应因 ParseXml 抛 XmlException
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("FAIL", result);
        adapterMock.Verify(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_VerifySucceeded_V3Json_ShouldProcessSuccessfully()
    {
        // Arrange：V3 JSON 报文，验签通过，ChannelNotifyResult 含 OutTradeNo 等字段
        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\",\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1234567890",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "valid_sig",
            ["Wechatpay-Serial"] = "serial_001"
        };

        var paidAt = DateTime.UtcNow;
        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock
            .Setup(a => a.VerifyNotifyAsync(rawBody, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelNotifyResult
            {
                Verified = true,
                OutTradeNo = OutTradeNo,
                ChannelTradeNo = ChannelTradeNo,
                IsPaid = true,
                Amount = 100m,
                PaidAt = paidAt
            });

        var order = PaymentOrder.Create(Guid.NewGuid(), OrderId, UserId, 100m, "CNY", PaymentChannel.WeChatPay);
        var orderRepoMock = new Mock<IPaymentOrderRepository>();
        orderRepoMock
            .Setup(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Redis null = 开发环境放行
        var sut = CreateHandler(adapterMock, orderRepoMock, redis: null);

        // Act：验签通过后应使用 ChannelNotifyResult 字段处理，不依赖 ParseXml
        var result = await sut.HandleAsync(rawBody, headers);

        // Assert
        Assert.Equal("SUCCESS", result);
        Assert.Equal(PaymentStatus.Paid, order.Status);
        Assert.Equal(ChannelTradeNo, order.ChannelTradeNo);
        orderRepoMock.Verify(r => r.GetByOutTradeNoAsync(OutTradeNo, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayNotifyHandlerParseXmlTests"
```

预期：编译失败。`WeChatPayNotifyHandler` 构造函数接收 `WeChatPayAdapter` 具体类而非 `IPaymentChannelAdapter` 接口，Mock 无法注入；`ChannelNotifyResult` 无 `OutTradeNo` 字段（P0-5 修复前）。

**步骤 3：实现修复**

3.1 修改 `WeChatPayNotifyHandler.cs` 构造函数，将 `WeChatPayAdapter` 改为 `IPaymentChannelAdapter`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs
// 修改第 19 行字段声明
private readonly IPaymentChannelAdapter _adapter;

// 修改第 26-33 行构造函数
public WeChatPayNotifyHandler(
    IPaymentChannelAdapter adapter,
    IPaymentOrderRepository paymentOrderRepository,
    IRefundOrderRepository refundOrderRepository,
    IUnitOfWork unitOfWork,
    IConnectionMultiplexer? redis = null,
    ILogger<WeChatPayNotifyHandler>? logger = null)
{
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
    _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
    _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    _redis = redis;
    _logger = logger ?? InternalNullLoggerFactory.CreateLogger<WeChatPayNotifyHandler>();
}
```

3.2 修改 `HandleAsync` 方法，删除第 55 行 `ParseXml` 调用，验签通过后使用 `ChannelNotifyResult` 字段：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs
// 替换第 48-93 行 HandleAsync 方法体
public async Task<string> HandleAsync(string rawBody, Dictionary<string, string> headers)
{
    try
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(headers);

        // 先验签，验签失败直接返回 FAIL，不解析未授信报文
        var result = await _adapter.VerifyNotifyAsync(rawBody, headers);

        if (!result.Verified)
        {
            _logger.LogWarning("微信支付通知验签失败 ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
            return "FAIL";
        }

        // 回调幂等：使用 Redis 记录已处理的渠道交易号
        var channelTradeNo = result.ChannelTradeNo;
        if (!string.IsNullOrEmpty(channelTradeNo))
        {
            if (!await MarkCallbackProcessedAsync(channelTradeNo))
            {
                _logger.LogInformation("微信支付通知：回调已处理，幂等跳过 ChannelTradeNo={ChannelTradeNo}", channelTradeNo);
                return "SUCCESS";
            }
        }

        if (result.IsPaid)
        {
            return await HandlePaymentNotifyAsync(result);
        }

        if (result.IsRefund)
        {
            return await HandleRefundNotifyAsync(result);
        }

        _logger.LogInformation("微信支付通知：非支付/退款通知，忽略");
        return "SUCCESS";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "微信支付通知处理异常");
        return "FAIL";
    }
}
```

3.3 修改 `HandlePaymentNotifyAsync` 方法，接收 `ChannelNotifyResult` 而非 `Dictionary<string, string>`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs
// 替换第 95-119 行 HandlePaymentNotifyAsync 方法
private async Task<string> HandlePaymentNotifyAsync(ChannelNotifyResult result)
{
    var outTradeNo = result.OutTradeNo;
    if (string.IsNullOrEmpty(outTradeNo))
    {
        _logger.LogWarning("微信支付通知：OutTradeNo 为空 ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
        return "FAIL";
    }

    var order = await _paymentOrderRepository.GetByOutTradeNoAsync(outTradeNo);
    if (order is null)
    {
        _logger.LogWarning("微信支付通知：支付单不存在 OutTradeNo={OutTradeNo}", outTradeNo);
        return "FAIL";
    }

    if (order.Status == PaymentStatus.Paid)
    {
        _logger.LogInformation("微信支付通知：支付单已支付，幂等跳过 OutTradeNo={OutTradeNo}", outTradeNo);
        return "SUCCESS";
    }

    if (order.Status != PaymentStatus.Pending && order.Status != PaymentStatus.ChannelOrdered)
    {
        _logger.LogInformation("微信支付通知：支付单状态 {Status} 不可标记成功，跳过 OutTradeNo={OutTradeNo}",
            order.Status, outTradeNo);
        return "SUCCESS";
    }

    var tradeNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : order.ChannelTradeNo;
    if (string.IsNullOrEmpty(tradeNo))
    {
        _logger.LogWarning("微信支付通知：缺少第三方交易号 OutTradeNo={OutTradeNo}", outTradeNo);
        return "FAIL";
    }

    var amount = result.Amount ?? order.Amount;
    var paidAt = result.PaidAt ?? DateTime.UtcNow;
    order.MarkSucceeded(tradeNo, amount, paidAt);
    await _paymentOrderRepository.UpdateAsync(order);
    await _unitOfWork.SaveEntitiesAsync();

    _logger.LogInformation("微信支付通知：支付单已标记成功 OutTradeNo={OutTradeNo} PaymentId={PaymentId}",
        outTradeNo, order.Id);
    return "SUCCESS";
}
```

3.4 修改 `HandleRefundNotifyAsync` 方法，接收 `ChannelNotifyResult`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs
// 替换 HandleRefundNotifyAsync 方法，改为接收 ChannelNotifyResult
private async Task<string> HandleRefundNotifyAsync(ChannelNotifyResult result)
{
    var outRefundNo = result.OutTradeNo;
    if (string.IsNullOrEmpty(outRefundNo))
    {
        _logger.LogWarning("微信支付通知：退款通知缺少 OutRefundNo ChannelTradeNo={ChannelTradeNo}", result.ChannelTradeNo);
        return "FAIL";
    }

    var refund = await _refundOrderRepository.GetByOutRefundNoAsync(outRefundNo);
    if (refund is null)
    {
        _logger.LogWarning("微信支付通知：退款单不存在 OutRefundNo={OutRefundNo}", outRefundNo);
        return "FAIL";
    }

    if (refund.Status == RefundStatus.Succeeded)
    {
        _logger.LogInformation("微信支付通知：退款单已成功，幂等跳过 OutRefundNo={OutRefundNo}", outRefundNo);
        return "SUCCESS";
    }

    if (refund.Status != RefundStatus.Refunding)
    {
        _logger.LogInformation("微信支付通知：退款单状态 {Status} 不可标记成功，跳过 OutRefundNo={OutRefundNo}",
            refund.Status, outRefundNo);
        return "SUCCESS";
    }

    var channelRefundNo = !string.IsNullOrEmpty(result.ChannelTradeNo) ? result.ChannelTradeNo : refund.OutRefundNo;
    refund.MarkSucceeded(channelRefundNo, DateTime.UtcNow);
    await _refundOrderRepository.UpdateAsync(refund);
    await _unitOfWork.SaveEntitiesAsync();

    _logger.LogInformation("微信支付通知：退款单已标记成功 OutRefundNo={OutRefundNo} RefundId={RefundId}",
        outRefundNo, refund.Id);
    return "SUCCESS";
}
```

3.5 在 DI 注册处更新 `WeChatPayNotifyHandler` 的注入（将 `WeChatPayAdapter` 注册为 `IPaymentChannelAdapter` 的实现之一，或在注册 Handler 时传入 `WeChatPayAdapter` 实例——因 `WeChatPayAdapter` 已实现 `IPaymentChannelAdapter`，DI 容器可直接注入）。

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayNotifyHandlerParseXmlTests"
```

预期：2 个测试全部通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs \
        src/Services/Payment/Leno.Payment.Infrastructure.Tests/Notify/WeChatPayNotifyHandlerParseXmlTests.cs
git commit -m "fix(payment): P0-1 微信通知移除验签前 ParseXml 调用，改用 ChannelNotifyResult 字段"
```

---

### P0-2 修复微信 V3 回调验签误用 ApiKey 作为平台公钥

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L158-L159]、[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs#L36]

**根因**：`WeChatPayAdapter.VerifyNotifyAsync` 第 158-159 行调用 `WeChatPayV3SignatureHelper.VerifyNotifySign(timestamp, nonce, rawBody, signature, config.ApiKey)`，将 `ApiKey`（APIv3 密钥，32 字节对称密钥）作为 `publicKey` 参数传入。`VerifyNotifySign` 内部执行 `rsa.ImportFromPem(publicKey)`，ApiKey 不是合法 PEM 公钥，`ImportFromPem` 抛出异常被 catch 吞掉返回 `false`，导致所有 V3 回调验签永远失败。微信 V3 回调验签应使用**微信支付平台公钥**（RSA 公钥 PEM），而非 APIv3 对称密钥。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/WeChatPayAdapterPublicKeyTests.cs`

```csharp
using System.Security.Cryptography;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-2 测试：验证 WeChatPayAdapter.VerifyNotifyAsync 使用 PlatformPublicKey 而非 ApiKey 验签。
/// </summary>
public class WeChatPayAdapterPublicKeyTests
{
    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static string BuildCallbackBody()
    {
        return "{\"id\":\"evt-001\",\"create_time\":\"2026-07-22T10:00:00+08:00\","
            + "\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"abc\",\"nonce\":\"def\",\"associated_data\":\"\"}}";
    }

    private static WeChatPayAdapter CreateAdapter(ChannelConfig config)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<WeChatPayClient>.Instance;
        var options = Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = "test_v3_key_32chars_long_1234567890",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = NullLogger<WeChatPayAdapter>.Instance;

        return new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldUsePlatformPublicKey_NotApiKey()
    {
        // Arrange：生成 RSA 密钥对，用私钥签名模拟微信平台签名
        var (platformPrivateKey, platformPublicKey) = GenerateKeyPair();
        var apiV3Key = "test_v3_key_32chars_long_1234567890";

        var body = BuildCallbackBody();
        var timestamp = "1753166400";
        var nonce = "nonce123";

        // 用平台私钥生成正确签名
        var message = $"{timestamp}\n{nonce}\n{body}\n";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(platformPrivateKey);
        var signatureBytes = rsa.SignData(
            System.Text.Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var config = new ChannelConfig
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiKey = apiV3Key,
            PlatformPublicKey = platformPublicKey,
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(body, headers);

        // Assert：使用正确的平台公钥验签应通过
        Assert.True(result.Verified);
    }

    [Fact]
    public async Task VerifyNotifyAsync_WithApiKeyAsPublicKey_ShouldFailVerification()
    {
        // Arrange：ApiKey 不是合法 PEM 公钥，验签应失败
        var apiV3Key = "test_v3_key_32chars_long_1234567890";
        var body = BuildCallbackBody();
        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = "1753166400",
            ["Wechatpay-Nonce"] = "nonce123",
            ["Wechatpay-Signature"] = "invalid_sig",
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var config = new ChannelConfig
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiKey = apiV3Key,
            // 不设置 PlatformPublicKey，模拟旧配置
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(body, headers);

        // Assert：PlatformPublicKey 为空时验签应失败（不应回退到 ApiKey）
        Assert.False(result.Verified);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayAdapterPublicKeyTests"
```

预期：编译失败。`ChannelConfig` 无 `PlatformPublicKey` 字段。

**步骤 3：实现修复**

3.1 修改 `ChannelConfig`，新增 `PlatformPublicKey` 字段：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Domain/Services/IChannelConfigProvider.cs
// 修改第 10-18 行 ChannelConfig 类
public sealed class ChannelConfig
{
    public string AppId { get; set; } = string.Empty;
    public string MchId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>微信支付平台公钥（PEM 格式），用于 V3 回调验签。支付宝渠道此字段为 null。</summary>
    public string? PlatformPublicKey { get; set; }

    /// <summary>支付宝公钥（PEM 格式），用于回调验签。微信渠道此字段为 null。</summary>
    public string? PublicKey { get; set; }

    public string? CertPath { get; set; }
    public string NotifyUrl { get; set; } = string.Empty;
    public string RefundNotifyUrl { get; set; } = string.Empty;
}
```

3.2 修改 `ChannelOption`，新增 `PlatformPublicKey` 和 `PublicKey` 字段：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Config/PaymentChannelOptions.cs
// 修改第 18-37 行 ChannelOption 类
public sealed class ChannelOption
{
    public string AppId { get; set; } = string.Empty;
    public string MchId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>微信支付平台公钥（PEM 格式），V3 回调验签用。</summary>
    public string? PlatformPublicKey { get; set; }

    /// <summary>支付宝公钥（PEM 格式），回调验签用。</summary>
    public string? PublicKey { get; set; }

    public string? CertPath { get; set; }
    public string NotifyUrl { get; set; } = string.Empty;
    public string RefundNotifyUrl { get; set; } = string.Empty;
}
```

3.3 修改 `ChannelConfigProvider`，映射新字段：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs
// 修改第 32-40 行
var config = new ChannelConfig
{
    AppId = option.AppId,
    MchId = option.MchId,
    ApiKey = option.ApiKey,
    PlatformPublicKey = option.PlatformPublicKey,
    PublicKey = option.PublicKey,
    CertPath = option.CertPath,
    NotifyUrl = option.NotifyUrl,
    RefundNotifyUrl = option.RefundNotifyUrl
};
```

3.4 修改 `WeChatPayAdapter.VerifyNotifyAsync`，使用 `PlatformPublicKey` 替代 `ApiKey`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs
// 修改第 157-159 行
// V3 回调签名验证：使用微信支付平台公钥（RSA-SHA256），而非 APIv3 对称密钥
var verified = WeChatPay.WeChatPayV3SignatureHelper.VerifyNotifySign(
    timestamp ?? string.Empty, nonce ?? string.Empty, rawBody, signature ?? string.Empty,
    config.PlatformPublicKey ?? string.Empty);
```

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayAdapterPublicKeyTests"
```

预期：2 个测试全部通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Domain/Services/IChannelConfigProvider.cs \
        src/Services/Payment/Leno.Payment.Infrastructure/Config/PaymentChannelOptions.cs \
        src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs \
        src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs \
        src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/WeChatPayAdapterPublicKeyTests.cs
git commit -m "fix(payment): P0-2 微信 V3 回调验签改用 PlatformPublicKey 替代 ApiKey"
```

---

### P0-3 修复支付宝回调验签误用 ApiKey（私钥）作为公钥

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs#L165]、[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs#L36]

**根因**：`AlipayAdapter.VerifyNotifyAsync` 第 165 行调用 `AlipaySignatureHelper.VerifySign(dict, config.ApiKey, sign)`，将 `ApiKey`（支付宝 RSA 私钥）作为公钥传入验签方法。RSA2 验签应使用**支付宝公钥**验证签名，而非商户私钥。用私钥验签会导致验签逻辑永远失败或产生不可预期的结果。`ChannelConfigProvider` 第 36 行 `ApiKey = option.ApiKey` 直接将私钥映射到 `ApiKey` 字段，未区分公钥与私钥。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/AlipayAdapterPublicKeyTests.cs`

```csharp
using System.Security.Cryptography;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.Alipay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-3 测试：验证 AlipayAdapter.VerifyNotifyAsync 使用 PublicKey 而非 ApiKey（私钥）验签。
/// </summary>
public class AlipayAdapterPublicKeyTests
{
    private static (string privateKey, string publicKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private static AlipayAdapter CreateAdapter(ChannelConfig config)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.Alipay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<AlipayClient>.Instance;
        var client = new AlipayClient(httpClient, clientLogger);
        var adapterLogger = NullLogger<AlipayAdapter>.Instance;

        return new AlipayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    private static Dictionary<string, string> BuildNotifyFields(string privateKey, string totalAmount = "100.00")
    {
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = "2021000000000001",
            ["charset"] = "UTF-8",
            ["out_trade_no"] = "PAY20260722000001",
            ["trade_no"] = "2026071222001000000000000001",
            ["trade_status"] = "TRADE_SUCCESS",
            ["total_amount"] = totalAmount,
            ["gmt_payment"] = "2026-07-22 10:00:00",
            ["notify_time"] = "2026-07-22 10:00:00",
            ["notify_type"] = "trade_status_sync",
            ["notify_id"] = "notify-001",
            ["sign_type"] = "RSA2"
        };
        fields["sign"] = AlipaySignatureHelper.GenerateSign(fields, privateKey);
        return fields;
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldUsePublicKey_NotPrivateKey()
    {
        // Arrange：生成 RSA 密钥对，私钥签名模拟支付宝签名，公钥验签
        var (privateKey, publicKey) = GenerateKeyPair();

        var fields = BuildNotifyFields(privateKey);
        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            MchId = "2088000000000001",
            ApiKey = privateKey,
            PublicKey = publicKey,
            NotifyUrl = "https://example.com/notify/alipay",
            RefundNotifyUrl = "https://example.com/notify/alipay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, fields);

        // Assert：使用正确的公钥验签应通过
        Assert.True(result.Verified);
        Assert.True(result.IsPaid);
    }

    [Fact]
    public async Task VerifyNotifyAsync_WithPrivateKeyAsPublicKey_ShouldFailVerification()
    {
        // Arrange：用私钥作为公钥验签，应失败
        var (privateKey, _) = GenerateKeyPair();

        var fields = BuildNotifyFields(privateKey);
        var rawBody = string.Join("&", fields.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var config = new ChannelConfig
        {
            AppId = "2021000000000001",
            MchId = "2088000000000001",
            ApiKey = privateKey,
            // 不设置 PublicKey，模拟旧配置（回退到 ApiKey 即私钥）
            NotifyUrl = "https://example.com/notify/alipay",
            RefundNotifyUrl = "https://example.com/notify/alipay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, fields);

        // Assert：用私钥验签应失败
        Assert.False(result.Verified);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~AlipayAdapterPublicKeyTests"
```

预期：编译失败。`ChannelConfig` 无 `PublicKey` 字段（与 P0-2 共享修复）；`AlipayAdapter` 第 165 行仍使用 `config.ApiKey`。

**步骤 3：实现修复**

3.1 `ChannelConfig` 与 `ChannelOption` 已在 P0-2 步骤 3.1-3.2 中新增 `PublicKey` 字段。

3.2 `ChannelConfigProvider` 已在 P0-2 步骤 3.3 中映射 `PublicKey = option.PublicKey`。

3.3 修改 `AlipayAdapter.VerifyNotifyAsync`，使用 `PublicKey` 替代 `ApiKey`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs
// 修改第 165 行
var verified = Alipay.AlipaySignatureHelper.VerifySign(dict, config.PublicKey ?? string.Empty, sign);
```

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~AlipayAdapterPublicKeyTests"
```

预期：2 个测试全部通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs \
        src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/AlipayAdapterPublicKeyTests.cs
git commit -m "fix(payment): P0-3 支付宝回调验签改用 PublicKey 替代 ApiKey（私钥）"
```

---

### P0-4 修复 PaymentsController 买家端接口缺失用户归属校验（IDOR）

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L42-L66]

**根因**：`GetPaymentResultAsync`（第 42 行）、`QueryPaymentStatusAsync`（第 52 行）、`GetRefundResultAsync`（第 62 行）三个买家端接口仅校验 `[Authorize(Roles = "Buyer")]`，未校验当前 JWT 用户与支付单/退款单的 `UserId` 归属关系。任意已认证 Buyer 可传入他人的 `orderId`/`paymentId`/`afterSalesId` 查询他人支付/退款记录，构成 IDOR（不安全直接对象引用）。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Api.Tests/Controllers/PaymentsControllerIdorTests.cs`

```csharp
using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Leno.Payment.Api.Tests.Controllers;

/// <summary>
/// P0-4 测试：验证 PaymentsController 买家端三个接口校验用户归属，防止 IDOR。
/// </summary>
public class PaymentsControllerIdorTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid AttackerUserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    private static PaymentsController CreateController(Guid currentUserId)
    {
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContextMock.SetupGet(x => x.UserId).Returns(currentUserId);

        var paymentAppMock = new Mock<IPaymentAppService>();
        var refundAppMock = new Mock<IRefundAppService>();

        // 模拟返回属于 OwnerUserId 的支付/退款数据
        paymentAppMock
            .Setup(s => s.GetPaymentResultAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOrderDto { OrderId = OrderId, UserId = OwnerUserId });
        paymentAppMock
            .Setup(s => s.QueryPaymentStatusAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusDto { PaymentId = PaymentId });
        refundAppMock
            .Setup(s => s.GetRefundResultAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundOrderDto { AfterSalesId = AfterSalesId, UserId = OwnerUserId });

        return new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);
    }

    [Fact]
    public async Task GetPaymentResult_ShouldReturn403_When_UserDoesNotOwnOrder()
    {
        // Arrange：攻击者尝试查询他人的订单支付结果
        var sut = CreateController(AttackerUserId);

        // Act
        var result = await sut.GetPaymentResultAsync(OrderId, CancellationToken.None);

        // Assert：应返回 403 Forbidden，而非 200 OK
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetPaymentResult_ShouldReturn200_When_UserOwnsOrder()
    {
        // Arrange：所有者查询自己的订单支付结果
        var sut = CreateController(OwnerUserId);

        // Act
        var result = await sut.GetPaymentResultAsync(OrderId, CancellationToken.None);

        // Assert：应返回 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task QueryPaymentStatus_ShouldReturn403_When_UserDoesNotOwnPayment()
    {
        // Arrange：攻击者尝试查询他人的支付状态
        var paymentAppMock = new Mock<IPaymentAppService>();
        paymentAppMock
            .Setup(s => s.QueryPaymentStatusAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelStatusDto { PaymentId = PaymentId });

        // 需要应用层返回 UserId 以供控制器校验归属
        // 修复前提：QueryPaymentStatusAsync 返回的 ChannelStatusDto 需包含 UserId 字段
        // 或控制器通过 PaymentId 查询支付单获取 UserId
        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        userContextMock.SetupGet(x => x.UserId).Returns(AttackerUserId);

        var refundAppMock = new Mock<IRefundAppService>();
        var sut = new PaymentsController(userContextMock.Object, paymentAppMock.Object, refundAppMock.Object);

        // Act
        var result = await sut.QueryPaymentStatusAsync(PaymentId, CancellationToken.None);

        // Assert：应返回 403
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetRefundResult_ShouldReturn403_When_UserDoesNotOwnRefund()
    {
        // Arrange：攻击者尝试查询他人的退款结果
        var sut = CreateController(AttackerUserId);

        // Act
        var result = await sut.GetRefundResultAsync(AfterSalesId, CancellationToken.None);

        // Assert：应返回 403
        Assert.IsType<ForbidResult>(result);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Api.Tests --filter "FullyQualifiedName~PaymentsControllerIdorTests"
```

预期：编译失败或测试失败。当前控制器无归属校验，返回 200 OK 而非 403。

**步骤 3：实现修复**

3.1 修改 `IPaymentAppService` 和 `IRefundAppService`，新增带 `userId` 校验的重载方法（或修改现有方法签名增加 `userId` 参数）：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Application/IPaymentAppService.cs
// 在接口中新增归属校验方法
/// <summary>按订单标识查询支付结果，校验用户归属。</summary>
Task<PaymentOrderDto?> GetPaymentResultAsync(Guid orderId, Guid userId, CancellationToken ct = default);

/// <summary>主动查询渠道支付状态，校验用户归属。</summary>
Task<ChannelStatusDto> QueryPaymentStatusAsync(Guid paymentId, Guid userId, CancellationToken ct = default);
```

```csharp
// 文件：src/Services/Payment/Leno.Payment.Application/IRefundAppService.cs
// 在接口中新增归属校验方法
/// <summary>按售后单标识查询退款结果，校验用户归属。</summary>
Task<RefundOrderDto?> GetRefundResultAsync(Guid afterSalesId, Guid userId, CancellationToken ct = default);
```

3.2 修改 `PaymentAppService`，实现归属校验：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Application/Services/PaymentAppService.cs
// 新增带 userId 校验的重载
public async Task<PaymentOrderDto?> GetPaymentResultAsync(Guid orderId, Guid userId, CancellationToken ct = default)
{
    var payment = await _paymentOrderRepository.GetByOrderIdAsync(orderId, ct);
    if (payment is null)
    {
        return null;
    }

    if (payment.UserId != userId)
    {
        throw new UnauthorizedAccessException("无权访问此支付单");
    }

    return ToDto(payment);
}

public async Task<ChannelStatusDto> QueryPaymentStatusAsync(Guid paymentId, Guid userId, CancellationToken ct = default)
{
    var payment = await _paymentOrderRepository.GetByIdAsync(paymentId, ct)
        ?? throw new InvalidOperationException($"支付单不存在 PaymentId={paymentId}");

    if (payment.UserId != userId)
    {
        throw new UnauthorizedAccessException("无权访问此支付单");
    }

    // 以下逻辑与原 QueryPaymentStatusAsync 一致
    if (payment.Status is PaymentStatus.Paid or PaymentStatus.Closed)
    {
        return new ChannelStatusDto
        {
            PaymentId = payment.Id,
            IsPaid = payment.Status == PaymentStatus.Paid,
            ChannelTradeNo = payment.ChannelTradeNo,
            PaidAt = payment.PaidAt
        };
    }

    var result = await _channelStatusQueryService.QueryPaymentStatusAsync(payment.Channel, payment.OutTradeNo, ct);

    if (result.IsPaid && payment.Status != PaymentStatus.Paid)
    {
        if (!result.Amount.HasValue || result.Amount.Value != payment.Amount)
        {
            _logger.LogWarning("主动查询补偿金额不一致，进入人工对账队列 PaymentId={PaymentId} 期望金额={Expected} 实付金额={Actual}",
                payment.Id, payment.Amount, result.Amount);
            return new ChannelStatusDto
            {
                PaymentId = payment.Id,
                IsPaid = false,
                ChannelTradeNo = result.ChannelTradeNo,
                PaidAt = result.PaidAt
            };
        }

        var tradeNo = result.ChannelTradeNo ?? payment.ChannelTradeNo ?? payment.OutTradeNo;
        payment.MarkSucceeded(tradeNo, result.Amount.Value, result.PaidAt ?? DateTime.UtcNow);
        await _paymentOrderRepository.UpdateAsync(payment, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("主动查询补偿：支付单 {PaymentId} 已标记支付成功", payment.Id);
    }

    return new ChannelStatusDto
    {
        PaymentId = payment.Id,
        IsPaid = result.IsPaid,
        ChannelTradeNo = result.ChannelTradeNo,
        PaidAt = result.PaidAt
    };
}
```

3.3 修改 `RefundAppService`，实现归属校验：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Application/Services/RefundAppService.cs
// 新增带 userId 校验的重载
public async Task<RefundOrderDto?> GetRefundResultAsync(Guid afterSalesId, Guid userId, CancellationToken ct = default)
{
    var refund = await _refundOrderRepository.GetByAfterSalesIdAsync(afterSalesId, ct);
    if (refund is null)
    {
        return null;
    }

    if (refund.UserId != userId)
    {
        throw new UnauthorizedAccessException("无权访问此退款单");
    }

    return ToDto(refund);
}
```

3.4 修改 `PaymentsController`，调用带 `userId` 校验的方法：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs
// 修改第 42-66 行三个买家端接口
[Authorize(Roles = "Buyer")]
[HttpGet("api/payments/{orderId:guid}")]
[ProducesResponseType(typeof(ApiResponse<PaymentOrderDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetPaymentResultAsync(Guid orderId, CancellationToken ct)
{
    var userId = GetCurrentUserId();
    try
    {
        var result = await _paymentAppService.GetPaymentResultAsync(orderId, userId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "支付单不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }
    catch (UnauthorizedAccessException)
    {
        return Forbid();
    }
}

[Authorize(Roles = "Buyer")]
[HttpGet("api/payments/{paymentId:guid}/status")]
[ProducesResponseType(typeof(ApiResponse<ChannelStatusDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> QueryPaymentStatusAsync(Guid paymentId, CancellationToken ct)
{
    var userId = GetCurrentUserId();
    try
    {
        var result = await _paymentAppService.QueryPaymentStatusAsync(paymentId, userId, ct);
        return Ok(ApiResponse.Success(result));
    }
    catch (UnauthorizedAccessException)
    {
        return Forbid();
    }
}

[Authorize(Roles = "Buyer")]
[HttpGet("api/refunds/{afterSalesId:guid}")]
[ProducesResponseType(typeof(ApiResponse<RefundOrderDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetRefundResultAsync(Guid afterSalesId, CancellationToken ct)
{
    var userId = GetCurrentUserId();
    try
    {
        var result = await _refundAppService.GetRefundResultAsync(afterSalesId, userId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "退款单不存在"));
        }
        return Ok(ApiResponse.Success(result));
    }
    catch (UnauthorizedAccessException)
    {
        return Forbid();
    }
}
```

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Api.Tests --filter "FullyQualifiedName~PaymentsControllerIdorTests"
```

预期：4 个测试全部通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Application/IPaymentAppService.cs \
        src/Services/Payment/Leno.Payment.Application/IRefundAppService.cs \
        src/Services/Payment/Leno.Payment.Application/Services/PaymentAppService.cs \
        src/Services/Payment/Leno.Payment.Application/Services/RefundAppService.cs \
        src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs \
        src/Services/Payment/Leno.Payment.Api.Tests/Controllers/PaymentsControllerIdorTests.cs
git commit -m "fix(payment): P0-4 买家端接口新增用户归属校验，修复 IDOR 漏洞"
```

---

### P0-5 修复微信 V3 回调 ChannelNotifyResult 缺失 OutTradeNo 字段

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L200-L248]、[file:///workspace/src/Services/Payment/Leno.Payment.Domain/Services/IPaymentChannelAdapter.cs#L68-L93]

**根因**：`WeChatPayAdapter.VerifyNotifyAsync` 第 200-248 行解析解密后的 JSON 时，提取了 `transaction_id`、`trade_state`、`success_time`、`amount`，但未提取 `out_trade_no`。`ChannelNotifyResult` 类（第 68-93 行）也没有 `OutTradeNo` 字段。这导致 `WeChatPayNotifyHandler` 无法从验签结果中获取商户单号，只能依赖 `ParseXml` 从原始 XML 报文中提取（但 V3 报文为 JSON），形成 P0-1 的连锁问题。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/WeChatPayAdapterOutTradeNoTests.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Channels.WeChatPay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// P0-5 测试：验证 WeChatPayAdapter.VerifyNotifyAsync 解析 OutTradeNo 并填入 ChannelNotifyResult。
/// </summary>
public class WeChatPayAdapterOutTradeNoTests
{
    private const string OutTradeNo = "PAY20260722000001";
    private const string ChannelTradeNo = "4200000000202607220000000001";
    private const string ApiV3Key = "test_v3_key_32chars_long_1234567890";

    private static WeChatPayAdapter CreateAdapter(ChannelConfig config)
    {
        var configProviderMock = new Mock<IChannelConfigProvider>();
        configProviderMock
            .Setup(p => p.GetConfigAsync(PaymentChannel.WeChatPay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var httpClient = new HttpClient();
        var clientLogger = NullLogger<WeChatPayClient>.Instance;
        var options = Microsoft.Extensions.Options.Options.Create(new WeChatPayOptions
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiV3Key = ApiV3Key,
            PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
            SerialNo = "SERIAL001"
        });
        var client = new WeChatPayClient(httpClient, options, clientLogger);
        var adapterLogger = NullLogger<WeChatPayAdapter>.Instance;

        return new WeChatPayAdapter(client, configProviderMock.Object, adapterLogger);
    }

    /// <summary>
    /// 使用 AES-GCM 加密构造微信 V3 回调 resource.ciphertext。
    /// </summary>
    private static string EncryptResource(string plaintext, string key, string nonce, string associatedData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var nonceBytes = Encoding.UTF8.GetBytes(nonce);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var associatedBytes = string.IsNullOrEmpty(associatedData) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(associatedData);

        using var aes = new AesGcm(keyBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        aes.Encrypt(nonceBytes, plaintextBytes, ciphertext, tag, associatedBytes);

        // 微信格式：ciphertext + tag，Base64 编码
        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
        return Convert.ToBase64String(combined);
    }

    [Fact]
    public async Task VerifyNotifyAsync_ShouldPopulateOutTradeNo_FromDecryptedData()
    {
        // Arrange：构造 V3 回调 JSON，包含加密的 resource
        var decryptedData = "{\"out_trade_no\":\"" + OutTradeNo + "\","
            + "\"transaction_id\":\"" + ChannelTradeNo + "\","
            + "\"trade_state\":\"SUCCESS\","
            + "\"success_time\":\"2026-07-22T10:00:00+08:00\","
            + "\"amount\":{\"total\":10000,\"payer\":{\"total\":10000}}}";

        var nonce = "nonce12345";
        var associatedData = "";
        var ciphertext = EncryptResource(decryptedData, ApiV3Key, nonce, associatedData);

        var rawBody = "{\"id\":\"evt-001\",\"event_type\":\"TRANSACTION.SUCCESS\","
            + "\"resource\":{\"ciphertext\":\"" + ciphertext + "\","
            + "\"nonce\":\"" + nonce + "\","
            + "\"associated_data\":\"" + associatedData + "\"}}";

        // 生成平台密钥对并签名
        using var rsa = RSA.Create(2048);
        var platformPrivateKey = rsa.ExportRSAPrivateKeyPem();
        var platformPublicKey = rsa.ExportRSAPublicKeyPem();

        var timestamp = "1753166400";
        var signMessage = $"{timestamp}\n{nonce}\n{rawBody}\n";
        using var signRsa = RSA.Create();
        signRsa.ImportFromPem(platformPrivateKey);
        var signatureBytes = signRsa.SignData(
            Encoding.UTF8.GetBytes(signMessage),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        var headers = new Dictionary<string, string>
        {
            ["Wechatpay-Timestamp"] = timestamp,
            ["Wechatpay-Nonce"] = nonce,
            ["Wechatpay-Signature"] = signature,
            ["Wechatpay-Serial"] = "SERIAL001"
        };

        var config = new ChannelConfig
        {
            AppId = "wx1234567890",
            MchId = "1234567890",
            ApiKey = ApiV3Key,
            PlatformPublicKey = platformPublicKey,
            NotifyUrl = "https://example.com/notify/wechatpay",
            RefundNotifyUrl = "https://example.com/notify/wechatpay/refund"
        };

        var sut = CreateAdapter(config);

        // Act
        var result = await sut.VerifyNotifyAsync(rawBody, headers);

        // Assert
        Assert.True(result.Verified);
        Assert.Equal(OutTradeNo, result.OutTradeNo);
        Assert.Equal(ChannelTradeNo, result.ChannelTradeNo);
        Assert.True(result.IsPaid);
        Assert.Equal(100m, result.Amount);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayAdapterOutTradeNoTests"
```

预期：编译失败。`ChannelNotifyResult` 无 `OutTradeNo` 字段。

**步骤 3：实现修复**

3.1 修改 `ChannelNotifyResult`，新增 `OutTradeNo` 字段：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Domain/Services/IPaymentChannelAdapter.cs
// 修改第 68-93 行 ChannelNotifyResult 类
public sealed class ChannelNotifyResult
{
    /// <summary>验签是否通过。</summary>
    public bool Verified { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>商户支付单号（out_trade_no），由渠道回调报文中解析。</summary>
    public string? OutTradeNo { get; init; }

    /// <summary>第三方交易号。</summary>
    public string? ChannelTradeNo { get; init; }

    /// <summary>是否为支付成功通知。</summary>
    public bool IsPaid { get; init; }

    /// <summary>支付时间（UTC）。</summary>
    public DateTime? PaidAt { get; init; }

    /// <summary>是否为退款通知。</summary>
    public bool IsRefund { get; init; }

    /// <summary>退款金额（仅退款通知有值）。</summary>
    public decimal? RefundAmount { get; init; }

    /// <summary>实付金额（单位元），仅支付通知有值，用于与本地支付单金额强校验。</summary>
    public decimal? Amount { get; init; }
}
```

3.2 修改 `WeChatPayAdapter.VerifyNotifyAsync`，在解析解密数据时提取 `out_trade_no`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs
// 修改第 193-247 行，在解析解密数据时新增 out_trade_no 提取
var isPaid = false;
var isRefund = false;
string? outTradeNo = null;        // 新增
string? channelTradeNo = null;
DateTime? paidAt = null;
decimal? refundAmount = null;
decimal? amount = null;

if (!string.IsNullOrEmpty(decryptData))
{
    try
    {
        var dataRoot = JsonDocument.Parse(decryptData).RootElement;

        // 新增：提取 out_trade_no（商户支付单号）
        outTradeNo = dataRoot.TryGetProperty("out_trade_no", out var otn) ? otn.GetString() : null;

        channelTradeNo = dataRoot.TryGetProperty("transaction_id", out var txnId) ? txnId.GetString() : null;
        var tradeState = dataRoot.TryGetProperty("trade_state", out var state) ? state.GetString() : null;
        isPaid = string.Equals(tradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

        var successTime = dataRoot.TryGetProperty("success_time", out var st) ? st.GetString() : null;
        paidAt = ParseWeChatTime(successTime);

        var refundStatus = dataRoot.TryGetProperty("refund_status", out var rs) ? rs.GetString() : null;
        isRefund = !string.IsNullOrEmpty(refundStatus);

        if (dataRoot.TryGetProperty("amount", out var amountNode))
        {
            if (isPaid && amountNode.TryGetProperty("total", out var totalNode)
                && totalNode.ValueKind == JsonValueKind.Number)
            {
                amount = amountNode.GetProperty("total").GetInt32() / 100m;
            }

            if (isRefund)
            {
                var refundAmt = amountNode.TryGetProperty("refund", out var ra) ? ra.GetInt32() : 0;
                refundAmount = refundAmt / 100m;
            }
        }
    }
    catch (JsonException)
    {
        // 解密数据解析失败，保持默认值
    }
}

return new ChannelNotifyResult
{
    Verified = verified,
    OrderId = Guid.Empty,
    OutTradeNo = outTradeNo,        // 新增
    ChannelTradeNo = channelTradeNo,
    IsPaid = isPaid,
    PaidAt = paidAt,
    IsRefund = isRefund,
    RefundAmount = refundAmount,
    Amount = amount
};
```

3.3 同步修改 `AlipayAdapter.VerifyNotifyAsync`，在 `ChannelNotifyResult` 中填充 `OutTradeNo`：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs
// 修改 return 语句，新增 OutTradeNo = GetField(dict, "out_trade_no")
return new ChannelNotifyResult
{
    Verified = verified,
    OrderId = Guid.Empty,
    OutTradeNo = GetField(dict, "out_trade_no"),
    ChannelTradeNo = GetField(dict, "trade_no"),
    IsPaid = isPaid,
    PaidAt = paidAt,
    IsRefund = isRefund,
    RefundAmount = refundAmount,
    Amount = amount
};
```

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~WeChatPayAdapterOutTradeNoTests"
```

预期：1 个测试通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Domain/Services/IPaymentChannelAdapter.cs \
        src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs \
        src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs \
        src/Services/Payment/Leno.Payment.Infrastructure.Tests/Channels/WeChatPayAdapterOutTradeNoTests.cs
git commit -m "fix(payment): P0-5 ChannelNotifyResult 新增 OutTradeNo 字段，V3 回调解析商户单号"
```

---

### P0-6 修复 PaymentRequestedEventConsumer 先调渠道下单再保存支付单

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L58-L79]

**根因**：`HandleAsync` 方法在第 58-64 行创建 `PaymentOrder`，第 67 行调用 `adapter.CreatePaymentAsync`（远程渠道下单），第 78 行才 `_paymentOrderRepository.AddAsync` + 第 79 行 `SaveEntitiesAsync`。如果渠道下单成功但数据库保存失败（如 DB 连接断开），支付单在渠道侧已创建但本地无记录，无法关联回调或对账，造成资金损失。正确顺序应为：先持久化支付单（Pending 态），再调渠道下单，最后更新状态并保存。

**步骤 1：编写失败测试**

测试文件：`src/Services/Payment/Leno.Payment.Infrastructure.Tests/Consumers/PaymentRequestedEventConsumerSaveOrderTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Consumers;

/// <summary>
/// P0-6 测试：验证 PaymentRequestedEventConsumer 先保存支付单（Pending 态）再调渠道下单。
/// </summary>
public class PaymentRequestedEventConsumerSaveOrderTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const decimal Amount = 100m;

    private static PaymentRequestedIntegrationEvent CreateEvent()
    {
        return new PaymentRequestedIntegrationEvent
        {
            OrderId = OrderId,
            UserId = UserId,
            Amount = Amount,
            Currency = "CNY",
            Channel = "WeChatPay"
        };
    }

    private static PaymentRequestedEventConsumer CreateConsumer(
        Mock<IPaymentOrderRepository> repoMock,
        Mock<IUnitOfWork> uowMock,
        Mock<PaymentChannelFactory> factoryMock,
        Mock<IIdempotencyStore> idempotencyMock)
    {
        return new PaymentRequestedEventConsumer(
            repoMock.Object,
            uowMock.Object,
            factoryMock.Object,
            NullLogger<PaymentRequestedEventConsumer>.Instance,
            idempotencyMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldSavePaymentOrder_Before_CallingChannelAdapter()
    {
        // Arrange
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        // 记录 AddAsync 和 CreatePaymentAsync 的调用顺序
        var callSequence = new List<string>();
        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("AddAsync"))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("SaveEntitiesAsync"))
            .Returns(Task.CompletedTask);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("CreatePaymentAsync"))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = "4200000000202607220000000001",
                PrepayId = "wx001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=001"
            });

        var factoryMock = new Mock<PaymentChannelFactory>(Mock.Of<IServiceProvider>());
        // PaymentChannelFactory.GetAdapter 返回 IPaymentChannelAdapter
        // 使用反射或部分 Mock 绕过构造函数依赖
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        var idempotencyMock = new Mock<IIdempotencyStore>();

        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);
        var evt = CreateEvent();

        // Act
        await sut.HandleAsync(evt, CancellationToken.None);

        // Assert：AddAsync + SaveEntitiesAsync 必须在 CreatePaymentAsync 之前
        Assert.Contains("AddAsync", callSequence);
        Assert.Contains("SaveEntitiesAsync", callSequence);
        Assert.Contains("CreatePaymentAsync", callSequence);

        var addAsyncIndex = callSequence.IndexOf("AddAsync");
        var saveIndex = callSequence.IndexOf("SaveEntitiesAsync");
        var createPaymentIndex = callSequence.IndexOf("CreatePaymentAsync");

        // 第一次 SaveEntitiesAsync（保存 Pending 态）应在 CreatePaymentAsync 之前
        Assert.True(saveIndex < createPaymentIndex,
            $"SaveEntitiesAsync (index={saveIndex}) 应在 CreatePaymentAsync (index={createPaymentIndex}) 之前");
    }

    [Fact]
    public async Task HandleAsync_WhenChannelSucceedsButSaveFails_ShouldStillHavePaymentOrderInDb()
    {
        // Arrange：第一次保存（Pending 态）成功，渠道下单成功，第二次保存失败
        var repoMock = new Mock<IPaymentOrderRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);

        var savedOrders = new List<PaymentOrder>();
        repoMock.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) => savedOrders.Add(order))
            .Returns(Task.CompletedTask);

        var saveCallCount = 0;
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveCallCount++)
            .ReturnsAsync(() => saveCallCount == 1)
            .ReturnsAsync(() => saveCallCount > 1 ? throw new InvalidOperationException("DB connection lost") : true);

        var adapterMock = new Mock<IPaymentChannelAdapter>();
        adapterMock.Setup(a => a.CreatePaymentAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelPaymentResult
            {
                ChannelTradeNo = "4200000000202607220000000001",
                PrepayId = "wx001",
                CodeUrl = "weixin://wxpay/bizpayurl?pr=001"
            });

        var factoryMock = new Mock<PaymentChannelFactory>(Mock.Of<IServiceProvider>());
        factoryMock.Setup(f => f.GetAdapter(PaymentChannel.WeChatPay))
            .Returns(adapterMock.Object);

        var idempotencyMock = new Mock<IIdempotencyStore>();
        var sut = CreateConsumer(repoMock, uowMock, factoryMock, idempotencyMock);
        var evt = CreateEvent();

        // Act：即使第二次保存失败，支付单已通过第一次保存持久化
        var ex = await Record.ExceptionAsync(() => sut.HandleAsync(evt, CancellationToken.None));

        // Assert：支付单已被保存到仓储（第一次 SaveEntitiesAsync 成功）
        Assert.NotEmpty(savedOrders);
        Assert.Equal(PaymentStatus.Pending, savedOrders[0].Status);
        // 异常应向上抛出触发消息重试，但支付单已在 DB 中
        Assert.NotNull(ex);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~PaymentRequestedEventConsumerSaveOrderTests"
```

预期：测试失败。当前 `CreatePaymentAsync` 在 `AddAsync` + `SaveEntitiesAsync` 之前调用，`callSequence` 中 `CreatePaymentAsync` 排在 `SaveEntitiesAsync` 之前。

**步骤 3：实现修复**

修改 `PaymentRequestedEventConsumer.HandleAsync`，调整执行顺序为：创建支付单 → 保存 Pending 态 → 调渠道下单 → 更新状态 → 保存：

```csharp
// 文件：src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs
// 替换第 58-83 行 HandleAsync 方法体
protected override async Task HandleAsync(PaymentRequestedIntegrationEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    // 幂等：同一订单已存在支付单则跳过
    var existing = await _paymentOrderRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
    if (existing is not null)
    {
        Logger.LogInformation("支付请求事件：订单 {OrderId} 已存在支付单 PaymentId={PaymentId}，跳过",
            integrationEvent.OrderId, existing.Id);
        return;
    }

    if (!Enum.TryParse(integrationEvent.Channel, true, out PaymentChannel channel))
    {
        Logger.LogWarning("支付请求事件：不支持的支付渠道 Channel={Channel} OrderId={OrderId}，跳过",
            integrationEvent.Channel, integrationEvent.OrderId);
        return;
    }

    // 1. 创建支付单（Pending 态）
    var paymentOrder = PaymentOrder.Create(
        Guid.NewGuid(),
        integrationEvent.OrderId,
        integrationEvent.UserId,
        integrationEvent.Amount,
        integrationEvent.Currency,
        channel);

    // 2. 先持久化支付单（Pending 态），确保渠道下单成功后本地有记录可关联
    await _paymentOrderRepository.AddAsync(paymentOrder, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);

    // 3. 调用渠道下单
    var adapter = _channelFactory.GetAdapter(channel);
    var result = await adapter.CreatePaymentAsync(paymentOrder, ct);

    // 4. 根据渠道返回更新支付单状态
    if (string.IsNullOrEmpty(result.ChannelTradeNo))
    {
        paymentOrder.MarkFailed("渠道下单未返回交易号");
    }
    else
    {
        paymentOrder.MarkChannelOrdered(result.ChannelTradeNo, result.PrepayId, result.CodeUrl, result.H5Url);
    }

    // 5. 保存状态更新
    await _paymentOrderRepository.UpdateAsync(paymentOrder, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);

    Logger.LogInformation("支付单已创建 OrderId={OrderId} PaymentId={PaymentId} OutTradeNo={OutTradeNo} Channel={Channel}",
        integrationEvent.OrderId, paymentOrder.Id, paymentOrder.OutTradeNo, channel);
}
```

**步骤 4：验证通过**

```bash
dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~PaymentRequestedEventConsumerSaveOrderTests"
```

预期：2 个测试全部通过。

**步骤 5：提交**

```bash
git add src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs \
        src/Services/Payment/Leno.Payment.Infrastructure.Tests/Consumers/PaymentRequestedEventConsumerSaveOrderTests.cs
git commit -m "fix(payment): P0-6 支付请求消费者先保存支付单再调渠道下单，防止丢单"
```

---

## P1 详细修复计划（任务清单格式）

### P1-7 修复 ReconciliationService 下次对账时间计算逻辑错误

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L48-L49]

**问题描述**：第 48-49 行 `var nextRun = now.Date.AddHours(2).AddHours(8);` 逻辑错误。注释说明"UTC+8 凌晨 2:00 = UTC 18:00"，但 `now.Date.AddHours(2)` 得到当天 UTC 00:02，再 `AddHours(8)` 得到 UTC 10:00（北京时间 18:00），而非预期的 UTC 18:00（北京时间次日 02:00）。应直接计算 UTC 18:00。

**修复任务清单**：

- [ ] 修改 `ReconciliationService.cs` 第 49 行，将 `now.Date.AddHours(2).AddHours(8)` 改为 `now.Date.AddHours(18)`（UTC 18:00 = 北京时间次日 02:00）
- [ ] 验证 `nextRun <= now` 的判断分支：若当前时间已过 UTC 18:00，`nextRun` 应加 1 天
- [ ] 在 `ReconciliationServiceTests.cs` 中新增测试用例：验证在 UTC 10:00 时计算的下次对账时间为当天 UTC 18:00，在 UTC 20:00 时为次日 UTC 18:00
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~ReconciliationServiceTests"` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/ReconciliationServiceTests.cs`

---

### P1-8 修复对账查询按 CreatedAt 过滤而非 PaidAt

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L157-L160]、[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCorePaymentOrderRepository.cs#L118-L126]

**问题描述**：`ReconciliationService.LoadSystemOrdersPagedAsync` 第 157-160 行调用 `paymentRepo.QueryAsync` 传入 `billDate`/`endDateExclusive` 作为 `startDate`/`endDate`，但 `EfCorePaymentOrderRepository.ApplyFilters` 第 118-126 行按 `o.CreatedAt` 过滤。跨日支付（如 23:50 创建、次日 00:10 支付成功）会因 `CreatedAt` 在前一天而被排除出次日的对账范围，造成漏对账。应按 `PaidAt` 过滤已支付单。

**修复任务清单**：

- [ ] 在 `IPaymentOrderRepository.QueryAsync` 签名中新增 `DateTime? paidStart` / `DateTime? paidEnd` 参数，或新增 `QueryPaidByPaidAtAsync` 方法
- [ ] 在 `EfCorePaymentOrderRepository.ApplyFilters` 中新增按 `PaidAt` 过滤的逻辑：`if (paidStart.HasValue) query = query.Where(o => o.PaidAt >= paidStart.Value)`
- [ ] 修改 `ReconciliationService.LoadSystemOrdersPagedAsync`，调用新方法按 `PaidAt` 过滤而非 `CreatedAt`
- [ ] 新增测试：验证跨日支付单（CreatedAt=前一天，PaidAt=当天）被正确纳入当天对账
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~ReconciliationServiceTests"` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Domain/Repositories/IPaymentOrderRepository.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCorePaymentOrderRepository.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/ReconciliationServiceTests.cs`

---

### P1-9 修复 PaymentChannelConfig.Description 公共 setter 绕过聚合封装

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentChannelConfig.cs#L28]

**问题描述**：第 28 行 `public string? Description { get; set; }` 使用公共 `set` 访问器，外部代码可直接赋值绕过聚合根封装。应用层 `PaymentChannelConfigAppService` 第 55-58 行 `config.Description = dto.Description` 直接赋值，未经过聚合方法校验长度限制（`MaxDescriptionLength = 500`）。

**修复任务清单**：

- [ ] 修改 `PaymentChannelConfig.cs` 第 28 行，将 `public string? Description { get; set; }` 改为 `public string? Description { get; private set; }`
- [ ] 新增聚合方法 `UpdateDescription(string? description)`，校验长度后赋值并发布 `PaymentChannelConfigChangedDomainEvent`
- [ ] 修改 `PaymentChannelConfigAppService`，将 `config.Description = dto.Description` 改为 `config.UpdateDescription(dto.Description)`
- [ ] 新增测试：验证 `UpdateDescription` 在超过 500 字符时抛出 `PaymentDomainException`
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~PaymentChannelConfigTests"` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentChannelConfig.cs`
- `src/Services/Payment/Leno.Payment.Application/Services/PaymentChannelConfigAppService.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/PaymentChannelConfigTests.cs`

---

### P1-10 修复 RefundRequestedEventConsumer 未校验原支付单状态为 Paid

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Consumers/RefundRequestedEventConsumer.cs#L60-L64]

**问题描述**：第 60-64 行获取 `originalPayment` 后仅检查是否为 null，未校验 `originalPayment.Status == PaymentStatus.Paid`。若原支付单处于 Pending/ChannelOrdered/Failed/Closed 状态时发起退款，渠道侧会拒绝退款请求，但系统已创建退款单，状态不一致。

**修复任务清单**：

- [ ] 在 `RefundRequestedEventConsumer.cs` 第 64 行后新增状态校验：`if (originalPayment.Status != PaymentStatus.Paid) throw new InvalidOperationException($"原支付单状态非已支付，不可退款 PaymentId={integrationEvent.PaymentId} Status={originalPayment.Status}")`
- [ ] 新增测试：验证原支付单为 Pending 时消费者抛出异常，不创建退款单
- [ ] 新增测试：验证原支付单为 Paid 时消费者正常创建退款单
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/RefundRequestedEventConsumer.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/Consumers/RefundRequestedEventConsumerTests.cs`（新建）

---

### P1-11 修复 PaymentStatusCheckJob 未检查 ExpireAt 超时关单

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L41-L63]

**问题描述**：`ExecuteAsync` 仅查询 Pending/ChannelOrdered 态支付单并调用渠道查询接口，未检查 `ExpireAt` 字段。超过 `ExpireAt`（创建时 +2 小时）的支付单应主动调用 `MarkClosed` 关单，否则过期支付单会一直堆积并被反复查询渠道，浪费资源且可能被渠道侧拒绝。

**修复任务清单**：

- [ ] 在 `PaymentStatusCheckJob.ExecuteAsync` 中新增过期关单逻辑：查询 `ExpireAt < DateTime.UtcNow` 且状态为 Pending/ChannelOrdered 的支付单
- [ ] 对每笔过期支付单调用 `order.MarkClosed("支付超时自动关闭")`，然后 `UpdateAsync` + `SaveEntitiesAsync`
- [ ] 新增仓储方法 `GetExpiredOrdersAsync(DateTime threshold, int page, int pageSize, CancellationToken ct)` 或在 `QueryAsync` 中新增 `expireBefore` 过滤参数
- [ ] 新增测试：验证 ExpireAt 已过期的 Pending 态支付单被标记为 Closed
- [ ] 新增测试：验证 ExpireAt 未过期的支付单不被关闭
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs`
- `src/Services/Payment/Leno.Payment.Domain/Repositories/IPaymentOrderRepository.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCorePaymentOrderRepository.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/Jobs/PaymentStatusCheckJobTests.cs`（新建）

---

### P1-12 修复 PaymentGrpcService 返回 AmountCents=0 / PaidAt=空

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Api/GrpcServices/PaymentGrpcService.cs#L48-L63]、[file:///workspace/src/Services/Payment/Leno.Payment.Application/IPaymentInternalQueryService.cs#L15-L27]

**问题描述**：`PaymentGrpcService.MapToProto` 第 58 行 `AmountCents = 0L` 和第 60 行 `PaidAt = string.Empty` 硬编码默认值，因为 `PaymentInfoResultDto`（第 16-27 行）仅含 `PaymentId`/`Channel`/`OrderId`/`Status`，缺少 `Amount`/`PaidAt`/`TradeNo`/`RefundedAmount` 字段。跨域调用方（如售后域）获取到的支付信息不完整，无法做金额校验或时间判断。

**修复任务清单**：

- [ ] 修改 `PaymentInfoResultDto`，新增字段：`decimal Amount`、`string Currency`、`DateTime? PaidAt`、`string? TradeNo`、`decimal RefundedAmount`
- [ ] 修改 `PaymentInternalQueryService`（实现类），在查询支付单时填充新增字段
- [ ] 修改 `PaymentGrpcService.MapToProto`，使用 DTO 中的真实值填充 `AmountCents`、`PaidAt`、`TransactionId`、`RefundedAmountCents`
- [ ] 同步修改 `InternalPaymentsController` 的返回（DTO 变更后自动包含新字段）
- [ ] 新增测试：验证 gRPC 返回的 `PaymentInfo` 含正确的 `AmountCents` 和 `PaidAt`
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Application/IPaymentInternalQueryService.cs`
- `src/Services/Payment/Leno.Payment.Application/Services/PaymentInternalQueryService.cs`
- `src/Services/Payment/Leno.Payment.Api/GrpcServices/PaymentGrpcService.cs`
- `src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs`

---

### P1-13 修复 PaymentOrder/RefundOrder EF 配置缺失 RowVersion 乐观并发标记

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/PaymentOrderConfiguration.cs#L12-L42]、[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/RefundOrderConfiguration.cs#L12-L41]

**问题描述**：`PaymentOrderConfiguration` 和 `RefundOrderConfiguration` 均未配置 `IsRowVersion()` 乐观并发标记。在并发场景下（如异步通知与补偿任务同时更新同一支付单），可能发生更新覆盖，后写的覆盖先写的数据。`PaymentOrder`/`RefundOrder` 聚合根继承的 `AggregateRoot` 基类需有 `RowVersion` 属性并在 EF 配置中标记为并发令牌。

**修复任务清单**：

- [ ] 检查 `AggregateRoot` 基类是否已有 `RowVersion` 属性（byte[] 或 uint）；若无，新增
- [ ] 在 `PaymentOrderConfiguration.Configure` 中新增 `builder.Property(o => o.RowVersion).IsRowVersion()`
- [ ] 在 `RefundOrderConfiguration.Configure` 中新增 `builder.Property(r => r.RowVersion).IsRowVersion()`
- [ ] 新增 EF Core 迁移：`dotnet ef migrations add AddRowVersionToPaymentAndRefundOrders`
- [ ] 新增测试：验证并发更新同一支付单时抛出 `DbUpdateConcurrencyException`
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Configurations/PaymentOrderConfiguration.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Configurations/RefundOrderConfiguration.cs`
- `src/BuildingBlocks/Leno.SharedKernel/Domain/AggregateRoot.cs`（如需修改基类）
- `src/Services/Payment/Leno.Payment.Infrastructure/Migrations/`（新增迁移文件）

---

### P1-14 修复 ReconciliationDiffConfiguration 表名 PascalCase 且枚举转换不一致

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/ReconciliationDiffConfiguration.cs#L14]

**问题描述**：第 14 行 `builder.ToTable("ReconciliationDiffs")` 使用 PascalCase 表名，与 Payment BC 其他表（如 `payment_orders`、`refund_orders`）的 snake_case 命名规范不一致。第 19/20/26 行 `Channel`/`DiffType`/`Status` 枚举使用 `HasConversion<string>()`（字符串存储），而 `PaymentOrderConfiguration` 中 `Channel`/`Status` 使用 `HasConversion<int>()`（整数存储），同一枚举在不同表中存储类型不一致。

**修复任务清单**：

- [ ] 修改 `ReconciliationDiffConfiguration.cs` 第 14 行，将 `ToTable("ReconciliationDiffs")` 改为 `ToTable("reconciliation_diffs")`
- [ ] 修改第 19/20/26 行枚举转换，与 PaymentOrderConfiguration 保持一致：`Channel` 用 `HasConversion<int>()`，`DiffType`/`Status` 也用 `HasConversion<int>()`（或全部统一为 string，需评估对账查询索引效率）
- [ ] 新增 EF Core 迁移：`dotnet ef migrations add RenameReconciliationDiffsTableAndAlignEnumConversion`
- [ ] 验证对账服务功能不受影响：运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests --filter "FullyQualifiedName~ReconciliationServiceTests"`

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Configurations/ReconciliationDiffConfiguration.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Migrations/`（新增迁移文件）

---

### P1-15 修复 AlipayNotifyHandler 退款通知误用 trade_no 作为渠道退款单号

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs#L163]

**问题描述**：第 163 行 `var channelRefundNo = GetField(fields, "trade_no");` 误将支付宝 `trade_no`（原支付交易号）作为渠道退款单号。支付宝退款通知中退款单号字段为 `trade_no`（退款后的新交易号）在某些场景下可能与原支付 `trade_no` 相同，但更可靠的做法是使用 `out_request_no`（商户退款请求号）或检查是否有专门的退款交易号字段。根据支付宝文档，退款通知中 `trade_no` 确实是退款交易号（与原支付 `trade_no` 不同），但当前代码在 `trade_no` 为空时回退到 `refund.OutRefundNo`（商户退款号），逻辑可接受但应优先使用 `trade_no` 并确保不为空时才使用。

**修复任务清单**：

- [ ] 确认支付宝退款通知字段：`trade_no` 在退款通知中为退款交易号（非原支付交易号），逻辑正确但需确保 `VerifyNotifyAsync` 中 `ChannelTradeNo` 在退款通知时为退款交易号
- [ ] 修改 `AlipayAdapter.VerifyNotifyAsync`，在退款通知时将 `out_request_no` 映射到 `ChannelNotifyResult.OutTradeNo`（供 Handler 查找退款单），将 `trade_no` 映射到 `ChannelTradeNo`（退款交易号）
- [ ] 修改 `AlipayNotifyHandler.HandleRefundNotifyAsync`，使用 `result.OutTradeNo`（即 `out_request_no`）查找退款单，使用 `result.ChannelTradeNo`（即 `trade_no`）作为渠道退款单号
- [ ] 新增测试：验证支付宝退款通知中 `out_request_no` 正确映射到 `ChannelNotifyResult.OutTradeNo`，`trade_no` 映射到 `ChannelTradeNo`
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/Notify/AlipayNotifyHandlerTests.cs`（新建或扩展）

---

## P2 详细修复计划（任务清单格式）

### P2-16 修复 OutTradeNo/OutRefundNo 生成用时间戳+随机数，高并发碰撞风险

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L101]、[file:///workspace/src/Services/Payment/Leno.Payment.Domain/Aggregates/RefundOrder.cs#L119]

**问题描述**：`PaymentOrder.Create` 第 101 行 `$"PAY{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100000, 999999)}"` 格式为 `PAY` + 14 位时间戳 + 6 位随机数，同一秒内 6 位随机数碰撞概率为 1/900000，在高并发支付场景下可能生成重复 `OutTradeNo`。`RefundOrder.Create` 第 119 行同理。

**修复任务清单**：

- [ ] 将 `OutTradeNo` 生成逻辑改为 `PAY` + 14 位时间戳 + 雪花 ID 或 `Guid.NewGuid().ToString("N").Substring(0, 16)`，确保全局唯一
- [ ] 将 `OutRefundNo` 生成逻辑同理改为 `RFD` + 14 位时间戳 + 唯一标识
- [ ] 或引入 `IIdGenerator` 接口由基础设施层提供雪花 ID 生成器，通过依赖注入传入聚合工厂方法
- [ ] 新增测试：验证 10000 次并发生成 `OutTradeNo` 无碰撞
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Domain.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs`
- `src/Services/Payment/Leno.Payment.Domain/Aggregates/RefundOrder.cs`
- `src/Services/Payment/Leno.Payment.Domain.Tests/Aggregates/PaymentOrderTests.cs`（新建或扩展）

---

### P2-17 修复 NotifyController StreamReader 未 using

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L51]、[file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L84]

**问题描述**：第 51 行和第 84 行 `await new StreamReader(Request.Body).ReadToEndAsync(ct)` 创建的 `StreamReader` 未包裹 `using`，虽然底层 `Request.Body` 由 ASP.NET Core 管理生命周期，但 `StreamReader` 自身有缓冲区应显式释放。

**修复任务清单**：

- [ ] 修改 `NotifyController.cs` 第 51 行，改为 `using var reader = new StreamReader(Request.Body); var rawBody = await reader.ReadToEndAsync(ct);`
- [ ] 修改第 84 行同理
- [ ] 编译确认无警告：`dotnet build src/Services/Payment/Leno.Payment.Api`

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs`

---

### P2-18 修复 InternalPaymentsController 双路由标注 [Obsolete] 但未下线

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs#L24-L25]

**问题描述**：第 23-25 行同一 Action 标注了两个 `[HttpGet]` 路由（`internal/v1/payments/{orderId}/info` 和 `internal/payments/{orderId}/info`），第 24 行 `[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]` 标注了旧路由但未实际移除。双路由长期共存增加维护成本。

**修复任务清单**：

- [ ] 确认双路由过渡期是否已过（检查 git log 中 `[Obsolete]` 标注的提交时间）
- [ ] 若已过过渡期，移除第 24-25 行的 `[Obsolete]` 标注和旧路由 `[HttpGet("internal/payments/{orderId}/info}")]`
- [ ] 若未过过渡期，将 `[Obsolete]` 标注从 Action 级别移到旧路由 Attribute 上（使用条件路由或中间件过滤）
- [ ] 检查是否有其他服务仍在调用旧路由 `internal/payments/{orderId}/info`，若无则安全移除
- [ ] 编译确认：`dotnet build src/Services/Payment/Leno.Payment.Api`

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs`

---

### P2-19 修复 WeChatPayAdapter tradeType 硬编码 NATIVE

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L43]

**问题描述**：第 43 行 `const string tradeType = "NATIVE";` 硬编码为扫码支付（Native），不支持 H5 支付（`H5Url`）、JSAPI 支付、小程序支付等场景。当前 `ChannelPaymentResult` 已有 `H5Url` 字段但永远不会被填充。

**修复任务清单**：

- [ ] 将 `tradeType` 从常量改为参数，由 `PaymentOrder` 聚合或 `PaymentRequestedIntegrationEvent` 传入支付方式（NATIVE / H5 / JSAPI / APP）
- [ ] 在 `PaymentOrder` 聚合中新增 `TradeType` 属性（默认 NATIVE 保持兼容）
- [ ] 在 `PaymentRequestedIntegrationEvent` 中新增 `TradeType` 字段
- [ ] 在 `WeChatPayAdapter.CreatePaymentAsync` 中根据 `paymentOrder.TradeType` 传入对应的 `tradeType`
- [ ] 新增测试：验证 H5 支付方式传入 `H5` 时 `tradeType` 为 `H5` 且返回 `H5Url`
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs`
- `src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs`

---

### P2-20 修复 PaymentStatusCheckJob BatchSize 硬编码 100

**审计位置**：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L17-L18]

**问题描述**：第 17-18 行 `private const int ThresholdMinutes = 5; private const int BatchSize = 100;` 为硬编码常量，`BatchSize` 不可通过配置调整。在不同生产环境负载下，固定 100 可能过大（小环境 DB 压力大）或过小（大环境扫描轮次过多）。

**修复任务清单**：

- [ ] 新增 `PaymentJobOptions` 配置类，包含 `ThresholdMinutes`（默认 5）和 `BatchSize`（默认 100）属性
- [ ] 在 `appsettings.json` 的 `Payment:Jobs` 节中绑定配置
- [ ] 修改 `PaymentStatusCheckJob` 构造函数，注入 `IOptions<PaymentJobOptions>` 替代常量
- [ ] 在 DI 注册处绑定配置：`services.Configure<PaymentJobOptions>(configuration.GetSection("Payment:Jobs"))`
- [ ] 新增测试：验证从配置读取的 `BatchSize` 覆盖默认值
- [ ] 运行 `dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests` 确认通过

**涉及文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Config/PaymentJobOptions.cs`（新建）
- `src/Services/Payment/Leno.Payment.Api/appsettings.json`
- `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

---

## 执行顺序建议

按依赖关系和优先级，建议以下执行顺序：

1. **P0-2 + P0-3**（并行）：修复 `ChannelConfig` 新增 `PlatformPublicKey`/`PublicKey` 字段，修正微信/支付宝验签密钥
2. **P0-5**：修复 `ChannelNotifyResult` 新增 `OutTradeNo` 字段（P0-1 依赖此项）
3. **P0-1**：修复 `WeChatPayNotifyHandler` 移除验签前 `ParseXml`（依赖 P0-5 的 `OutTradeNo` 字段）
4. **P0-4**：修复 IDOR（独立，可并行）
5. **P0-6**：修复支付请求消费者保存顺序（独立，可并行）
6. **P1-7 ~ P1-15**：按编号顺序执行
7. **P2-16 ~ P2-20**：按编号顺序执行

## 风险与回滚

- **P0-2/P0-3 风险**：修改验签密钥后，需确保 `appsettings.json` / 环境变量中已配置 `PlatformPublicKey` 和 `PublicKey`，否则验签全部失败。建议先在测试环境验证，再灰度上线。
- **P0-1 风险**：重构 `WeChatPayNotifyHandler` 构造函数为 `IPaymentChannelAdapter` 后，DI 注册需同步修改。若遗漏会导致启动失败。
- **P0-6 风险**：修改保存顺序后，若渠道下单失败但仍已保存 Pending 态支付单，需确保有补偿任务（P1-11）定期关单，否则 Pending 态支付单堆积。
- **P1-13 风险**：新增 `RowVersion` 需要数据库迁移，生产环境需停机或蓝绿部署。
- **P1-14 风险**：重命名表名需数据库迁移，需确认无其他服务直接 SQL 查询 `ReconciliationDiffs` 表。

# 支付集成域 - 任务执行计划

> **模块**: BC6 支付集成域
> **对应文档**: `08-支付集成域.md`
> **任务 ID 前缀**: PAY
> **总任务数**: 6 | **P0**: 3 | **P1**: 1 | **P2**: 2

---

## 模块概述

支付域负责支付单生命周期管理、渠道对接、回调验签与退款执行。已实现核心功能（支付单、微信/支付宝回调、退款），但缺失微信支付/支付宝 SDK 适配器、回调验签、对账文件下载与渠道配置管理。

---

## Task PAY-01: 测试项目创建 [P0]

### 子任务 Checklist

- [x] PAY-01.1: 创建 `Leno.Payment.Domain.Tests` 项目
- [x] PAY-01.2: 创建 `Leno.Payment.Application.Tests` 项目
- [x] PAY-01.3: 创建 `Leno.Payment.Api.Tests` 项目
- [x] PAY-01.4: 覆盖 PaymentOrder 聚合（Create、MarkAsPaying、MarkAsPaid、MarkAsFailed、MarkAsClosed、MarkAsRefunding、MarkAsRefunded）
- [x] PAY-01.5: 覆盖支付渠道适配器（Mock 微信/支付宝）
- [x] PAY-01.6: 覆盖回调验签逻辑
- [x] PAY-01.7: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试覆盖率 ≥ 80%
- [x] 覆盖支付单状态机全流转
- [x] 覆盖多渠道适配器

---

## Task PAY-02: 微信支付 SDK 对接 [P0]

### 子任务 Checklist

- [ ] PAY-02.1: 添加微信支付 SDK NuGet 包（`SkiaSharp.QrCode` 或 `Senparc.Weixin.TenPay`）
- [ ] PAY-02.2: 创建 `WeChatPayChannel` 实现 `IPaymentChannel` 接口
- [ ] PAY-02.3: 实现 `CreatePaymentAsync` - 统一下单（JSAPI/Native/H5），返回支付参数/二维码
- [ ] PAY-02.4: 实现 `QueryPaymentAsync` - 查询订单状态（`GET /v3/pay/transactions/out-trade-no/{outTradeNo}`）
- [ ] PAY-02.5: 实现 `ClosePaymentAsync` - 关闭订单（`POST /v3/pay/transactions/out-trade-no/{outTradeNo}/close`）
- [ ] PAY-02.6: 实现 `CreateRefundAsync` - 申请退款（`POST /v3/refund/domestic/refunds`）
- [ ] PAY-02.7: 实现 `QueryRefundAsync` - 查询退款状态
- [ ] PAY-02.8: 微信支付参数配置（AppId、MchId、ApiV3Key、PrivateKey、NotifyUrl）
- [ ] PAY-02.9: 敏感参数从环境变量/配置中心读取，不落代码仓库
- [ ] PAY-02.10: 编写微信支付 Mock 集成测试

### 验收标准
- [ ] 微信支付统一下单、查询、关闭、退款
- [ ] 回调签名验证
- [ ] 参数配置化，敏感参数不落代码仓库

---

## Task PAY-03: 支付宝 SDK 对接 [P0]

### 子任务 Checklist

- [ ] PAY-03.1: 添加 `AlipaySDKNet` NuGet 包
- [ ] PAY-03.2: 创建 `AlipayChannel` 实现 `IPaymentChannel` 接口
- [ ] PAY-03.3: 实现 `CreatePaymentAsync` - 创建支付（`alipay.trade.page.pay` / `alipay.trade.wap.pay` / `alipay.trade.app.pay`）
- [ ] PAY-03.4: 实现 `QueryPaymentAsync` - 查询订单（`alipay.trade.query`）
- [ ] PAY-03.5: 实现 `ClosePaymentAsync` - 关闭订单（`alipay.trade.close`）
- [ ] PAY-03.6: 实现 `CreateRefundAsync` - 申请退款（`alipay.trade.refund`）
- [ ] PAY-03.7: 实现 `QueryRefundAsync` - 查询退款（`alipay.trade.fastpay.refund.query`）
- [ ] PAY-03.8: 支付宝参数配置（AppId、PrivateKey、AlipayPublicKey、NotifyUrl）
- [ ] PAY-03.9: 编写支付宝 Mock 集成测试

### 验收标准
- [ ] 支付宝支付创建、查询、关闭、退款
- [ ] 回调签名验证
- [ ] 参数配置化，敏感参数不落代码仓库

---

## Task PAY-04: 支付回调验签 [P1]

### 子任务 Checklist

- [ ] PAY-04.1: 在 `WeChatPayChannel` 中实现 `VerifySignature` 方法（微信支付 V3 签名验证）
- [ ] PAY-04.2: 在 `AlipayChannel` 中实现 `VerifySignature` 方法（支付宝 RSA 签名验证）
- [ ] PAY-04.3: 在 `NotifyController` 中先验签再处理业务逻辑
- [ ] PAY-04.4: 验签失败返回 401，不处理业务
- [ ] PAY-04.5: 验签通过后发布 `PaymentSucceededIntegrationEvent` 或 `PaymentFailedIntegrationEvent`
- [ ] PAY-04.6: 回调接口幂等（以渠道交易号去重，Redis 记录已处理回调）
- [ ] PAY-04.7: 编写验签通过/失败场景测试

### 验收标准
- [ ] 微信支付回调验签
- [ ] 支付宝回调验签
- [ ] 验签失败拒绝处理
- [ ] 回调接口幂等

---

## Task PAY-05: 对账文件下载 [P2]

### 子任务 Checklist

- [ ] PAY-05.1: 创建后台服务 `ReconciliationService`（`BackgroundService`）
- [ ] PAY-05.2: 实现微信支付对账文件下载（`GET /v3/bill/tradebill`）
- [ ] PAY-05.3: 实现支付宝对账文件下载（`alipay.data.dataservice.bill.downloadurl.query`）
- [ ] PAY-05.4: 解析对账文件（CSV/TXT），提取交易记录
- [ ] PAY-05.5: 与本系统支付单比对（按交易号匹配）
- [ ] PAY-05.6: 差异记录到对账差异表（`ReconciliationDiff`）
- [ ] PAY-05.7: 实现 `GET /api/admin/reconciliation/diffs` 查询对账差异
- [ ] PAY-05.8: 每日 T+1 自动下载对账文件

### 验收标准
- [ ] 每日自动下载对账文件
- [ ] 对账文件解析正确
- [ ] 差异记录可查询

---

## Task PAY-06: 支付渠道配置管理 [P2]

### 子任务 Checklist

- [ ] PAY-06.1: 创建 `PaymentChannelConfig` 实体（Channel、AppId、MchId、PrivateKey、PublicKey、Enabled）
- [ ] PAY-06.2: 实现 `GET /api/admin/payment-channels` - 列表（密钥脱敏展示）
- [ ] PAY-06.3: 实现 `PUT /api/admin/payment-channels/{channel}` - 更新参数
- [ ] PAY-06.4: 实现 `POST /api/admin/payment-channels/{channel}/enable` - 启用
- [ ] PAY-06.5: 实现 `POST /api/admin/payment-channels/{channel}/disable` - 停用
- [ ] PAY-06.6: 密钥加密存储（AES-256），脱敏返回
- [ ] PAY-06.7: 参数变更发布事件通知支付域刷新配置

### 验收标准
- [ ] 支付渠道参数 CRUD
- [ ] 密钥加密存储
- [ ] 启停不影响已发起支付
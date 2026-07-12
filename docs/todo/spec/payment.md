# 支付集成域 - 缺失功能任务

> **限界上下文**: BC8 支付集成域
> **对应文档**: `08-支付集成域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

支付域已实现核心功能（支付单、微信/支付宝回调、退款），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 微信支付 SDK 对接 | P0 关键 | 微信支付统一下单、查询、关闭订单 |
| 支付宝 SDK 对接 | P0 关键 | 支付宝支付、查询、关闭 |
| 支付渠道适配器模式 | P1 重要 | IPaymentChannel 抽象与多渠道适配 |
| 退款渠道适配 | P1 重要 | 微信/支付宝退款接口对接 |
| 支付回调验签 | P1 重要 | 微信/支付宝回调签名验证 |
| 对账文件下载 | P2 一般 | 微信/支付宝对账文件下载与解析 |
| 支付渠道配置管理 | P2 一般 | 管理员维护支付渠道参数 |
| 支付超时关闭 | P2 一般 | 支付单超时未支付自动关闭 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.Payment.Domain.Tests`、`Leno.Payment.Application.Tests`、`Leno.Payment.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 PaymentOrder 聚合（Create、MarkAsPaying、MarkAsPaid、MarkAsFailed、MarkAsClosed、MarkAsRefunding、MarkAsRefunded）
3. 覆盖支付渠道适配器（Mock 微信/支付宝）
4. 覆盖回调验签逻辑
5. 覆盖 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖支付单状态机全流转
- [ ] 覆盖多渠道适配器
- [ ] 覆盖回调验签

### 参考
- `编码规范.md` 第 13 章
- `08-支付集成域.md` 第 8 章验收标准

---

## Task 2: 微信支付 SDK 对接

**严重程度**: P0 关键

### 功能描述
实现微信支付 SDK 适配器，对接统一下单、查询订单、关闭订单、申请退款接口。

### 技术实现路径
1. 添加 `SkiaSharp.QrCode` 或微信支付 SDK NuGet 包
2. 创建 `WeChatPayChannel` 实现 `IPaymentChannel` 接口
3. 实现：
   - `CreatePaymentAsync` - 统一下单（JSAPI/Native/H5）
   - `QueryPaymentAsync` - 查询订单状态
   - `ClosePaymentAsync` - 关闭订单
   - `CreateRefundAsync` - 申请退款
   - `QueryRefundAsync` - 查询退款状态
   - `VerifySignature` - 回调签名验证
4. 微信支付参数从配置读取（AppId、MchId、ApiKey、NotifyUrl）

### 预期完成标准
- [ ] 微信支付统一下单
- [ ] 微信支付查询订单
- [ ] 微信支付关闭订单
- [ ] 微信支付申请退款
- [ ] 微信支付回调签名验证
- [ ] 参数配置化，敏感参数不落代码仓库

### 参考
- `08-支付集成域.md` 第 4 章微信支付
- `编码规范.md` 第 11 章配置组织

---

## Task 3: 支付宝 SDK 对接

**严重程度**: P0 关键

### 功能描述
实现支付宝 SDK 适配器，对接支付、查询、关闭、退款接口。

### 技术实现路径
1. 添加 `AlipaySDKNet` 或支付宝 SDK NuGet 包
2. 创建 `AlipayChannel` 实现 `IPaymentChannel` 接口
3. 实现：
   - `CreatePaymentAsync` - 创建支付（电脑网站/手机网站/App）
   - `QueryPaymentAsync` - 查询订单
   - `ClosePaymentAsync` - 关闭订单
   - `CreateRefundAsync` - 申请退款
   - `QueryRefundAsync` - 查询退款
   - `VerifySignature` - 回调签名验证
4. 支付宝参数从配置读取（AppId、PrivateKey、PublicKey、NotifyUrl）

### 预期完成标准
- [ ] 支付宝支付创建
- [ ] 支付宝查询订单
- [ ] 支付宝关闭订单
- [ ] 支付宝申请退款
- [ ] 支付宝回调签名验证
- [ ] 参数配置化，敏感参数不落代码仓库

### 参考
- `08-支付集成域.md` 第 4 章支付宝
- `编码规范.md` 第 11 章配置组织

---

## Task 4: 支付回调验签

**严重程度**: P1 重要

### 功能描述
实现微信支付和支付宝的异步通知回调签名验证，防止伪造回调。

### 技术实现路径
1. 在 `WeChatPayChannel` 中实现 `VerifySignature` 方法
2. 在 `AlipayChannel` 中实现 `VerifySignature` 方法
3. 在 `NotifyController` 中先验签再处理业务逻辑
4. 验签失败返回错误，不处理业务
5. 验签通过后发布 `PaymentSucceededIntegrationEvent` 或 `RefundSucceededIntegrationEvent`

### 预期完成标准
- [ ] 微信支付回调验签
- [ ] 支付宝回调验签
- [ ] 验签失败拒绝处理
- [ ] 验签通过后发布对应事件
- [ ] 回调接口幂等（以渠道交易号去重）

### 参考
- `08-支付集成域.md` 第 4 章回调处理
- `00-需求文档总览与DDD架构.md` 第 6.2 节

---

## Task 5: 对账文件下载

**严重程度**: P2 一般

### 功能描述
实现微信支付/支付宝对账文件下载与解析，支持运营对账。

### 技术实现路径
1. 创建后台服务 `ReconciliationService`
2. 定时下载前一日对账文件
3. 解析对账文件（CSV/TXT）
4. 与本系统支付单比对
5. 差异记录到对账差异表
6. 运营端查询对账结果

### 预期完成标准
- [ ] 每日自动下载对账文件
- [ ] 对账文件解析正确
- [ ] 差异记录可查询
- [ ] 运营端展示对账结果

### 参考
- `08-支付集成域.md` 第 4 章对账功能

---

## Task 6: 支付渠道配置管理

**严重程度**: P2 一般

### 功能描述
实现管理员维护支付渠道参数（微信/支付宝的 AppId、商户号、密钥等）。

### 技术实现路径
1. 创建 `PaymentChannelConfig` 聚合或实体
2. 实现 API：
   - `GET /api/admin/payment-channels` - 列表
   - `PUT /api/admin/payment-channels/{channel}` - 更新参数
   - `POST /api/admin/payment-channels/{channel}/enable` - 启用
   - `POST /api/admin/payment-channels/{channel}/disable` - 停用
3. 密钥加密存储，查询脱敏返回
4. 参数变更发布事件通知支付域刷新配置

### 预期完成标准
- [ ] 支付渠道参数 CRUD
- [ ] 密钥加密存储
- [ ] 启停不影响已发起支付
- [ ] 参数变更附加审计日志

### 参考
- `08-支付集成域.md` 第 4 章渠道配置
- `00-需求文档总览与DDD架构.md` 第 4.8 节
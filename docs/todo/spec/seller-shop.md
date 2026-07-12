# 卖家与店铺管理域 - 缺失功能任务

> **限界上下文**: BC10 卖家与店铺管理域
> **对应文档**: `11-卖家与店铺管理域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

卖家与店铺管理域已实现核心功能（店铺申请、审核、管理），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 店铺资质管理 | P1 重要 | 资质证照上传、审核、过期提醒 |
| 店铺暂停/恢复 | P1 重要 | ShopSuspendedEvent/ShopResumedEvent 发布 |
| 店铺关闭 | P1 重要 | 店铺关闭流程与 ShopClosedEvent 发布 |
| 店铺信息变更审核 | P1 重要 | 店铺名称/Logo 等变更需审核 |
| 卖家经营数据 | P1 重要 | 店铺经营概览（订单数、销售额、评分） |
| 订单事件消费 | P1 重要 | 消费订单事件更新店铺经营数据 |
| 商品事件消费 | P2 一般 | 消费商品事件更新店铺商品数 |
| 卖家资质证照上传 | P2 一般 | 通过 IFileStorageService 上传证照 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.SellerShop.Domain.Tests`、`Leno.SellerShop.Application.Tests`、`Leno.SellerShop.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 Shop 聚合（SubmitApplication、Approve、Reject、Suspend、Resume、Close、UpdateInfo）
3. 覆盖 SellerDashboard 应用服务
4. 覆盖 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖店铺状态机全流转
- [ ] 覆盖卖家经营数据查询
- [ ] API 集成测试覆盖申请→审核→经营全流程

### 参考
- `编码规范.md` 第 13 章
- `11-卖家与店铺管理域.md` 第 8 章验收标准

---

## Task 2: 店铺资质管理

**严重程度**: P1 重要

### 功能描述
实现卖家资质证照上传、审核与过期提醒，入驻申请时需提交资质证照。

### 技术实现路径
1. 创建 `ShopQualification` 实体（证照类型、证照号、图片 URL、有效期、审核状态）
2. 店铺入驻申请时提交资质证照
3. 运营审核资质证照（通过/驳回）
4. 资质证照上传通过 `IFileStorageService`
5. 定时任务检测资质证照有效期，到期前提醒

### 预期完成标准
- [ ] 入驻申请时提交资质证照
- [ ] 运营审核资质证照
- [ ] 资质证照到期前提醒
- [ ] 证照图片通过 IFileStorageService 存储

### 参考
- `11-卖家与店铺管理域.md` 第 4 章店铺资质
- `00-需求文档总览与DDD架构.md` 第 4.9 节

---

## Task 3: 店铺暂停/恢复/关闭

**严重程度**: P1 重要

### 功能描述
实现店铺暂停/恢复/关闭的完整流程，并发布对应集成事件。

### 技术实现路径
1. 在 Shop 聚合中实现 `Suspend(reason)`、`Resume()`、`Close(reason)` 方法
2. 暂停时发布 `ShopSuspendedEvent`（驱动商品域置商品不可售、订单域阻止新单）
3. 恢复时发布 `ShopResumedEvent`（驱动商品域恢复商品）
4. 关闭时发布 `ShopClosedEvent`（驱动商品域下架全部商品、订单域停止新单）
5. 实现 API：
   - `POST /api/admin/shops/{id}/suspend` - 暂停店铺
   - `POST /api/admin/shops/{id}/resume` - 恢复店铺
   - `POST /api/admin/shops/{id}/close` - 关闭店铺

### 预期完成标准
- [ ] 店铺暂停发布 ShopSuspendedEvent
- [ ] 店铺恢复发布 ShopResumedEvent
- [ ] 店铺关闭发布 ShopClosedEvent
- [ ] 事件经发件箱模式保证原子性
- [ ] 暂停/关闭店铺不可接新单

### 参考
- `11-卖家与店铺管理域.md` 第 4 章店铺状态管理
- `00-需求文档总览与DDD架构.md` 第 5 章 ShopSuspendedEvent、ShopClosedEvent

---

## Task 4: 店铺经营数据

**严重程度**: P1 重要

### 功能描述
实现卖家经营数据看板，包括订单数、销售额、评分、商品数等概览数据。

### 技术实现路径
1. 在基础设施层创建 `OrderEventConsumer` 消费者
2. 消费 `OrderCreatedEvent`：店铺订单数 +1
3. 消费 `OrderPaidEvent`：店铺销售额累计
4. 消费 `OrderCancelledEvent`：店铺订单数调整
5. 消费 `OrderCompletedEvent`：累计经营概览
6. 消费 `ProductPublishedEvent`：店铺商品数 +1
7. 消费 `ProductTakenDownEvent`：店铺商品数 -1
8. 实现 `SellerDashboardController`：
   - `GET /api/seller/dashboard` - 经营概览（已有）
   - `GET /api/seller/sales-trend` - 销售趋势（已有）

### 预期完成标准
- [ ] 订单事件驱动店铺数据更新
- [ ] 商品事件驱动店铺商品数更新
- [ ] 卖家经营概览正确展示
- [ ] 销售趋势按日期范围查询

### 参考
- `11-卖家与店铺管理域.md` 第 4 章经营数据
- `00-需求文档总览与DDD架构.md` 第 5 章事件清单
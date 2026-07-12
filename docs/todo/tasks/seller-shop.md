# 卖家与店铺管理域 - 任务执行计划

> **模块**: BC9 卖家与店铺管理域
> **对应文档**: `11-卖家与店铺管理域.md`
> **任务 ID 前缀**: SS
> **总任务数**: 4 | **P0**: 1 | **P1**: 3 | **P2**: 0

---

## 模块概述

卖家与店铺管理域负责卖家入驻、店铺申请审核、店铺状态管理与经营数据。已实现核心功能（店铺申请、审核、管理），但缺失店铺资质管理、店铺暂停/恢复/关闭事件发布、店铺信息变更审核与经营数据事件消费。

---

## Task SS-01: 测试项目创建 [P0]

### 子任务 Checklist

- [ ] SS-01.1: 创建 `Leno.SellerShop.Domain.Tests` 项目
- [ ] SS-01.2: 创建 `Leno.SellerShop.Application.Tests` 项目
- [ ] SS-01.3: 创建 `Leno.SellerShop.Api.Tests` 项目
- [ ] SS-01.4: 覆盖 Shop 聚合（SubmitApplication、Approve、Reject、Suspend、Resume、Close、UpdateInfo）
- [ ] SS-01.5: 覆盖 SellerDashboard 应用服务
- [ ] SS-01.6: 配置测试覆盖率 ≥ 80%

### 验收标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖店铺状态机全流转
- [ ] API 集成测试覆盖申请→审核→经营全流程

---

## Task SS-02: 店铺资质管理 [P1]

### 子任务 Checklist

- [ ] SS-02.1: 创建 `ShopQualification` 实体（QualificationType、Number、ImageUrl、ValidFrom、ValidTo、Status）
- [ ] SS-02.2: 店铺入驻申请时强制提交资质证照（营业执照、行业许可证等）
- [ ] SS-02.3: 运营审核资质证照（通过/驳回，驳回需填写原因）
- [ ] SS-02.4: 资质证照上传通过 `IFileStorageService` 存储
- [ ] SS-02.5: 创建后台服务 `QualificationExpiryReminder` 定时检测资质有效期
- [ ] SS-02.6: 资质到期前 30 天/7 天/1 天提醒卖家更新
- [ ] SS-02.7: 资质过期后限制店铺部分功能（不可上架新品）

### 验收标准
- [ ] 入驻申请时提交资质证照
- [ ] 运营审核资质证照
- [ ] 资质证照到期前提醒

---

## Task SS-03: 店铺暂停/恢复/关闭 [P1]

### 子任务 Checklist

- [ ] SS-03.1: 在 Shop 聚合中实现 `Suspend(operatorId, reason)` 方法
- [ ] SS-03.2: 在 Shop 聚合中实现 `Resume(operatorId)` 方法
- [ ] SS-03.3: 在 Shop 聚合中实现 `Close(operatorId, reason)` 方法
- [ ] SS-03.4: 暂停时发布 `ShopSuspendedEvent`（shopId、sellerId、reason、suspendedAt）
- [ ] SS-03.5: 恢复时发布 `ShopResumedEvent`（shopId、sellerId、resumedAt）
- [ ] SS-03.6: 关闭时发布 `ShopClosedEvent`（shopId、sellerId、reason、closedAt）
- [ ] SS-03.7: 事件经发件箱模式保证原子性
- [ ] SS-03.8: 实现 `POST /api/admin/shops/{id}/suspend` 端点
- [ ] SS-03.9: 实现 `POST /api/admin/shops/{id}/resume` 端点
- [ ] SS-03.10: 实现 `POST /api/admin/shops/{id}/close` 端点
- [ ] SS-03.11: 暂停/关闭店铺不可接新单（通过订单域消费事件拦截）

### 验收标准
- [ ] 店铺暂停发布 ShopSuspendedEvent
- [ ] 店铺恢复发布 ShopResumedEvent
- [ ] 店铺关闭发布 ShopClosedEvent
- [ ] 事件经发件箱模式保证原子性

---

## Task SS-04: 店铺经营数据 [P1]

### 子任务 Checklist

- [ ] SS-04.1: 在基础设施层创建 `OrderEventConsumer` 消费者
- [ ] SS-04.2: 消费 `OrderCreatedEvent`：店铺订单数 +1（累计）
- [ ] SS-04.3: 消费 `OrderPaidEvent`：店铺销售额累计（按 sellerId 聚合）
- [ ] SS-04.4: 消费 `OrderCancelledEvent`：店铺取消订单数 +1
- [ ] SS-04.5: 消费 `OrderCompletedEvent`：店铺完成订单数 +1
- [ ] SS-04.6: 在基础设施层创建 `ProductEventConsumer` 消费者
- [ ] SS-04.7: 消费 `ProductPublishedEvent`：店铺商品数 +1
- [ ] SS-04.8: 消费 `ProductTakenDownEvent`：店铺商品数 -1
- [ ] SS-04.9: 实现 `GET /api/seller/dashboard` - 经营概览（今日/本月/累计）
- [ ] SS-04.10: 实现 `GET /api/seller/sales-trend` - 销售趋势（按日期范围）

### 验收标准
- [ ] 订单事件驱动店铺数据更新
- [ ] 商品事件驱动店铺商品数更新
- [ ] 卖家经营概览正确展示
# 商品域 - 任务执行计划

> **模块**: BC2 商品域
> **对应文档**: `02-商品域.md`
> **任务 ID 前缀**: PRD
> **总任务数**: 7 | **P0**: 1 | **P1**: 4 | **P2**: 2

---

## 模块概述

商品域负责 SPU/SKU 建模、分类与品牌管理、商品审核流转与搜索索引构建。已实现核心功能（商品 CRUD、审核、搜索、分类、品牌），但缺失审核历史、价格变更历史、店铺事件联动、库存补货、运营全量商品列表与 SKU 编码唯一校验。

---

## Task PRD-01: 测试项目创建 [P0] ✅ (2026-07-11)

### 子任务 Checklist

- [x] PRD-01.1: 创建 `Leno.Product.Domain.Tests` 项目
- [x] PRD-01.2: 创建 `Leno.Product.Application.Tests` 项目
- [x] PRD-01.3: 创建 `Leno.Product.Api.Tests` 项目
- [x] PRD-01.4: 覆盖 Product 聚合（SubmitForReview、Approve、Reject、TakeDown、SuspendByShop、ResumeByShop）
- [x] PRD-01.5: 覆盖 Category 聚合（Enable、Disable）和 Brand 聚合
- [x] PRD-01.6: 覆盖应用服务（CreateAsync、ApproveAsync、RejectAsync 等）
- [x] PRD-01.7: 覆盖 API 集成测试（卖家管理、搜索、管理员审核）
- [x] PRD-01.8: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试覆盖率 ≥ 80%（67 项测试）
- [x] 覆盖商品状态机全流转路径
- [x] 覆盖分类树与品牌管理
- [x] 应用层测试 15 项，API 集成测试 10 项

**测试统计**: Domain 67 + Application 15 + API 10 = 92 项测试全部通过

---

## Task PRD-02: 商品审核历史记录 [P1]

### 子任务 Checklist

- [ ] PRD-02.1: 创建 `AuditInfo` 值对象（OperatorId、OperatorName、Result、Reason、AuditedAt）
- [ ] PRD-02.2: 在 Product 聚合中维护 `_auditHistory` 列表（`List<AuditInfo>`）
- [ ] PRD-02.3: 修改 `Publish` 方法追加审核历史（Result=Approved）
- [ ] PRD-02.4: 修改 `Reject` 方法追加审核历史（Result=Rejected，含原因）
- [ ] PRD-02.5: 提供 `GetAuditHistory()` 方法返回审核历史
- [ ] PRD-02.6: 配置 EF Core 值转换存储 AuditInfo 列表（JSON 列）
- [ ] PRD-02.7: 查询商品详情时返回审核历史列表

### 验收标准
- [ ] 每次审核操作记录到历史列表
- [ ] 审核历史包含操作人、时间、结果、原因
- [ ] 审核历史不可修改

---

## Task PRD-03: 价格变更历史 [P1]

### 子任务 Checklist

- [ ] PRD-03.1: 创建 `PriceChangeRecord` 值对象（SkuId、OldPrice、NewPrice、ChangedAt、ChangedBy）
- [ ] PRD-03.2: 在 Product 聚合中维护 `_priceChangeHistory` 列表
- [ ] PRD-03.3: 修改 `AdjustPrice` 方法追加历史记录
- [ ] PRD-03.4: 提供 `GetPriceHistory(skuId)` 方法
- [ ] PRD-03.5: 配置 EF Core 值转换存储价格历史列表
- [ ] PRD-03.6: 实现 `GET /api/products/{id}/price-history` 查询端点

### 验收标准
- [ ] 每次价格调整记录变更历史
- [ ] 变更历史包含新旧价格、时间、操作人
- [ ] 可按 SKU 查询价格变更历史

---

## Task PRD-04: 店铺暂停/恢复联动 [P1]

### 子任务 Checklist

- [ ] PRD-04.1: 在基础设施层创建 `ShopEventConsumer` 消费者
- [ ] PRD-04.2: 消费 `ShopSuspendedEvent`：按 sellerId 查询所有已上架商品，调用 `SuspendByShop`
- [ ] PRD-04.3: 消费 `ShopResumedEvent`：按 sellerId 查询所有店铺暂停态商品，调用 `ResumeByShop`
- [ ] PRD-04.4: 消费 `ShopClosedEvent`：按 sellerId 查询所有商品，调用 `TakeDown`
- [ ] PRD-04.5: 幂等消费以 EventId 去重（Redis 记录已消费事件 ID）
- [ ] PRD-04.6: 批量操作使用分页处理避免大事务

### 验收标准
- [ ] 店铺暂停时关联商品自动置为店铺暂停态
- [ ] 店铺恢复时商品恢复已上架态
- [ ] 店铺关闭时商品全部下架
- [ ] 事件消费幂等

---

## Task PRD-05: 库存补货与盘点 [P1]

### 子任务 Checklist

- [ ] PRD-05.1: 完善 Product 聚合中 `UpdateStock(skuId, delta)` 方法（校验结果 ≥ 0）
- [ ] PRD-05.2: 实现 `POST /api/products/{id}/skus/{skuId}/stock` 端点
- [ ] PRD-05.3: 发布 `StockAdjustedEvent`（SkuId、ProductId、Delta、NewStock）
- [ ] PRD-05.4: 在基础设施层实现 `StockAdjustedEventConsumer` 同步 ES 读模型
- [ ] PRD-05.5: 库存变更记录操作日志（操作人、时间、delta）

### 验收标准
- [ ] 卖家可调整指定 SKU 库存
- [ ] 库存调整后校验结果 ≥ 0
- [ ] 发布 StockAdjustedEvent 同步 ES

---

## Task PRD-06: 运营全量商品管理列表 [P2]

### 子任务 Checklist

- [ ] PRD-06.1: 实现 `GET /api/admin/products/all` 端点
- [ ] PRD-06.2: 支持按状态（草稿/待审核/已上架/已下架/已驳回/店铺暂停）筛选
- [ ] PRD-06.3: 支持按卖家 ID、分类 ID、关键词筛选
- [ ] PRD-06.4: 分页返回，包含审核状态信息
- [ ] PRD-06.5: 与卖家端商品列表隔离（卖家只能看自己的）

### 验收标准
- [ ] 运营端可查看全平台商品
- [ ] 支持多维度筛选
- [ ] 仅 Admin/Operator 角色可访问

---

## Task PRD-07: SKU 编码全局唯一校验 [P2]

### 子任务 Checklist

- [ ] PRD-07.1: 在领域层定义 `IProductUniquenessChecker` 接口
- [ ] PRD-07.2: 在基础设施层实现 `ProductUniquenessChecker`（查询数据库校验唯一性）
- [ ] PRD-07.3: 在商品创建/编辑应用服务中调用校验
- [ ] PRD-07.4: 支持排除自身 ID（编辑场景，`excludeProductId` 参数）
- [ ] PRD-07.5: 重复时返回明确错误提示（"SKU 编码已被使用" / "商品标题已存在"）

### 验收标准
- [ ] SKU 编码全局唯一校验
- [ ] 商品标题同店铺内不重复
- [ ] 编辑场景支持排除自身
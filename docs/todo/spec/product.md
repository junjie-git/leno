# 商品域 - 缺失功能任务

> **限界上下文**: BC2 商品域
> **对应文档**: `02-商品域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

商品域已实现核心功能（商品 CRUD、审核、搜索、分类、品牌），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 商品审核历史 | P1 重要 | 审核记录可追溯，当前仅记录当前审核状态 |
| 价格变更历史 | P1 重要 | 价格调整需记录变更历史 |
| 店铺暂停/恢复联动 | P1 重要 | 消费 ShopSuspendedEvent/ShopResumedEvent 联动商品状态 |
| 库存补货/盘点 | P1 重要 | 卖家调整库存基线（StockAdjustedEvent） |
| 商品批量操作 | P2 一般 | 批量上架/下架/审核 |
| 运营全量商品管理列表 | P2 一般 | 运营端查看所有卖家商品（含已下架） |
| SKU 编码全局唯一校验 | P2 一般 | IProductUniquenessChecker 实现 |
| 商品搜索评分/销量排序 | P2 一般 | 按评分、销量排序功能 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.Product.Domain.Tests`、`Leno.Product.Application.Tests`、`Leno.Product.Api.Tests` 测试项目。

### 技术实现路径
1. 创建 `src/Services/Product/Leno.Product.Domain.Tests/` 项目
2. 覆盖 Product 聚合所有方法（SubmitForReview、Publish、Reject、TakeDown、AdjustPrice、UpdateStock 等）
3. 覆盖 Category 聚合（Enable、Disable）
4. 覆盖 Brand 聚合
5. 覆盖应用服务与 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖商品状态机全流转路径
- [ ] 覆盖分类树与品牌管理
- [ ] API 集成测试覆盖买家搜索与卖家管理

### 参考
- `编码规范.md` 第 13 章
- `02-商品域.md` 第 4 章功能需求

---

## Task 2: 商品审核历史记录

**严重程度**: P1 重要

### 功能描述
实现商品审核历史记录，使每次审核操作（通过/驳回）可追溯。当前仅记录当前审核状态。

### 技术实现路径
1. 创建 `AuditInfo` 值对象，记录审核人、审核时间、审核结果、驳回原因
2. 在 Product 聚合中维护 `_auditHistory` 列表
3. `Publish` 和 `Reject` 方法在设置当前审核状态的同时追加历史记录
4. 提供 `GetAuditHistory()` 方法返回审核历史
5. 配置 EF Core 值转换存储 AuditInfo 列表

### 预期完成标准
- [ ] 每次审核操作记录到历史列表
- [ ] 审核历史包含操作人、时间、结果、原因
- [ ] 查询商品详情时返回审核历史
- [ ] 审核历史不可修改

### 参考
- `02-商品域.md` 第 2.1.1 节 Product 聚合 AuditInfo 字段

---

## Task 3: 价格变更历史

**严重程度**: P1 重要

### 功能描述
实现 `AdjustPrice` 方法记录价格变更历史，包括变更前价格、变更后价格、变更时间、操作人。

### 技术实现路径
1. 创建 `PriceChangeRecord` 值对象（SkuId、OldPrice、NewPrice、ChangedAt、ChangedBy）
2. 在 Product 聚合中维护 `_priceChangeHistory` 列表
3. `AdjustPrice` 方法追加历史记录
4. 提供 `GetPriceHistory(skuId)` 方法

### 预期完成标准
- [ ] 每次价格调整记录变更历史
- [ ] 变更历史包含新旧价格、时间、操作人
- [ ] 可按 SKU 查询价格变更历史

### 参考
- `02-商品域.md` 第 2.1.1 节 AdjustPrice 方法

---

## Task 4: 店铺暂停/恢复联动

**严重程度**: P1 重要

### 功能描述
消费 `ShopSuspendedEvent` 和 `ShopResumedEvent`（来自卖家域），联动商品状态变更。

### 技术实现路径
1. 在基础设施层创建 `ShopEventConsumer` 消费者
2. 消费 `ShopSuspendedEvent`：调用 `Product.SuspendByShop` 置商品为店铺暂停态
3. 消费 `ShopResumedEvent`：调用 `Product.ResumeByShop` 恢复商品已上架态
4. 消费 `ShopClosedEvent`：调用 `Product.TakeDown` 下架全部商品
5. 幂等消费以 EventId 去重

### 预期完成标准
- [ ] 店铺暂停时关联商品自动置为店铺暂停态
- [ ] 店铺恢复时商品恢复已上架态
- [ ] 店铺关闭时商品全部下架
- [ ] 事件消费幂等

### 参考
- `02-商品域.md` 第 3 章领域事件清单
- `02-商品域.md` 第 2.1.1 节 SuspendByShop/ResumeByShop 方法

---

## Task 5: 库存补货与盘点

**严重程度**: P1 重要

### 功能描述
实现卖家补货或盘点修正库存的 `UpdateStock` 方法，发布 `StockAdjustedEvent` 驱动 ES 同步。

### 技术实现路径
1. 在 Product 聚合中完善 `UpdateStock(skuId, delta)` 方法（校验结果 ≥ 0）
2. 实现库存调整 API：`POST /api/products/{id}/skus/{skuId}/stock`
3. 发布 `StockAdjustedEvent` 同步 ES 读模型
4. 库存变更记录操作日志

### 预期完成标准
- [ ] 卖家可调整指定 SKU 库存
- [ ] 库存调整后校验结果 ≥ 0
- [ ] 发布 StockAdjustedEvent 同步 ES
- [ ] 库存调整记录操作日志

### 参考
- `02-商品域.md` 第 2.1.1 节 UpdateStock 方法
- `02-商品域.md` 第 3 章 StockAdjustedEvent

---

## Task 6: 运营全量商品管理列表

**严重程度**: P2 一般

### 功能描述
实现运营端查看所有卖家商品（含已下架、已驳回等全部状态），支持按状态、卖家、分类筛选。

### 技术实现路径
1. 在 `AdminProductsController` 中添加 `GET /api/admin/products/all` 端点
2. 支持按状态、卖家 ID、分类 ID、关键词筛选
3. 分页返回，包含审核状态信息
4. 与卖家端商品列表隔离（卖家只能看自己的）

### 预期完成标准
- [ ] 运营端可查看全平台商品
- [ ] 支持多维度筛选（状态、卖家、分类、关键词）
- [ ] 分页查询
- [ ] 仅 Admin/Operator 角色可访问

### 参考
- `02-商品域.md` 第 4.0 节功能点概览表 F-PRD-004

---

## Task 7: SKU 编码全局唯一校验

**严重程度**: P2 一般

### 功能描述
实现 `IProductUniquenessChecker` 领域服务，校验 SKU 编码全局唯一与商品标题同店铺内不重复。

### 技术实现路径
1. 在领域层定义 `IProductUniquenessChecker` 接口
2. 在基础设施层实现（查询数据库校验唯一性）
3. 在商品创建/编辑应用服务中调用校验
4. 支持排除自身 ID（编辑场景）

### 预期完成标准
- [ ] SKU 编码全局唯一校验
- [ ] 商品标题同店铺内不重复
- [ ] 编辑场景支持排除自身
- [ ] 重复时返回明确错误提示

### 参考
- `02-商品域.md` 第 2.2 节 IProductUniquenessChecker
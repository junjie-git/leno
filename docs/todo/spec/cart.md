# 购物车域 - 缺失功能任务

> **限界上下文**: BC3 购物车域
> **对应文档**: `03-购物车域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

购物车域已实现核心功能（添加、修改、删除、选中、总价计算），但以下功能缺失：

| 缺失项               | 严重程度 | 说明                                          |
| -------------------- | -------- | --------------------------------------------- |
| 测试项目             | P0 关键  | 无任何测试项目                                |
| 匿名购物车 (Redis)   | P0 关键  | 仅实现登录用户购物车，匿名购物车未实现        |
| 登录时匿名购物车合并 | P1 重要  | 登录后合并匿名购物车到用户购物车              |
| 商品下架失效标记     | P1 重要  | 消费 ProductTakenDownEvent 自动标记失效项     |
| 商品上架恢复标记     | P1 重要  | 消费 ProductPublishedEvent 恢复失效项         |
| 商品信息变更刷新     | P1 重要  | 消费 ProductUpdatedEvent 刷新购物车项展示快照 |
| 全选/取消全选        | P2 一般  | 批量切换所有有效项选中状态                    |
| 购物车失效商品提示   | P2 一般  | 失效项在总价计算时标记提示                    |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述

创建 `Leno.Cart.Domain.Tests`、`Leno.Cart.Application.Tests`、`Leno.Cart.Api.Tests` 测试项目。

### 技术实现路径

1. 创建 `src/Services/Cart/Leno.Cart.Domain.Tests/` 项目
2. 覆盖 Cart 聚合所有方法（AddItem、ChangeQuantity、RemoveItem、ToggleSelection、MarkInvalid 等）
3. 覆盖 CartPricingDomainService
4. 覆盖应用服务（GetCartAsync、AddItemAsync、MergeAnonymousCartAsync）
5. 覆盖 API 控制器

### 预期完成标准

- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖数量合并、种类上限、失效项不可选中等不变量
- [ ] 应用层测试覆盖价格计算编排
- [ ] API 集成测试覆盖匿名与登录两种场景

### 参考

- `编码规范.md` 第 13 章
- `03-购物车域.md` 第 4 章功能需求

---

## Task 2: 匿名购物车 (Redis)

**严重程度**: P0 关键

### 功能描述

实现未登录用户的匿名购物车，以 `AnonymousId` 为键存入 Redis，结构为轻量 JSON（仅 SKU ID、数量、选中状态、加入时间），TTL 默认 30 天。

### 技术实现路径

1. 实现 `IAnonymousCartStore` 接口
2. 创建 `RedisCartStore` 实现（基于 StackExchange.Redis）
3. 客户端通过 `X-Anonymous-Id` 请求头传递匿名标识
4. 修改 `CartsController` 支持匿名购物车操作
5. 每次访问续期 TTL
6. Redis 不可用时返回错误提示

### 预期完成标准

- [ ] 匿名购物车存 Redis，TTL 7 天
- [ ] 加购/修改/删除/选中操作完整
- [ ] 通过 `X-Anonymous-Id` 头隔离数据
- [ ] 每次访问续期 TTL
- [ ] Redis 不可用时友好提示

### 参考

- `03-购物车域.md` 第 4 章 FP-01
- `03-购物车域.md` 第 2.1 节匿名购物车存储说明

---

## Task 3: 登录时匿名购物车合并

**严重程度**: P1 重要

### 功能描述

实现登录后将匿名购物车合并到用户购物车，同 SKU 合并数量，保留用户购物车已有项，清空匿名购物车。

### 技术实现路径

1. 在 `ICartAppService` 中实现 `MergeAnonymousCartAsync` 方法
2. 合并逻辑：遍历匿名购物车项，逐项调用 `AddItem`（同 SKU 自动合并）
3. 登录接口（AuthController）中触发合并
4. 合并完成后删除匿名购物车 Redis 键
5. 发布 `CartMergedEvent`

### 预期完成标准

- [ ] 登录后自动合并匿名购物车
- [ ] 同 SKU 合并数量（不超 99）
- [ ] 合并后清空匿名购物车
- [ ] 发布 CartMergedEvent
- [ ] 合并失败不影响登录

### 参考

- `03-购物车域.md` 第 4 章 FP-09
- `03-购物车域.md` 第 3 章 CartMergedEvent

---

## Task 4: 商品事件消费（下架/上架/变更）

**严重程度**: P1 重要

### 功能描述

消费商品域发布的集成事件，自动处理购物车项的失效/恢复/刷新。

### 技术实现路径

1. 在基础设施层创建 `ProductEventConsumer` 消费者
2. 消费 `ProductTakenDownEvent`：按 SKU 调用 `Cart.MarkInvalid`，自动取消选中
3. 消费 `ProductPublishedEvent`：按 SKU 调用 `Cart.MarkValid`，恢复有效
4. 消费 `ProductUpdatedEvent`：刷新购物车项展示快照（名称、图片）
5. 幂等消费以 EventId 去重

### 预期完成标准

- [ ] 商品下架时购物车项自动标记失效
- [ ] 商品重新上架时购物车项恢复有效
- [ ] 商品信息变更时刷新展示快照
- [ ] 事件消费幂等

### 参考

- `03-购物车域.md` 第 3 章领域事件清单
- `03-购物车域.md` 第 4 章 FP-11

---

## Task 5: 全选/取消全选

**严重程度**: P2 一般

### 功能描述

实现批量切换所有有效项选中状态，失效项不受影响。

### 技术实现路径

1. 在 Cart 聚合中实现 `ToggleAllSelection(bool isSelected)` 方法
2. 仅操作有效项（IsValid=true），失效项保持未选中
3. 实现 API：`PUT /api/cart/select-all`

### 预期完成标准

- [ ] 全选仅操作有效项
- [ ] 失效项不受全选/取消全选影响
- [ ] 批量操作性能可接受

### 参考

- `03-购物车域.md` 第 4 章 FP-07
- `03-购物车域.md` 第 2.1 节 ToggleAllSelection 方法

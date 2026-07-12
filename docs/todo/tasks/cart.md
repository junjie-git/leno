# 购物车域 - 任务执行计划

> **模块**: BC3 购物车域
> **对应文档**: `03-购物车域.md`
> **任务 ID 前缀**: CART
> **总任务数**: 5 | **P0**: 2 | **P1**: 2 | **P2**: 1

---

## 模块概述

购物车域负责买家在结算前的商品暂存与选中管理。已实现登录用户购物车的核心功能（添加、修改、删除、选中、总价计算），但缺失匿名购物车（Redis）、登录合并、商品事件消费（下架/上架/变更）与全选功能。

---

## Task CART-01: 测试项目创建 [P0]

### 子任务 Checklist

- [x] CART-01.1: 创建 `Leno.Cart.Domain.Tests` 项目
- [x] CART-01.2: 创建 `Leno.Cart.Application.Tests` 项目
- [x] CART-01.3: 创建 `Leno.Cart.Api.Tests` 项目
- [x] CART-01.4: 覆盖 Cart 聚合（AddItem、UpdateQuantity、RemoveItem、SelectItems、DeselectItems、ClearSelectedItems、ClearItemsBySourceIds）
- [ ] CART-01.5: 覆盖 CartPricingDomainService
- [x] CART-01.6: 覆盖应用服务（AddItemAsync、UpdateQuantityAsync、RemoveItemAsync、SelectItemsAsync、GetCartAsync、PreviewCheckoutAsync）
- [x] CART-01.7: 覆盖 API 集成测试（登录用户场景，GET/POST/PUT/DELETE 端点）
- [ ] CART-01.8: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试 14 项（Cart 聚合创建、添加、合并、更新、删除、选中、清空）
- [x] 应用层单元测试 18 项（六大方法正常/异常路径全覆盖）
- [x] API 集成测试 8 项（健康检查、认证、六个端点）
- [ ] 测试覆盖率 ≥ 80%（待配置 coverlet）
- [ ] CartPricingDomainService 测试（待领域服务实现后补充）

---

## Task CART-02: 匿名购物车 (Redis) [P0]

### 子任务 Checklist

- [ ] CART-02.1: 在领域层定义 `IAnonymousCartStore` 接口
- [ ] CART-02.2: 创建 `RedisCartStore` 实现（基于 StackExchange.Redis，JSON 序列化）
- [ ] CART-02.3: Redis Key 格式: `cart:anonymous:{anonymousId}`，TTL 30 天
- [ ] CART-02.4: 数据结构：List<CartItemDto>（SkuId、Quantity、IsSelected、AddedAt）
- [ ] CART-02.5: 修改 `CartsController` 支持匿名购物车操作（通过 `X-Anonymous-Id` 头）
- [ ] CART-02.6: 每次访问续期 TTL（`ExtendTtlAsync`）
- [ ] CART-02.7: Redis 不可用时返回友好提示
- [ ] CART-02.8: 添加 `RedisCartStore` 集成测试

### 验收标准
- [ ] 匿名购物车存 Redis，TTL 30 天
- [ ] 加购/修改/删除/选中操作完整
- [ ] 通过 `X-Anonymous-Id` 头隔离数据
- [ ] 每次访问续期 TTL

---

## Task CART-03: 登录时匿名购物车合并 [P1]

### 子任务 Checklist

- [ ] CART-03.1: 在 `ICartAppService` 中实现 `MergeAnonymousCartAsync(userId, anonymousId)` 方法
- [ ] CART-03.2: 合并逻辑：遍历匿名购物车项，逐项调用 `AddItem`（同 SKU 自动合并数量）
- [ ] CART-03.3: 合并后单 SKU 总量不超 99、种类不超 50
- [ ] CART-03.4: 选中状态按"任一来源选中即选中"合并
- [ ] CART-03.5: 合并完成后删除匿名购物车 Redis 键
- [ ] CART-03.6: 发布 `CartMergedEvent`（UserId、AnonymousId、MergedItemCount）
- [ ] CART-03.7: 实现 `POST /api/cart/merge` 端点
- [ ] CART-03.8: 合并幂等：同一匿名标识重复触发合并无操作
- [ ] CART-03.9: 合并失败不影响登录

### 验收标准
- [ ] 登录后自动合并匿名购物车
- [ ] 同 SKU 合并数量（不超 99）
- [ ] 合并后清空匿名购物车
- [ ] 合并幂等

---

## Task CART-04: 商品事件消费（下架/上架/变更） [P1]

### 子任务 Checklist

- [ ] CART-04.1: 在基础设施层创建 `ProductEventConsumer` 消费者
- [ ] CART-04.2: 消费 `ProductTakenDownEvent`：按 skuIds 调用 `Cart.MarkInvalid(skuId, reason)`，自动取消选中
- [ ] CART-04.3: 消费 `ProductPublishedEvent`：按 skuIds 调用 `Cart.MarkValid(skuId)`，恢复有效
- [ ] CART-04.4: 消费 `ProductUpdatedEvent`：刷新购物车项展示快照（名称、图片）
- [ ] CART-04.5: 消费 `OrderCreatedEvent`：调用 `Cart.ClearCheckedOutItems(checkedOutItemIds)` 清空已结算项
- [ ] CART-04.6: 幂等消费以 EventId 去重
- [ ] CART-04.7: 批量更新购物车时使用乐观锁防并发冲突

### 验收标准
- [ ] 商品下架时购物车项自动标记失效
- [ ] 商品重新上架时购物车项恢复有效
- [ ] 商品信息变更时刷新展示快照
- [ ] 下单后清空已结算项

---

## Task CART-05: 全选/取消全选 [P2]

### 子任务 Checklist

- [ ] CART-05.1: 在 Cart 聚合中实现 `ToggleAllSelection(bool isSelected)` 方法
- [ ] CART-05.2: 仅操作有效项（IsValid=true），失效项保持未选中
- [ ] CART-05.3: 实现 `PATCH /api/cart/selection` 端点
- [ ] CART-05.4: 空购物车返回成功且无副作用
- [ ] CART-05.5: 编写全选/取消全选单元测试

### 验收标准
- [ ] 全选仅操作有效项
- [ ] 失效项不受全选/取消全选影响
- [ ] 批量操作性能可接受
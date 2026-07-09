# 购物车域 (Cart) 开发任务

> **限界上下文**: BC3 购物车域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis  
> **依赖**: `shared-kernel`、`product`（SKU 价格与库存查询）  
> **对应文档**: `03-购物车域.md`

---

## 模块概述

购物车域管理买家选购商品行项，支持添加、修改数量、删除、选中/取消选中、多卖家分组预览。下单时购物车选中项转化为订单行快照，订单创建后消费 `OrderCreatedEvent` 清空已结算项。购物车数据以 Redis 缓存为主、DB 持久化为辅。

---

## Task 1: 项目初始化与领域层 — Cart 聚合

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Domain/Leno.Cart.Domain.csproj`
- Create: `src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs`
- Create: `src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs`

- [ ] 创建 Leno.Cart.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `Cart` 聚合根（CartId、UserId、Items、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `Cart.Create` 工厂方法（UserId 关联，初始化空购物车）
- [ ] 实现 `Cart.AddItem(skuId, quantity, sellerId)`（同 SKU 合并数量，校验数量 1-99）
- [ ] 实现 `Cart.UpdateItemQuantity(skuId, quantity)`（校验数量 1-99）
- [ ] 实现 `Cart.RemoveItem(skuId)` 方法
- [ ] 实现 `Cart.SelectItems(skuIds)`/`Cart.DeselectItems(skuIds)`（选中/取消选中）
- [ ] 实现 `Cart.ClearSelectedItems()`（订单创建后清空已结算项）
- [ ] 实现 `CartItem` 实体（CartItemId、SkuId、Quantity、SellerId、IsSelected、SourceCartItemId）
- [ ] 编写单元测试覆盖购物车操作
- [ ] 提交：`feat(cart): add Cart aggregate root`

---

## Task 2: 领域层 — 仓储接口与领域服务

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Domain/Repositories/ICartRepository.cs`
- Create: `src/Services/Cart/Leno.Cart.Domain/Services/ICartPriceService.cs`

- [ ] 定义 `ICartRepository` 接口（GetByUserIdAsync、AddAsync、UpdateAsync、DeleteAsync）
- [ ] 定义 `ICartPriceService` 防腐层接口（GetSkuPricesAsync，供购物车预览时查询商品域实时价格）
- [ ] 提交：`feat(cart): add repository interface and price service`

---

## Task 3: 基础设施层 — Redis + EF Core 双写实现

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/CartDbContext.cs`
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs`
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisCartCache.cs`
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs`

- [ ] 实现 `CartDbContext`（DbSet<Cart>，引用 BaseDbContext）
- [ ] 实现 `EfCoreCartRepository`（EF Core 持久化，购物车以 UserId 为唯一键）
- [ ] 实现 `RedisCartCache`（Redis Hash 存储购物车，读写穿透策略，TTL 7 天）
- [ ] 实现 `CartPriceService`（防腐层实现，调用商品域 API 查询 SKU 价格）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证双写一致性
- [ ] 提交：`feat(cart): add Redis cache and EF Core repository`

---

## Task 4: 应用层 — 购物车管理用例

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Application/ICartAppService.cs`
- Create: `src/Services/Cart/Leno.Cart.Application/DTOs/CartDto.cs`
- Create: `src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs`

- [ ] 定义 `ICartAppService` 接口（AddItemAsync、UpdateQuantityAsync、RemoveItemAsync、SelectItemsAsync、GetCartAsync、PreviewCheckoutAsync）
- [ ] 实现 `AddItemAsync`（加载购物车→调用防腐层校验 SKU 可售→添加项→保存）
- [ ] 实现 `UpdateQuantityAsync`/`RemoveItemAsync`/`SelectItemsAsync`
- [ ] 实现 `GetCartAsync`（从 Redis 读取，附加实时价格与库存状态）
- [ ] 实现 `PreviewCheckoutAsync`（按卖家分组返回选中项预览，含价格与优惠试算）
- [ ] 编写 DTO 与 FluentValidation 输入校验
- [ ] 编写单元测试覆盖用例
- [ ] 提交：`feat(cart): add cart application service`

---

## Task 5: 表现层 — API 控制器

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs`

- [ ] 实现 `CartsController`（GET /api/cart、POST /api/cart/items、PUT /api/cart/items/{skuId}、DELETE /api/cart/items/{skuId}）
- [ ] 实现 POST /api/cart/items/select（批量选中/取消选中）
- [ ] 实现 POST /api/cart/preview（结算预览，按卖家分组）
- [ ] 配置 JWT 鉴权（仅买家可操作自身购物车）
- [ ] 编写 API 集成测试覆盖购物车全流程
- [ ] 提交：`feat(cart): add cart API controller`

---

## Task 6: 消费订单域事件清空已结算项

**文件:**
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Consumers/OrderEventConsumer.cs`

- [ ] 实现 `OrderCreatedEvent` 消费者（提取 SourceCartItemId 列表，清空购物车已结算项）
- [ ] 幂等消费（以 EventId 去重，重复消费不重复清空）
- [ ] 编写集成测试验证事件消费
- [ ] 提交：`feat(cart): add OrderCreatedEvent consumer for clearing purchased items`

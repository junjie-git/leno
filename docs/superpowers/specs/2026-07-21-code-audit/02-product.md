# Product（商品域）代码分析报告

## 概述

- **扫描范围**：
  - `/workspace/src/Services/Product/Leno.Product.Domain/`
  - `/workspace/src/Services/Product/Leno.Product.Application/`
  - `/workspace/src/Services/Product/Leno.Product.Infrastructure/`
  - `/workspace/src/Services/Product/Leno.Product.Api/`
- **严格排除**：Tests 目录（扫描范围内无）、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`
- **代码行数**：约 6751 行业务代码（Domain 1884 行 26 文件 / Application 2034 行 37 文件 / Infrastructure 2068 行 26 文件 / Api 765 行 9 文件，共 98 个 .cs 文件）
- **问题总数**：🔴 高 5 / 🟡 中 10 / 🟢 低 5

---

## 🔴 高风险问题

### 1. ProductUpdatedDomainEvent 已注册翻译但 SPU 聚合永不抛出 — 集成事件契约断裂

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/EventBus/ProductIntegrationEventMapper.cs#L27-L29`
  - `file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L262-L324`（`UpdateInfo`/`UpdateSpecs`/`AddSku` 均未抛出该事件）
  - `file:///workspace/src/Services/Product/Leno.Product.Domain/Events/ProductUpdatedDomainEvent.cs#L1-L32`（事件类已定义但成为孤儿）
- **类别**：B - 事件契约一致性 / A - 异步消息可靠性
- **根因**：`ProductIntegrationEventMapper` 第 27-29 行注册了 `ProductUpdatedDomainEvent → ProductUpdatedEvent` 翻译器，注释明确说明消费方为"购物车域刷新展示快照、搜索域同步 ES 读模型"。但 SPU 聚合的 `UpdateInfo`（262-288）、`UpdateSpecs`（293-298）、`AddSku`（303-324）三个变更方法均未调用 `AddDomainEvent(new ProductUpdatedDomainEvent(...))`，事件类已成为无人发布的孤儿。
- **影响**：
  - 卖家更新商品标题/主图后，购物车域内对应商品的展示快照永远不会刷新，买家看到的是旧标题与旧主图。
  - 搜索域 ES 读模型中 `Title`、`MainImageUrl` 字段永远停留在首次上架时的快照，搜索相关性评分与展示严重失真。
  - 与 `ProductPublishedEvent`（创建时同步全量）、`StockAdjustedEvent`（仅同步价格区间）形成读模型同步漏洞。
- **修复建议**：
  - 在 `SPU.UpdateInfo` 末尾追加 `AddDomainEvent(new ProductUpdatedDomainEvent(Id, ShopId, Title, MainImageUrl));`
  - `UpdateSpecs` 与 `AddSku` 同样需要发布（`AddSku` 影响 MinPrice/MaxPrice 读模型字段）
  - 或在 mapper 移除该 handler，明示"商品更新通过 SPU 局部字段同步读模型"策略，并在 `StockAdjustedEventConsumer` 之外新增专用同步消费者
- **影响范围**：购物车域、搜索域、商品读模型

### 2. ProductUniquenessChecker TOCTOU 竞态 + DB 缺唯一约束 — SKU 编码与店铺内标题可重复

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Services/ProductUniquenessChecker.cs#L20-L57`
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Migrations/20260717174853_InitialCreate.cs#L192-L210`
- **类别**：A - 并发与边界 / C - 缺失索引
- **根因**：
  - `IsSkuCodeUniqueAsync` 与 `IsTitleUniqueInShopAsync` 是经典的 check-then-act 模式，两个并发请求可同时通过唯一性检查然后双双 Insert，导致重复数据。
  - 迁移文件第 192-195 行 `ix_skus_sku_code` 是**非唯一索引**，仅 `ix_stock_baselines_sku_id`（217-221）有 `unique: true`。
  - `ix_spus_shop_id`（207-210）也是非唯一索引，无 `(shop_id, title)` 复合唯一索引。
- **影响**：
  - SKU 编码全局重复会破坏下游订单域、库存域的 SKU 引用解析。
  - 同店铺内商品标题重复违反业务规则，但数据库层面无最后防线。
  - 高并发卖家批量导入 SKU 时极易触发。
- **修复建议**：
  - 迁移追加 `migrationBuilder.CreateIndex("ix_skus_sku_code_unique", "skus", "sku_code", unique: true);`
  - 迁移追加 `(shop_id, title)` 复合唯一索引
  - 应用层在 `ProductDomainException("SPU_SKU_CODE_GLOBAL_DUPLICATE")` 之外，捕获 `DbUpdateException` 并转换为友好的领域异常
- **影响范围**：商品域、订单域、库存域

### 3. StockBaseline.Replenish 发布事件时 ProductId=Guid.Empty — 下游同步失效

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs#L76`
  - 对照 `file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L454`（正确传 `Id`）
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Consumers/StockAdjustedEventConsumer.cs#L37`（消费侧用 `integrationEvent.ProductId` 反查 SPU）
- **类别**：A - 异步消息可靠性 / B - 事件契约一致性
- **根因**：`StockBaseline.Replenish` 第 76 行 `AddDomainEvent(new StockAdjustedDomainEvent(Id, SkuId, Guid.Empty, AvailableQty, qty, DateTime.UtcNow));`，第三参数 `ProductId` 传 `Guid.Empty`。对照 SPU.UpdateStock 第 454 行 `AddDomainEvent(new StockAdjustedDomainEvent(Id, skuId, Id, newStock, delta, DateTime.UtcNow));` 传真实 `Id`。同一集成事件 `StockAdjustedEvent` 在不同入口产出不一致的 ProductId 值。
- **影响**：
  - `StockAdjustedEventConsumer` 第 37 行通过 `integrationEvent.ProductId` 调用 `_spuRepository.GetByIdAsync`，传入 Guid.Empty 永远返回 null，warning 后 return，**ES 读模型价格区间永远不会因卖家补货而更新**。
  - 订单域消费 `StockAdjustedEvent` 时若以 ProductId 做关联索引或聚合，将丢失商品归属。
  - 即便 SPU.UpdateStock 路径正常，两个入口产出的 ProductId 不一致破坏了集成事件契约。
- **修复建议**：
  - `StockBaseline` 增加 `ProductId` 字段（创建时由应用层注入），`Replenish` 发布事件时传入真实 ProductId
  - 或 `StockAdjustedDomainEvent` 仅保留 `SkuId`，下游需要 ProductId 时通过 `ISPURepository.GetBySkuIdAsync` 反查（增加一次 DB 调用，但消除字段冗余）
- **影响范围**：商品读模型、订单域

### 4. EfCoreSPURepository.UpdateAsync 仅 Attach 不标记 Modified — ShopEventConsumer 流程下状态变更不持久化

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs#L105-L114`
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Consumers/ShopEventConsumer.cs#L52-L68`
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs#L55`（`QueryAsync` 使用 `AsNoTracking`）
- **类别**：A - 状态机流转 / 事务边界
- **根因**：`EfCoreSPURepository.UpdateAsync` 仅在实体 `Detached` 时调用 `Attach`，未调用 `_context.Update(aggregate)` 或 `_context.Entry(aggregate).State = EntityState.Modified`。Attach 后实体的当前值即被快照为原始值，EF Core ChangeTracker 检测不到差异。

  典型 SPUAppService 流程下（如 `SPUAppService.UpdateAsync`），实体由 `GetByIdAsync`（第 23-24 行无 AsNoTracking）加载，已被跟踪，Attach 是 no-op，变更正常保存。

  但 `ShopEventConsumerBase.ProcessBatchAsync`（第 52-57 行）通过 `QueryAsync`（第 55 行 AsNoTracking）加载 SPU，调用 `spu.SuspendByShop()` 等方法变更 Status，再调用 `UpdateAsync(spu, ct)` 仅 Attach。`SaveEntitiesAsync` 时 ChangeTracker 认为无任何 Modified 实体，**不会发出 UPDATE 语句**。

- **影响**：
  - `ShopSuspendedEvent` 消费后 SPU 仍为 OnSale，店铺暂停语义完全失效，被暂停店铺的商品仍可被下单。
  - `ShopResumedEvent` 消费后 SPU 仍为 ShopSuspended，店铺恢复后商品仍不可售。
  - `ShopClosedEvent` 流程下 `TakeDownForShopClosure` 会 `AddDomainEvent(new ProductTakenDownDomainEvent(...))`，Outbox 会写入并发布下架事件（购物车域、搜索域会响应），但 SPU.Status 仍为 OnSale —— 形成**事件与状态不一致**的脏数据。
  - 由于幂等存储（Redis SET NX）已标记事件处理完成，重试不会触发，问题不可恢复。
- **修复建议**：
  - `UpdateAsync` 改为 `_context.SPUs.Update(aggregate);`（强制标记所有属性为 Modified），需评估对 rowversion 乐观并发的影响
  - 或在 `ShopEventConsumerBase.ProcessBatchAsync` 内改用带跟踪的查询替代 `QueryAsync`（AsNoTracking），并放弃分页改为流式处理
  - 推荐方案：所有 `EfCore*Repository.UpdateAsync` 统一改为 `_context.Update(aggregate);`，与 Generic Repository 通用模式对齐
- **影响范围**：商品域、店铺域联动、订单域（可售性判断失效）

### 5. ProductGrpcService 使用 Guid.GetHashCode() 做 int64 映射 — 跨进程碰撞

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L142`
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L150`
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L167-L168`
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L174`
- **类别**：A - 边界条件 / B - 集成契约
- **根因**：`(long)dto.SkuId.GetHashCode()`、`(long)dto.SpuId.GetHashCode()`、`(long)dto.SellerId.GetHashCode()` 将 Guid 映射为 int64 字段。`Guid.GetHashCode()` 返回 32 位 int，不同 Guid 哈希碰撞概率为 1/2^32，在百万级 SKU 规模下必然发生碰撞（生日悖论下约 65k GUID 即开始碰撞）。代码注释自承 `// POC 简化：Guid→int64 映射，生产化改为 string`。
- **影响**：
  - 旧 gRPC 客户端读取 `SkuId` 字段时可能取到与预期不同的 SKU 数据。
  - 跨进程不一致：不同 .NET 运行时版本/架构下 `GetHashCode` 实现可能不同，A 进程写入的 int64 在 B 进程无法还原为原 Guid。
  - 订单域若以 int64 SkuId 做幂等键或缓存键，将出现串单。
- **修复建议**：
  - 强制客户端使用 `SkuIdStr`/`SpuIdStr`/`SellerIdStr` 字段，`int64` 字段在 proto 中标 `deprecated`
  - 短期内无法迁移时，将映射改为稳定算法：取 Guid 字节序列前 8 字节转 int64
  - 完成迁移后移除 int64 字段
- **影响范围**：所有调用 Product gRPC 的下游域（订单、购物车、促销）

---

## 🟡 中风险问题

### 6. ProductSearchService 价格区间过滤仅校验 MinPrice，未校验 MaxPrice 重叠

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs#L109-L123`
- **类别**：A - 边界条件 / C - 大对象扫描逻辑
- **根因**：`BuildFilters` 在 minPrice/maxPrice 任一存在时，对 `MinPrice` 字段做 `NumberRangeQuery`：
  ```csharp
  var range = new NumberRangeQuery(Infer.Field<ProductReadModel>(f => f.MinPrice));
  if (minPrice.HasValue) range.Gte = (double)minPrice.Value;
  if (maxPrice.HasValue) range.Lte = (double)maxPrice.Value;
  ```
  这意味着 `maxPrice=150` 会过滤 `MinPrice <= 150` 的商品。若一个商品 `MinPrice=50, MaxPrice=200`，会通过过滤（MinPrice=50 ≤ 150），但其实际价格区间上限 200 超出用户期望的 150。
- **影响**：用户筛选 100-150 元商品时，会看到实际价格 200 元的商品，转化率下降；反向场景下异常数据也会被错误纳入。
- **修复建议**：使用 ES 的 `bool`+`range` 组合：`MinPrice <= maxPrice && MaxPrice >= minPrice`（区间相交）
- **影响范围**：买家端搜索体验

### 7. ProductSearchService sort 参数被静默忽略

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs#L40`
- **类别**：B - 接口契约一致性
- **根因**：第 40 行 `_ = sort;` 显式丢弃 sort 参数，注释为"排序由读模型仓储默认相关性得分；预留扩展点"。但 `ProductSearchQueryDto.Sort` 字段对外暴露，前端传入 `price_asc`/`price_desc`/`sales_desc` 等值会被静默吞掉，无任何警告或日志。
- **影响**：前端按价格升降序功能完全失效，但接口契约不报错，QA 难以发现；运营无法按销量排序。
- **修复建议**：短期 sort 参数为空或无效时 log warning，明确支持 `relevance`（默认）；中期在 ES 查询中实现 `price_asc`/`price_desc` 排序
- **影响范围**：买家端搜索、运营后台

### 8. ProductInternalQueryService.GetSkuInfosBatchAsync 循环逐条查询 — N+1

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs#L52-L67`
- **类别**：C - N+1 查询
- **根因**：`GetSkuInfosBatchAsync` 遍历 `skuIds`，对每个 skuId 调用 `GetSkuInfoAsync`（第 59 行），而 `GetSkuInfoAsync` 内部调用 `_spuRepository.GetBySkuIdAsync(skuId, ct)` 触发一次 DB 查询。批量 100 个 SKU 会触发 100 次 SPU 查询 + 100 次 SKU 内存查找。
- **影响**：订单域批量校验购物车 SKU 时延迟显著（50 SKU 约 50 次 DB round-trip，约 250-500ms 额外延迟）；数据库连接池压力倍增。
- **修复建议**：在 `ISPURepository` 增加 `GetBySkuIdsAsync(IReadOnlyCollection<Guid> skuIds, ct)`，单次 SQL `WHERE s.Id IN @skuIds`（Include SKU），返回 `Dictionary<SkuId, SPU>`
- **影响范围**：所有跨域批量查询 SKU 的场景（订单、购物车）

### 9. SpuReviewSummaryConsumer 增量评分计算浮点漂移

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L52-L56`
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L121-L131`
- **类别**：A - 边界条件 / C - 数据一致性
- **根因**：增量更新公式 `existing.Score * existing.ReviewCount + integrationEvent.Rating`，再除以 `ReviewCount+1`。`Score` 是 `double`，每次 `Math.Round(..., 2)` 后存回，多次增量后：
  - 加权累计值 `existing.Score * existing.ReviewCount` 不等于真实总评分（每次 round 引入误差）。
  - 隐藏评价时 `existing.Score * existing.ReviewCount - integrationEvent.Rating` 反推总评分，与提交时的累计值偏差被放大。
  - 千次评价后 Score 可能漂移 ±0.05，影响商品排序与口碑展示。
- **影响**：商品评分展示与评价域聚合根的实际评分不一致；极端场景下 Score 可能变为负数或超过 5（第 115-119 行有部分保护但仅覆盖 ReviewCount<=0）。
- **修复建议**：ProductReadModel 增加 `TotalScore` 字段（double，不 round），仅展示时计算 `Math.Round(TotalScore / ReviewCount, 2)`；或定期从评价域全量重算同步（compensating action）
- **影响范围**：商品读模型评分

### 10. SpuReviewSummaryConsumer.cs:151 TODO 占位 — ReviewModeratedEvent 评分同步未实现

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L151-L154`
- **类别**：A - 异常处理 / 占位违反代码完整性契约
- **根因**：第 151-154 行明确 TODO 注释：
  ```
  // TODO: ReviewModeratedEvent 当前未实现评分同步消费者。
  // 该事件仅含 ReviewId/Status/Action，缺少 SpuId 与 Rating，
  // 需评价与售后域补全字段或商品域查询评价仓储后才能增量更新读模型。
  ```
  违反"零占位容忍度"规则。`ReviewModeratedEvent` 是评价域发布的事件（审核驳回/通过/申诉），商品域读模型完全无消费者订阅。
- **影响**：评价被审核驳回后，商品评分读模型仍包含该评价，买家看到错误的评分；评价申诉成功恢复后，读模型未恢复。
- **修复建议**：短期与评价域对齐 `ReviewModeratedEvent` schema，补充 `SpuId` 与 `Rating` 字段；中期商品域实现 `SpuReviewModeratedSummaryConsumer`，根据 Action（Approve/Reject/Appeal）分别走 Hidden/Submitted 流程
- **影响范围**：商品读模型、评价审核流程

### 11. Money 值对象允许 amount=0 与 SKU 域 price>0 不一致

- **位置**：
  - `file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L32-L35`
  - `file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L413-L416`
- **类别**：B - 不变量一致性
- **根因**：`Money.Create` 仅校验 `amount < 0`，允许 `amount = 0`。但 SPU 的 `AdjustPrice` 第 413-416 行校验 `newPrice.Amount <= 0` 抛异常。共享内核 Money 与 Product BC 的不变量不一致。
- **影响**：不同聚合（SKU 价格、PriceHistory、Cart 总价、Order 总价）对 0 元的语义不统一；"0 元样品"或"赠品"在 SKU 不可创建，但在 Money 值对象可创建，开发者认知混乱。
- **修复建议**：明确"金额可零"的语义：Money 允许 0，各 BC 自行决定是否拒绝 0；或在 Money 增加 `RequirePositive()` 方法，SKU 价格场景调用 `Money.Create(...).RequirePositive()`
- **影响范围**：共享内核、商品域、订单域

### 12. ProductGrpcService PriceCents 截断非四舍五入

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L154`
  - `file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L171`
- **类别**：A - 边界条件 / 金额计算
- **根因**：`PriceCents = (long)(sku.Price * 100)` 与 `PriceCents = (long)(dto.Price * 100)` 使用显式 cast 截断小数，而非 `Math.Round(sku.Price * 100, MidpointRounding.AwayFromZero)`。`sku.Price` 是 `decimal`，Money.Create 时已 round 到 2 位小数，但 * 100 后仍可能因浮点表示引入截断误差（如 19.99 * 100 = 1998.999... → 截断为 1998 而非 1999）。
- **影响**：下游订单域若以 PriceCents 做金额计算，可能少收 1 分钱；财务对账时订单总额与商品标价不符。
- **修复建议**：改为 `PriceCents = (long)Math.Round(sku.Price * 100m, MidpointRounding.AwayFromZero);`
- **影响范围**：订单域金额计算

### 13. PriceHistory.Create reason 永远为 null，ChangedBy 永远为 string.Empty — 审计信息缺失

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L237`
  - `file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L366-L374`（`ToPriceChangeRecordDto`）
- **类别**：A - 审计/可追溯性 / B - 不变量
- **根因**：
  - 第 237 行 `PriceHistory.Create(spuId, skuId, oldPrice, dto.Price, reason: null, dto.Currency)` 显式传 `reason: null`，未将 `AdjustPriceDto` 中的 `Reason` 字段（若存在）透传。
  - 第 366-374 行 `ToPriceChangeRecordDto` 硬编码 `ChangedBy = string.Empty`，丢失了 `SPUAppService.AdjustPriceAsync` 收到的 `changedBy` 参数。
- **影响**：价格审计无法回答"谁改的、为什么改"，运营调查价格异常时缺关键证据；监管要求（如电商法价格透明）无法满足。
- **修复建议**：`PriceHistory.Create` 增加 `changedBy` 参数，应用层透传；`AdjustPriceDto` 增加 `Reason` 字段并校验非空，应用层透传；`ToPriceChangeRecordDto` 返回 `history.ChangedBy`
- **影响范围**：商品域价格审计、合规

### 14. StockBaseline.SyncDeducted 异常在状态赋值后抛出 — 聚合内存状态不一致

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs#L102-L122`
- **类别**：A - 状态机一致性
- **根因**：`SyncDeducted` 在第 112 行 `AvailableQty -= delta`、第 116 行 `DeductedQty = deductedQty` 之后，第 118-121 行才校验 `AvailableQty < 0` 并抛异常。异常抛出前聚合状态已被修改，调用方若捕获异常并继续操作同一聚合实例，会看到非法状态。
- **影响**：应用层若误用 try-catch 吞异常（当前代码未见，但是隐患），聚合状态泄漏；单元测试时若直接断言异常后聚合字段，结果不可预测。
- **修复建议**：改为先校验后赋值：
  ```csharp
  var newAvailable = AvailableQty - delta;
  if (newAvailable < 0) throw new ProductDomainException(...);
  AvailableQty = newAvailable;
  DeductedQty = deductedQty;
  ```
  同样模式适用于 `Replenish`、`SyncReserved`、`SyncReleased`
- **影响范围**：商品域库存聚合

### 15. ProductReadModelSyncConsumer 默认 "CNY" 币种硬编码 — 多币种场景出错

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs#L42`
- **类别**：B - 配置硬编码 / C - 数据一致性
- **根因**：第 42 行 `var currency = spu.SKUs.FirstOrDefault()?.Price.Currency ?? "CNY";`，当 SPU 无 SKU 时（理论不应出现，因为 Approve 校验 EnsureHasSkus）fallback 到 "CNY"。但若首个 SKU 的币种与其他 SKU 不一致（多币种店铺），仅取首个，读模型 `Currency` 与实际多币种 SKU 不符。
- **影响**：跨境电商场景下，部分 SKU 为 USD、部分为 CNY 时，读模型显示币种错误；买家端按币种筛选失效。
- **修复建议**：ProductReadModel 增加 `Currencies` 数组字段，列出所有 SKU 的币种集合；或强制 SPU 创建时校验所有 SKU 同币种（聚合不变量）
- **影响范围**：商品读模型、跨境交易

---

## 🟢 低风险问题

### 16. 多处 [Obsolete] 双轨方法/路由未明确下线时间点

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L166`（`GetByIdAsync`，2026-08-01 下线）
  - `file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L174`（`QueryProductsAsync`，2026-08-01 下线）
  - `file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L24-L25`（双路由，"1 周后下线"，未给具体日期）
  - `file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L41-L42`
- **类别**：B - 接口契约
- **影响**：双轨期过长增加维护成本；InternalProductsController 的"1 周"未明确起算日期，可能长期遗留
- **修复建议**：统一为具体日期，CI 增加 Obsolete 检测告警

### 17. SearchController 绕过 CQRS QueryHandler 直接调用 SearchService

- **位置**：
  - `file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs#L35-L44`
  - `file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L72`（`AddQueryHandlers` 已注册但未被使用）
- **类别**：B - CQRS 职责混乱
- **影响**：Application 层注册了 `ProductSearchQueryHandler`，但 Controller 直接调 `IProductSearchService`，CQRS 读侧抽象空转，后续扩展（如缓存、权限过滤）无法统一拦截
- **修复建议**：Controller 改为通过 `IQueryHandler<ProductSearchQuery, ProductSearchResult>` 调用

### 18. ProductReadModelAccessor 不返回 SKU 列表 — 读侧 vs 写侧信息丢失

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelAccessor.cs#L32-L50`
- **类别**：B - 读模型与聚合一致性
- **影响**：`ProductDetailResult` 仅含 MinPrice/MaxPrice，无 SKU 列表；买家端商品详情页若走 CQRS 读侧，无法展示 SKU 选择器，必须回退到写侧 `SPUAppService.GetByIdAsync`（已 Obsolete）
- **修复建议**：ProductReadModel 增加 `Skus` 嵌套文档，Accessor 返回完整列表

### 19. ToPriceChangeRecordDto 字段映射不完整

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L366-L374`
- **类别**：B - DTO 契约
- **影响**：`SkuId` 被转为 string，但 `OldPrice`/`NewPrice` 直接返回 decimal；与 API 响应中其他 DTO（如 `SkuDto.Id` 为 Guid）风格不一致
- **修复建议**：统一 DTO 字段类型，`SkuId` 保留 Guid

### 20. SKU 表 ix_skus_sku_code 是非唯一索引（与高风险 #2 关联，但作为索引设计本身是低风险）

- **位置**：`file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Migrations/20260717174853_InitialCreate.cs#L192-L195`
- **类别**：C - 索引设计
- **影响**：与 #2 关联；单独看是索引唯一性设计缺陷
- **修复建议**：见 #2

---

## BC 健康度评分

| 维度 | 评分(0-5) | 说明 |
| --- | --- | --- |
| 功能正确性 | 2.0 | 5 个高风险问题：集成事件契约断裂（#1）、TOCTOU 竞态无 DB 兜底（#2）、ProductId=Guid.Empty 致下游失效（#3）、ShopEventConsumer 状态变更不持久化（#4）、gRPC Guid 碰撞（#5）。状态机与不变量设计本身良好，但落地实现存在系统性缺陷。 |
| DDD 合规 | 3.0 | 聚合根封装、ACL（IProductQueryService/IProductUniquenessChecker/IProductReadModelAccessor）、Outbox+幂等、CQRS 端口设计整体合规。扣分项：ProductUpdatedDomainEvent 死代码、Money 不变量跨 BC 不一致、SearchController 绕过 QueryHandler、读模型信息丢失、双路由/双方法 Obsolete 治理不彻底。 |
| 性能与可靠性 | 2.5 | N+1 批量查询（#8）、浮点漂移（#9）、价格过滤逻辑错误（#6）、sort 静默吞掉（#7）、TODO 占位（#10）、金额截断（#12）、审计缺失（#13）。Outbox/Redis 幂等/MassTransit 重试基础能力健全，但消费侧逻辑细节多处需修复。 |

---

## 总结与优先修复建议

1. **立即修复**（阻断性）：
   - #4 ShopEventConsumer 流程下 SPU 状态不持久化 → 影响"店铺暂停/关闭"核心业务流程
   - #3 StockBaseline.Replenish ProductId=Guid.Empty → 影响补货后读模型同步
   - #1 ProductUpdatedDomainEvent 未抛出 → 影响商品更新后跨域同步

2. **本周内修复**（数据一致性）：
   - #2 DB 唯一索引缺失 → 高并发下数据重复风险
   - #5 gRPC GetHashCode 碰撞 → 规模化后必然发生
   - #6 价格区间过滤逻辑 → 影响搜索转化
   - #8 N+1 查询 → 影响订单域性能

3. **迭代内修复**（代码质量）：
   - #10 TODO 占位违反代码完整性契约
   - #11 Money 不变量不一致
   - #12 金额截断
   - #13 价格审计缺失
   - #14 聚合状态先赋值后校验

4. **长期治理**：
   - #16-#20 Obsolete 治理、CQRS 路由统一、读模型完整性、DTO 一致性

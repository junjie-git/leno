# Cart 购物车域代码静态分析报告

> 扫描日期：2026-07-21  
> 扫描范围：src/Services/Cart/Leno.Cart.{Api,Application,Domain,Infrastructure}/  
> 排除项：Tests 目录、Migrations Designer、ModelSnapshot、Generated

## 1. 概览

- **业务代码行数**：约 3000 行（不含 Tests/Migrations Designer/ModelSnapshot）
- **问题统计**：🔴 高 5 项 / 🟡 中 15 项 / 🟢 低 10 项
- **风险评级**：🔴 高 = 数据一致性破坏/资损/安全漏洞/可用性故障；🟡 中 = 边界场景 Bug/性能隐患；🟢 低 = 代码质量/可维护性

## 2. 🔴 高风险问题

### 2.1 SkuAddedToCartEvent/SkuRemovedFromCartEvent 无处理器，购物车-SKU 反向索引永远不被维护

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L90 ; file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L131 ; file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/EventBus/CartIntegrationEventMapper.cs#L19-L21
- **类别**：A4 事件发布完整性 / A5 内部事件无消费
- **现象**：
  - `Cart.AddItem` 在新增 SKU 时调用 `AddDomainEvent(new SkuAddedToCartEvent(...))`，`Cart.RemoveItem` 调用 `AddDomainEvent(new SkuRemovedFromCartEvent(...))`。
  - `CartIntegrationEventMapper` 显式声明这两个事件"不映射为集成事件，保持内部处理"。
  - 但 **整个 Cart.Infrastructure 中没有任何 `IDomainEventHandler` / `MediatR INotificationHandler` / `IInterceptor` 监听这两个事件去调用 `ICartSkuIndexService.AddAsync/RemoveAsync`**（全代码库 grep 仅在 Tests 中手动调用 `indexService.AddAsync`）。
  - `Leno.Infrastructure.Outbox.OutboxDbContextExtensions.SaveChangesWithOutboxAsync` 只翻译可映射为 `IIntegrationEvent` 的领域事件，这两个事件既不实现 `IIntegrationEvent` 也不在 mapper 中注册，会被 `ClearDomainEvents()` 直接丢弃。
- **影响**：
  - Redis 中的 `cart:sku:{skuId}` Set 永远为空。
  - `ProductTakenDownEventConsumer` / `ProductPublishedEventConsumer` / `ProductUpdatedEventConsumer` 中 `_indexService.GetCartIdsBySkuAsync(skuId)` 永远返回空列表，三个消费者实际什么也不做。
  - 商品下架后购物车项不会被标记 `IsValid=false`，用户仍可选中已被下架商品并进入结算预览，存在资损/超卖风险。
  - 与代码注释中标榜的"商品下架联动购物车"能力完全不符。
- **修复建议**：在 Infrastructure 层新增领域事件处理器（推荐在 `SaveEntitiesAsync` 之前的 `SaveChanges` 拦截器中处理，确保与持久化同事务）：
  ```csharp
  public sealed class CartSkuIndexDomainEventHandler :
      INotificationHandler<SkuAddedToCartEvent>,
      INotificationHandler<SkuRemovedFromCartEvent>
  {
      private readonly ICartSkuIndexService _indexService;
      public CartSkuIndexDomainEventHandler(ICartSkuIndexService indexService)
          => _indexService = indexService;

      public Task Handle(SkuAddedToCartEvent e, CancellationToken ct)
          => _indexService.AddAsync(e.SkuId, e.CartId, ct);

      public Task Handle(SkuRemovedFromCartEvent e, CancellationToken ct)
          => _indexService.RemoveAsync(e.SkuId, e.CartId, ct);
  }
  ```
  并在 `ServiceCollectionExtensions` 中注册；或在 `EfCoreUnitOfWork.SaveEntitiesAsync` 落库前显式遍历 `aggregate.DomainEvents` 调用 `ICartSkuIndexService`。

### 2.2 匿名购物车 TOCTOU 竞态 + Redis 异常静默吞掉导致数据丢失

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L34-L51 ; #L54-L69 ; #L72-L85 ; #L88-L101
- **类别**：A3 并发与幂等 / A7 异常吞掉 / A8 资源容错
- **现象**：
  - `GetAsync` `catch (Exception ex)` 后 `return null`，将"Redis 故障"伪装成"购物车不存在"。
  - `SaveAsync` / `RemoveAsync` / `RefreshTtlAsync` 全部 `catch (Exception ex)` 后仅 `LogWarning`，调用方无法感知失败。
  - 调用方 `AnonymousCartAppService.GetOrCreateCartAsync`（L137-L147）在 `GetAsync` 返回 null 后会创建新空购物车并 `SaveAsync` 覆盖原键 → **Redis 抖动期间用户购物车被静默清空**。
  - `AddItemAsync` / `UpdateQuantityAsync` 流程为"读-改-写"：并发请求读到同一份快照、各自修改、最后写回，**后写覆盖先写**，丢失其他并发操作的变更。无 WATCH/MULTI/Lua 脚本/版本号。
- **影响**：
  - 高并发加购场景数量丢失（如秒杀加购）。
  - Redis 网络抖动期间用户购物车数据被悄悄清空，无任何错误上报。
  - 与 `EfCoreCartRepository` 通过 `BaseDbContext` 配置的 `Version` rowversion 乐观锁形成强烈对比，用户购物车有并发保护而匿名购物车完全没有。
- **修复建议**：
  ```csharp
  // 1) 异常不应静默，应包装为可识别异常向上抛
  public async Task<CartAggregate?> GetAsync(string sessionId, CancellationToken ct = default)
  {
      try { /* ... */ }
      catch (RedisConnectionException ex)
      {
          _logger.LogError(ex, "Redis 不可用 SessionId={SessionId}", sessionId);
          throw new CartInfrastructureException("匿名购物车暂不可用", "CART_REDIS_UNAVAILABLE", ex);
      }
  }
  // 2) 使用 Redis 事务或 Lua 脚本实现 CAS
  //    或在 CartAggregate 上引入 Version 字段，序列化后随值一起写入，写入前 EXISTS+GET 比对
  ```

### 2.3 CartAppService.BuildCartDtoAsync 捕获了错误的异常类型，价格降级逻辑永不触发

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L219-L231
- **类别**：A7 异常处理 / A5 防腐层降级
- **现象**：
  ```csharp
  try {
      var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
      priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
  }
  catch (CartDomainException ex) {  // ← 永远不会进入
      _logger.LogWarning(ex, "购物车价格服务不可用，降级展示...");
      priceServiceUnavailable = true;
  }
  ```
  - `_priceService` 的实现 `CartPriceService` 继承 `AntiCorruptionBase`，其 `ExecuteAsync` 在 HTTP 非 2xx/网络异常/空响应时统一抛 **`AntiCorruptionException`**（参见 `AntiCorruptionBase.cs` L34/L41/L53/L76）。
  - **不是 `CartDomainException`**，catch 块永远不会命中。
  - 注释中描述的"购物车查看场景不因价格服务故障整体崩溃，降级展示并标记 PriceUnavailable"完全未实现。
- **影响**：
  - 商品域/internal API 任何抖动都会让 `GET /api/cart` 直接 500，所有买家看不到购物车。
  - 与代码注释、与 `PreviewCheckoutAsync` 的"硬拦截"设计形成矛盾，本应"查看降级 + 结算硬拦"的双层保护退化为"查看挂掉 + 结算硬拦"。
- **修复建议**：
  ```csharp
  catch (AntiCorruptionException ex)
  {
      _logger.LogWarning(ex, "购物车价格服务不可用，降级展示 UserId={UserId} ErrorCode={Code}",
          cart.UserId, ex.ErrorCode);
      priceServiceUnavailable = true;
  }
  ```

### 2.4 匿名购物车结算预览存在 0 元结算漏洞

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L100-L135 ; #L156-L196
- **类别**：A6 边界条件 / A5 防腐层
- **现象**：
  - `AnonymousCartAppService.BuildItemDto` (L179-L196) 在价格未命中时设置 `UnitPrice = 0, Title = string.Empty, Available = false`，**但没有设置 `PriceUnavailable = true`**（字段默认 false）。
  - `PreviewCheckoutAsync` (L113-L126) 计算 `SubtotalAmount = items.Sum(i => i.Subtotal)` —— 缺价项 `Subtotal = 0 * Quantity = 0`，被当作 0 元参与合计。
  - 与用户购物车版本（`CartAppService.PreviewCheckoutAsync` L133-L136 显式 `if (...Any(i => i.PriceUnavailable)) throw new CartDomainException(...)`）严重不一致。
  - `BuildCartDtoAsync` (L173) `SelectedTotalAmount = itemDtos.Where(i => i.IsSelected).Sum(i => i.Subtotal)` 同样把缺价选中项按 0 元累加。
- **影响**：
  - 价格服务部分失败时，匿名用户看到的结算合计可能小于实际应付金额，可能被前端误判为可下单，造成资损。
  - 前端无法通过 `PriceUnavailable` 标记区分"免费商品"与"价格加载失败"，无法正确拦截。
- **修复建议**：与 `CartAppService` 对齐：
  ```csharp
  private static CartItemDto BuildItemDto(CartItem item, Dictionary<Guid, SkuPriceSnapshot> priceMap)
  {
      if (!priceMap.TryGetValue(item.SkuId, out var snapshot))
      {
          return new CartItemDto { /* ... */ UnitPrice = 0, PriceUnavailable = true };
      }
      // ...
  }
  // PreviewCheckoutAsync 末尾：
  if (groups.SelectMany(g => g.Items).Any(i => i.PriceUnavailable))
      throw new CartDomainException("部分商品价格加载失败，暂不可结算", "CART_PRICE_UNAVAILABLE");
  ```

### 2.5 聚合不变量违反：AddItem 不校验品类上限，maxVariety=50 仅在 MergeFrom 中生效

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L67-L91 ; #L229-L257
- **类别**：A1 聚合不变量 / B2 业务规则位置
- **现象**：
  - `AddItem(Guid, int, Guid)` 仅校验 SkuId 非空与合并后数量 ≤99，**没有任何"购物车品类数量上限"校验**。
  - `maxVariety = 50` 这个常量被硬编码在 `MergeFrom` 方法内部 (L232)，仅对匿名购物车合并场景生效。
  - 直接通过 `POST /api/cart/items` 调用 `AddItemAsync` → `cart.AddItem` 的路径完全不校验品类上限，用户可加 100/1000 个不同 SKU。
- **影响**：
  - 聚合不变量被绕过，购物车可无限膨胀，结算预览/查询性能退化。
  - 单一聚合根加载大量 CartItem 时 EF Core 跟踪开销激增。
  - 违反 DDD"聚合不变量由聚合根统一保证"原则。
- **修复建议**：将 `maxVariety` 提升为聚合根常量并在 `AddItem` 内强制：
  ```csharp
  private const int MaxVariety = 50;
  public void AddItem(Guid skuId, int quantity, Guid sellerId)
  {
      // ...
      var existing = _items.FirstOrDefault(i => i.SkuId == skuId);
      if (existing is not null) { /* 合并 */ return; }
      if (_items.Count >= MaxVariety)
          throw new CartDomainException($"购物车品类数量已达上限 {MaxVariety}", "CART_VARIETY_LIMIT");
      // ...
  }
  ```

## 3. 🟡 中风险问题

### 3.1 MergeAnonymousCartAsync 跨存储非原子操作，Redis 删除失败导致重复合并数量翻倍

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L172-L184
- **类别**：A3 幂等 / A4 跨存储事务
- **现象**：流程为 `SaveEntitiesAsync(DB)` → `RemoveAsync(Redis)`。`RemoveAsync` 异常被静默吞掉（见 2.2）。若 DB 保存成功但 Redis 删除失败，下次同一 `anonymousId` 触发合并时，匿名购物车仍存在，`MergeFrom` 再次把每项数量加到用户购物车上。
- **影响**：用户购物车数量被翻倍累加，可能触发 `CART_QTY_OVERFLOW` 异常或直接超卖。
- **修复建议**：在 `CartMergedEvent` 中携带 `anonymousId` 与 `mergedItemCount`，由消费者端在确认事件发布成功后再删 Redis；或使用 `CartMergeRecord` 表记录已合并的 anonymousId 实现幂等。

### 3.2 ProductEventConsumer 三个消费者 N+1 查询 + UpdateAsync 滥用

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L49-L67 ; #L105-L124 ; #L161-L198 ; file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs#L38-L42
- **类别**：C1 N+1 / C3 循环内查询
- **现象**：每个 SKU 拿到 cartIds 后，`foreach (var cartId in batch) { var cart = await _cartRepository.GetByIdAsync(cartId, ct); ... await _cartRepository.UpdateAsync(cart, ct); }`。100 个购物车 = 100 次 SELECT + 100 次 `Update()`。`EfCoreCartRepository.UpdateAsync` 调用 `_context.Carts.Update(aggregate)`，对已跟踪实体强制将所有列标记为 Modified，生成全字段 UPDATE。
- **影响**：热门 SKU 上下架时，单事件可触发数百次 DB 往返，长时间占用 DbContext，可能阻塞消息管线。
- **修复建议**：批量加载 `await _context.Carts.Include(c => c.Items).Where(c => cartIds.Contains(c.Id)).ToListAsync(ct)`，移除对已跟踪实体的 `Update()` 调用。

### 3.3 ProductUpdatedEventConsumer 每 SKU 一次 HTTP 快照查询

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L166-L198
- **类别**：C3 循环内远程调用
- **现象**：`foreach (var skuId in integrationEvent.SkuIds) { snapshot = await _snapshotAntiCorruption.GetSkuSnapshotAsync(skuId, ct); ... }`。单事件 N SKU = N 次 HTTP/gRPC。
- **影响**：商品批量改价/改标题事件触发风暴式远程调用，可能拖垮商品域 internal API。
- **修复建议**：将 `IProductSnapshotAntiCorruption` 扩展为 `GetSkuSnapshotsAsync(IEnumerable<Guid>)` 批量接口（与 `ICartPriceService` 对齐）。

### 3.4 匿名购物车聚合 _domainEvents 永不清理，Redis JSON 单调增长

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L54-L69 ; file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs#L8-L17
- **类别**：C7 资源占用 / A4 事件生命周期
- **现象**：`AggregateRoot._domainEvents` 由 `AddItem`/`RemoveItem` 累积。`RedisAnonymousCartRepository.SaveAsync` 直接 `JsonSerializer.Serialize(cart)`，**包括 `_domainEvents`**。`ClearDomainEvents()` 只在 `SaveChangesWithOutboxAsync` 中调用，匿名购物车不走此路径。每次加购/移除都让序列化 JSON 增长。
- **影响**：长期使用的匿名购物车 Redis Value 可达数百 KB，网络/反序列化开销线性放大。
- **修复建议**：`SaveAsync` 序列化前调用 `cart.ClearDomainEvents()`；或在 `AnonymousCartAppService` 每次操作后显式清理。

### 3.5 CartSkuIndexService Redis Set 无 TTL，stale 索引永久驻留

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs#L27-L37 ; #L41-L49
- **类别**：C7 资源占用 / C4 缓存策略
- **现象**：`SetAddAsync` 不设置 Key 过期。当购物车被整体删除（如用户注销），`cart:sku:{skuId}` 中残留 cartId 永久存在。后续 `ProductEventConsumer` 拿到 stale cartId，`GetByIdAsync` 返回 null，徒劳查询。
- **影响**：Redis 内存单调增长，商品事件消费者无效 DB 查询累积。
- **修复建议**：为索引 Set 设置合理 TTL（如 30 天），或在 `ClearItemsBySourceIds` 完全清空购物车时同步 `RemoveAsync` 索引。

### 3.6 CartSkuIndexService 异常处理与 RedisAnonymousCartRepository 不一致

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs#L27-L49
- **类别**：A7 异常处理一致性
- **现象**：`AddAsync` / `RemoveAsync` / `GetCartIdsBySkuAsync` 不 catch 任何异常，Redis 故障直接抛 `RedisConnectionException` 上抛到 `ProductEventConsumer`。对比 `RedisAnonymousCartRepository` 全部静默吞掉。
- **影响**：策略不一致，难以推断 Redis 故障时的整体行为。`GetCartIdsBySkuAsync` 抛错会让整批商品下架事件消费失败，触发消息重试风暴。
- **修复建议**：统一异常策略：要么都"故障上抛 + 全局异常中间件兜底"，要么都"降级 + 上报指标"。

### 3.7 AnonymousCartsController 无鉴权 + 无限流，DoS 与 sessionId 泄露风险

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L12-L31 ; #L34-L85
- **类别**：A5 安全 / C8 失败保护
- **现象**：控制器无 `[Authorize]`、无 `[EnableRateLimiting]`。`POST /api/cart/anonymous` 每次创建一个 7 天 TTL 的 Redis Key，攻击者可无限调用造成 Redis OOM。sessionId 出现在 URL 路径（`{sessionId}/items/...`），被 access log、浏览器历史、Referer 头捕获后等同于持票人令牌泄露。
- **影响**：DoS 资源耗尽 + 会话劫持。
- **修复建议**：
  - 对 `POST /api/cart/anonymous` 加 IP 维度限流（如 10 次/分钟）。
  - sessionId 改为通过 `X-Cart-Session` 请求头或 Cookie 传递，不出现在 URL。
  - 创建匿名购物车时绑定客户端指纹（UA + IP 哈希），异常来源限流。

### 3.8 EfCoreCartRepository 读写未分离 AsNoTracking，读路径无谓跟踪

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs#L22-L31
- **类别**：C1 跟踪开销
- **现象**：`GetByIdAsync` 与 `GetByUserIdAsync` 都 `Include(c => c.Items)` 但无 `AsNoTracking()`。`CartAppService.GetCartAsync` / `PreviewCheckoutAsync` 等只读路径会强制 ChangeTracker 跟踪 Cart + 所有 CartItem，构建 DTO 后实体仍被跟踪至请求结束。
- **影响**：高 QPS 查询场景 ChangeTracker 内存与 CPU 开销显著。
- **修复建议**：拆分 `ICartQueryStore`（只读、AsNoTracking）与 `ICartRepository`（写、跟踪），或在仓储方法加 `asNoTracking` 参数。

### 3.9 Cart.AddItem 六参数重载 unitPrice 死参数 + 快照回退风险

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L104-L108
- **类别**：A6 死代码 / B2 业务规则
- **现象**：
  ```csharp
  public void AddItem(Guid skuId, string title, string mainImageUrl, decimal unitPrice, int quantity, Guid sellerId)
  {
      AddItem(skuId, quantity, sellerId);
      FindItem(skuId)?.RefreshDisplaySnapshot(title, mainImageUrl);
  }
  ```
  - `unitPrice` 参数被显式标注"接受但不持久化"，从不使用 —— 违反用户规则 §2"伪代码/存根/死参数"。
  - 调用 `RefreshDisplaySnapshot` 时若为新 SKU 没问题；若为已存在 SKU 合并数量，会用调用方传入的（可能旧的）`title/mainImageUrl` **覆盖** 聚合中可能已由 `ProductUpdatedEventConsumer` 刷新过的更新快照，造成回退。
  - 该重载在生产代码中无任何调用方（仅测试使用）。
- **影响**：违反"零占位"红线 + 潜在快照回退 Bug。
- **修复建议**：删除该重载；测试改用 `AddItem(skuId, quantity, sellerId)` + `RefreshDisplaySnapshot`。

### 3.10 CartInternalQueryService 金额转分截断而非四舍五入

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L31 ; #L33 ; #L47 ; #L50
- **类别**：A6 金额精度
- **现象**：`(long)(i.UnitPrice * 100)` 与 `(long)(cart.SelectedTotalAmount * 100)` 使用 `decimal → long` 显式转换，**向零截断**。`19.999m * 100 = 1999.9m`，转 long 得 1999，丢失 0.9 分。
- **影响**：跨 BC gRPC 查询返回的金额比实际少 1 分，订单域据此计算可能少收。
- **修复建议**：`(long)Math.Round(i.UnitPrice * 100m, MidpointRounding.AwayFromZero)`。

### 3.11 CartInternalQueryService.GetCartSnapshotAsync 永不返回 null，死代码

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L19-L35 ; file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L38-L41
- **类别**：A6 边界 / 代码质量
- **现象**：调用链 `_cartAppService.GetCartAsync` → `GetOrCreateCartAsync`，后者在购物车不存在时**创建**新空购物车。因此 `cart is null` 永远为 false，`GetCartSnapshotAsync` 永远不返回 null。`CartGrpcService` L38-L41 的 `throw new RpcException(StatusCode.NotFound)` 是不可达分支。
- **影响**：gRPC 客户端期待 404 语义实际拿到空快照；调用方可能基于"非 null 即有效"做出错误判断（如 Order BC 据此创建空订单）。
- **修复建议**：`GetCartSnapshotAsync` 应区分"不存在"与"空"，要么返回带 `Exists` 标志的 DTO，要么直接调用 `RequireCartAsync` 语义。

### 3.12 ClearSelectedItems 死代码 + 未发布 SkuRemovedFromCartEvent

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L193-L204
- **类别**：A4 事件完整性 / 代码质量
- **现象**：`ClearSelectedItems` 移除选中项时**不发布 `SkuRemovedFromCartEvent`**，与 `RemoveItem` 行为不一致（`RemoveItem` L131 发布事件）。即使 2.1 修复后，通过 `ClearSelectedItems` 清除的 SKU 也不会从反向索引移除。该方法当前无生产调用方。
- **影响**：若未来被启用，索引会出现 stale 条目；当前是死代码占空间。
- **修复建议**：要么删除该方法，要么在循环中 `AddDomainEvent(new SkuRemovedFromCartEvent(Id, item.SkuId))`。

### 3.13 CircuitBreakerState 单例工厂读取 IOptionsMonitor 时机错误

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L78-L87
- **类别**：C8 配置动态生效
- **现象**：KeyedSingleton 工厂在构造时读取 `IOptionsMonitor<AntiCorruptionOptions>.CurrentValue` 一次，熔断阈值（FailureThreshold/SuccessThreshold/OpenDurationSeconds）被冻结。Consul KV 运行时推送的新配置不生效。
- **影响**：运维侧调整熔断参数需重启服务，违背"Consul KV 热更新"的设计目标。
- **修复建议**：让 `CircuitBreakerState` 自己持有 `IOptionsMonitor<AntiCorruptionOptions>` 引用，每次状态变更前读取最新值。

### 3.14 CartAppService 多币种聚合错误

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L249 ; #L127 ; #L142
- **类别**：A6 边界 / 金额计算
- **现象**：`Currency = itemDtos.FirstOrDefault()?.Currency ?? "CNY"` 与 `groups.FirstOrDefault()?.Currency ?? "CNY"` 取第一项币种作为整单币种。若购物车混币种（CNY + USD），合计金额会以第一项币种标注但实际是不同币种数字相加，造成币种与金额不匹配。
- **影响**：跨境多币种场景金额错误。
- **修复建议**：按币种分组返回 `Dictionary<string, decimal>` 或拒绝混币种结算。

### 3.15 匿名购物车 BuildCartDtoAsync 不处理 AntiCorruptionException

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L156-L177
- **类别**：A5 防腐层 / A7 异常处理
- **现象**：`_priceService.GetSkuPricesAsync(skuIds, ct)` 直接 await，无 try/catch。价格服务故障时 `AntiCorruptionException` 直接冒泡到控制器层。与用户购物车版本（即便 2.3 修复后）的"降级展示"策略不一致。
- **影响**：商品域抖动期间匿名购物车所有端点 500。
- **修复建议**：与 `CartAppService.BuildCartDtoAsync` 保持一致的降级策略（修复 2.3 后），并设置 `PriceUnavailable = true`。

## 4. 🟢 低风险问题

### 4.1 RedisCartCache 注册但全局未被使用

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L150 ; file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Caching/RedisCartCache.cs
- **类别**：代码质量 / 死代码
- **现象**：`services.AddSingleton<RedisCartCache>();` 注册了缓存类，但全代码库 grep 仅有声明与注册，无任何 `RedisCartCache.GetAsync/SetAsync/RemoveAsync` 调用。
- **影响**：维护负担 + 误导后续开发者以为读路径有缓存。
- **修复建议**：要么真正接入读路径（CartAppService.GetCartAsync 先查缓存），要么删除该类与注册。

### 4.2 Cart.AddItem 与 MergeFrom 重复 FindItem

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L106-L107 ; #L238-L244
- **类别**：C1 性能
- **现象**：六参数 `AddItem` 先调用三参数 `AddItem`（内含 `FindItem`），随后又调用 `FindItem(skuId)`。`MergeFrom` 在 `AddItem` 调用前先 `FindItem` 判断是否需触发品类上限，`AddItem` 内部再次 `FindItem`。
- **影响**：每次加购多一次 O(N) 线性扫描。
- **修复建议**：提取 `TryGetItem` 内部方法，复用引用。

### 4.3 ConfigureAwait 使用不一致

- **文件**：仅 file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs#L59 ; file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs#L52 ; file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L36 ; #L55 使用
- **类别**：C6 异步规范
- **现象**：仅 gRPC 客户端类使用 `ConfigureAwait(false)`，应用层、仓储、控制器全部未使用。
- **影响**：ASP.NET Core 无 SynchronizationContext，无死锁风险，但库代码风格不一致。
- **修复建议**：统一规范——要么全部加，要么全部不加（推荐后者，.NET 10 已无需 ConfigureAwait(false)）。

### 4.4 CartItem.IsValid 字段初始化器与构造函数重复赋值

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs#L36 ; #L68
- **类别**：代码质量
- **现象**：`public bool IsValid { get; private set; } = true;` 字段初始化器已设为 true，构造函数 L68 又显式 `IsValid = true;`。
- **影响**：冗余但无 Bug。
- **修复建议**：删除构造函数中的重复赋值。

### 4.5 CartDbContextDesignTimeFactory 硬编码连接字符串含密码

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartDbContextDesignTimeFactory.cs#L15
- **类别**：安全 / 代码质量
- **现象**：`"Server=localhost,1433;Database=LenoCart;User Id=sa;Password=Leno@SqlServer2019;..."` 硬编码于源码。仅设计期使用，但仍会进版本库。
- **影响**：开发环境密码泄露到 Git 历史。
- **修复建议**：从环境变量或 `dotnet user-secrets` 读取。

### 4.6 匿名购物车 sessionId 暴露在 URL 路径

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L34 ; #L43 ; #L52 ; #L61 ; #L70 ; #L79
- **类别**：安全
- **现象**：所有匿名购物车操作以 `{sessionId}` 作为 URL 路径段。Access log、反向代理、浏览器历史、Referer 头均会记录。
- **影响**：sessionId 等同于持票人令牌，泄露即被劫持。
- **修复建议**：改为请求头 `X-Cart-Session` 或 Cookie。

### 4.7 MergeFrom 不跳过匿名购物车中的无效项

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L235
- **类别**：A2 状态转换 / 业务规则
- **现象**：`foreach (var item in anonymousCart.Items)` 未过滤 `IsValid == false` 的项，仍合并到用户购物车。
- **影响**：用户登录后购物车出现已下架商品。
- **修复建议**：`foreach (var item in anonymousCart.Items.Where(i => i.IsValid))`。

### 4.8 AnonymousCartAppService.GetCartAsync 刷新 TTL 被攻击者利用

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L44-L49
- **类别**：C7 资源占用
- **现象**：每次 `GetCartAsync` 都调用 `RefreshTtlAsync`，将 7 天 TTL 重置。攻击者定时访问可让一个匿名购物车永久驻留 Redis。
- **影响**：Redis 内存被无用匿名购物车长期占用。
- **修复建议**：限制 TTL 刷新次数，或仅在写操作时刷新。

### 4.9 ProductEventConsumer 三个消费者共享 DbContext 跨批次累积跟踪

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L54-L66 ; #L110-L123 ; #L184-L197
- **类别**：C1 跟踪累积
- **现象**：内层 `foreach (var cartId in batch)` 加载 100 个购物车全部由同一 Scoped DbContext 跟踪，`SaveEntitiesAsync` 后才清理。若某个购物车加载失败抛异常，整批回滚且之前批次已提交，重试时 idempotency 由 `MarkInvalid/MarkValid` 自身保证，但跟踪开销在批次内线性增长。
- **影响**：大批次场景 CPU/内存压力。
- **修复建议**：批处理后显式 `_context.ChangeTracker.Clear()` 或使用 `AsNoTracking` + 显式 `Update`。

### 4.10 AnonymousCartAppService.GetOrCreateCartAsync 在不存在时立即 SaveAsync 覆盖

- **文件**：file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L137-L147
- **类别**：A3 并发
- **现象**：`GetAsync` 返回 null 后立即 `CreateAnonymous` + `SaveAsync`。两个并发请求同时遇到 null 时都会创建并 SaveAsync，后者覆盖前者。虽然此时购物车为空无业务损失，但浪费一次 Redis 写。
- **影响**：轻微资源浪费。
- **修复建议**：使用 `SET NX` 原子创建，或接受现状。

## 5. 修复路线建议

| 优先级 | 问题数 | 建议周期 |
|-|-|-|
| P0（必修）| 5（2.1-2.5） | 3 天内 |
| P1（应修）| 15（3.1-3.15） | 2 周内 |
| P2（建议）| 10（4.1-4.10） | 1 个月内 |

**P0 顺序建议**：
1. 先修 2.1（反向索引无处理器）—— 这是其他商品联动逻辑生效的前提
2. 修 2.3（错误异常类型 catch）—— 影响所有买家查看购物车可用性
3. 修 2.4（匿名购物车 0 元结算漏洞）—— 资损风险
4. 修 2.5（聚合不变量）—— 数据完整性
5. 修 2.2（匿名购物车竞态+静默吞异常）—— 需重构 Redis 仓储，工作量最大

## 6. 附录：扫描覆盖的关键文件

### Domain 层
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Events/SkuAddedToCartEvent.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Events/CartMergedDomainEvent.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Exceptions/CartDomainException.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Repositories/ICartRepository.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Repositories/IAnonymousCartRepository.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Services/ICartPriceService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Services/ICartSkuIndexService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Domain/Leno.Cart.Domain.csproj

### Application 层
- file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/Validators/CartValidators.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/DTOs/CartDto.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/DTOs/CartItemDtos.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/DTOs/CheckoutPreviewDto.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/DTOs/SkuSnapshotDto.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/ICartAppService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/IAnonymousCartAppService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Application/Leno.Cart.Application.csproj

### Infrastructure 层
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartDbContext.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartDbContextDesignTimeFactory.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Caching/RedisCartCache.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/OrderCreatedEventConsumer.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/EventBus/CartIntegrationEventMapper.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/CartPriceDispatcherAdapter.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Migrations/20260717174927_InitialCreate.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Leno.Cart.Infrastructure.csproj

### Api 层
- file:///workspace/src/Services/Cart/Leno.Cart.Api/Program.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/CartControllerBase.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs
- file:///workspace/src/Services/Cart/Leno.Cart.Api/appsettings.json
- file:///workspace/src/Services/Cart/Leno.Cart.Api/appsettings.Development.json
- file:///workspace/src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj

### 共享内核（用于理解基类行为）
- file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs
- file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs
- file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/IDomainEvent.cs
- file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/IUnitOfWork.cs
- file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/IRepository.cs
- file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs
- file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs
- file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs
- file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs

---

**报告说明**：
- 共扫描 28 个核心业务源文件 + 8 个共享内核辅助文件用于交叉验证
- 已严格排除 `Tests/` 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`、`Generated/` 目录
- 所有 DDD 合规检查（B1-B8）已核查：Domain 层仅引用 SharedKernel（B1 通过），仓储接口在 Domain 层（B3 通过），聚合根未直接暴露给表现层（B4 通过，控制器收发 DTO），值对象 `SkuPriceSnapshot` 不可变（B5 通过），`_items` 经 `IReadOnlyCollection` 暴露（B6 通过），跨 BC 仅通过 Guid ID 引用（B7 通过），`CartConfiguration` 在 Infrastructure 层（B8 通过）
- EF Core 乐观并发已通过 `BaseDbContext` 的 `Version` rowversion shadow property 配置（用户购物车有保护）；匿名购物车无任何并发控制（见 2.2）

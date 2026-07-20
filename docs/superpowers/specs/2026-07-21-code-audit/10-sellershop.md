# SellerShop（卖家与店铺管理域）代码分析报告

## 概述
- 扫描范围：src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/
- 代码行数（业务，非测试、非 Migrations）：约 5285 行
- 问题总数：高 4 / 中 11 / 低 8
- BC 健康度评分：7.0 / 10

SellerShop BC 整体遵循 DDD 分层（Domain/Application/Infrastructure/Api）与 CQRS 模式，聚合根（Shop、SellerProfile、ShopMetrics、ShopDashboardData）状态机设计严谨，防腐层（GrpcOrderAntiCorruptionClient / GrpcProductAntiCorruptionClient）采用 fail-closed 模式集成 Polly 重试，Outbox 模式与幂等性存储使用规范。但存在多处需要关注的问题：设计期工厂硬编码凭据、评价事件以 SpuId 充当 ShopId 的语义 Bug、读模型 6 个字段硬编码 0 占位、gRPC 服务用 Guid.GetHashCode() 做不可逆映射、多处跨步骤操作缺少显式事务、`UpdateShopInfoAsync` 缺失归属校验等。

## 🔴 高风险问题

### 1. 设计期工厂硬编码 SA 密码，源码泄露即可获取数据库访问权限
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs#L14-L17`
- **类别**：A2 异常处理不当（安全） / B4 共享内核污染
- **根因**：第 15 行 `UseSqlServer("Server=localhost,1433;Database=LenoSellerShop;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")` —— 设计期工厂的连接字符串硬编码了 SA 账号密码 `Leno@SqlServer2019`。生产连接串本身在 `appsettings.json#L34` 已正确使用 `${MSSQL_SA_PASSWORD}` 占位符（见 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/appsettings.json#L33-L35`），但设计期工厂为绕过 Redis 等依赖直接连库生成迁移，硬编码了与生产同结构的明文凭据。该字符串以源码形式进入 Git 仓库历史，任何能读取源码的人（含离职员工、供应链人员）均可凭此密钥直接登录数据库。
- **影响**：源码一旦泄露，攻击者可直接以 SA 身份连接数据库，绕过应用层所有鉴权，可读取/篡改/删除店铺、卖家档案、银行账号、身份证号等敏感数据。即便生产环境密码通过环境变量注入，开发/测试环境若复用同一密码（命名 `Leno@SqlServer2019` 暗示版本绑定，多人复用概率高），横向渗透风险显著。
- **修复建议**：① 设计期工厂从环境变量 `MSSQL_SA_PASSWORD` 读取，未配置时回退到固定占位（如 `__DESIGN_ONLY__`）仅用于本地开发；② 或从 `appsettings.Development.json` 读取连接字符串；③ 提交历史中的密码需轮换；④ 在 CI 中加入 secret scanning 防止再次提交。
- **影响范围**：`SellerShopDbContextDesignTimeFactory`、所有 `dotnet ef migrations add` 命令的执行链路。

### 2. ReviewSubmittedShopDashboardSyncConsumer 将 SpuId 当作 ShopId 传入 builder，评价提交事件触发的 ES 读模型同步全部失效
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs#L40-L51`
  关联 builder 实现：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs#L30-L43`
- **类别**：A1 边界条件 / B3 防腐层缺失 / B7 事件契约一致性
- **根因**：第 42 行 `var shopId = integrationEvent.SpuId;` 直接将 `ReviewSubmittedEvent.SpuId`（商品 SPU 标识）赋值给本地变量 `shopId`，传给 `_builder.BuildAsync(shopId, ct)`。`ShopDashboardReadModelBuilder.BuildAsync` 第 38 行 `_shopRepository.GetByIdAsync(shopId, ct)` 按主键查 Shop 聚合，SPU 的 Guid 与 Shop 的 Guid 同为 128 位随机值，几乎不可能匹配，返回 `null`，第 44-49 行直接返回空跳过同步。代码第 14-20 行 `<remarks>` 注释明确承认该限制并标注"待后续接通 SpuId→ShopId 解析"。
- **影响**：评价提交事件触发的工作台读模型重建 100% 失效，ES 中 `leno_shop_dashboards` 索引的 `TotalReviews / AverageRating / FiveStarReviews / OneStarReviews` 字段永远保持旧值（即初始零值，见高风险 #3），卖家工作台永远显示 0 评价 0 评分。即便评论域有大量评价提交，卖家也无法在自己的工作台看到评分变化，运营分析失真。
- **修复建议**：① 在事件契约中扩展 `ShopId` / `SellerId` 字段（首选，避免跨 BC 查询）；② 或在 SellerShop BC 内增加 SpuId→ShopId 映射仓储，由 `IProductAntiCorruptionService` 反查 Spu 所属 ShopId；③ 在事件源（评论域）发布事件时即填充 ShopId。修复前应在 `BuildReadModelAsync` 显式记录 Warning 并 Metrics 计数，避免静默失败。
- **影响范围**：`ReviewSubmittedShopDashboardSyncConsumer`、卖家工作台评价统计字段、运营分析仪表盘。

### 3. ShopDashboardReadModelBuilder 6 个评论/订单字段硬编码 0 占位，工作台读模型数据严重失真
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs#L55-L63`
  关联读模型定义：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs#L31-L50`
- **类别**：B2 聚合设计违规 / B3 防腐层缺失 / C5 异步消息堆积（语义）
- **根因**：Builder 第 55-63 行对 6 个字段硬编码为 0：
  - 第 56 行 `ConfirmedOrders = 0,` —— `ShopDashboardData` 聚合本身就没有该字段（见 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L11-L32`），无法填充。
  - 第 58 行 `CancelledOrders = 0,` —— 同上，聚合无此字段。
  - 第 60 行 `TotalReviews = 0,` —— 类注释承认"待后续接通 ReviewAfterSales BC 评论仓储后填充"。
  - 第 61 行 `AverageRating = 0m,` —— 同上。
  - 第 62 行 `FiveStarReviews = 0,` —— 同上。
  - 第 63 行 `OneStarReviews = 0,` —— 同上。
  
  注释解释为"SellerShop BC 未持有评论仓储"，但读模型字段已暴露在 ES 索引中供查询。
- **影响**：卖家工作台 6 个核心指标永久为 0：已确认订单数、已取消订单数、累计评价数、平均评分、五星评价数、一星评价数。即便订单/评价事件正常流转，工作台显示的数据与真实经营情况完全脱节，卖家无法据信做出经营决策，运营也无法识别高投诉店铺。
- **修复建议**：① 短期：在读模型字段增加 `IsNull`/`IsEstimated` 标识，UI 层显示"暂未统计"而非"0"；② 中期：扩展 `ShopDashboardData` 聚合增加 `ConfirmedOrders / CancelledOrders` 字段，由 `OrderPaidEvent` / `OrderCancelledEvent` 驱动维护；③ 长期：通过防腐层 `IReviewAntiCorruptionService` 反查评论域聚合评分统计，或订阅 `ReviewSubmittedEvent` 时正确解析 ShopId（见高风险 #2）后调用评论域 ACL 拉取评分。
- **影响范围**：`ShopDashboardReadModelBuilder`、`ShopDashboardReadModel`、`ShopDashboardQueryHandler`、所有读取工作台的端点。

### 4. SellerGrpcService.MapToProto 用 Guid.GetHashCode() 转 long，跨 BC 主键映射不可逆且冲突率高
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs#L93-L111`
- **类别**：A1 边界条件 / B7 事件契约一致性 / B1 BC 边界泄露
- **根因**：第 99 行 `ShopId = (long)dto.ShopId.GetHashCode()`（SellerInfo 映射）与第 105 行 `ShopId = (long)dto.ShopId.GetHashCode()`（ShopInfo 映射）将 `Guid` 类型的 `ShopId` 通过 `GetHashCode()` 转 `int` 再强转 `long`。`Guid.GetHashCode()` 返回 32 位有符号整数，存在大量哈希冲突（不同 Guid 可能映射到同一 long 值），且哈希值与原 Guid 不可逆。代码第 98 行注释承认"POC 简化：Guid→int64 不可逆映射，生产化改为 proto 字段改 string"。`GetShopInfo` 第 60-62 行的旧客户端回退路径 `new Guid((int)request.ShopId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)` 同样不可逆——客户端拿到的 ShopId 是哈希值，无法反推真实 Guid，回退构造的 Guid 与原 Guid 完全不同。
- **影响**：所有调用 `GetSellerInfo` / `GetShopInfo` 的下游 BC（Order、Product、ReviewAfterSales 等）若依赖 `ShopId` 字段反查 SellerShop，会拿到错误的 Guid（哈希值强转回 Guid 后查不到任何 Shop）。冲突情况下两个不同卖家的 ShopId 可能映射到同一 long 值，导致跨 BC 归属校验错位。`ValidateSellerOwnership` 端点（第 75-91 行）虽未使用 `MapToProto`，但 `GetSellerInfo` 返回的 `ShopId` 被下游缓存的场景会持续放大错误。
- **修复建议**：① 在 proto 契约中将 `ShopId` 字段类型改为 `string`，承载 `Guid.ToString()`（已在 `ShopInfo.ShopIdStr` 第 110 行新增，但 `SellerInfo` 缺失对应字段）；② 删除 `(long)dto.ShopId.GetHashCode()` 行，`ShopId` 标记 deprecated，要求所有客户端 30 天内迁移到 `ShopIdStr`；③ 在 `SellerInfo` proto 增加 `SellerIdStr` / `ShopIdStr` 字段并填充。
- **影响范围**：`SellerGrpcService.GetSellerInfo`、`SellerGrpcService.GetShopInfo`、所有消费 SellerShop gRPC 的下游 BC。

## 🟡 中风险问题

### 5. ShopConfiguration 用字符串 "Qualifications" 访问 backing field，重命名一处即崩溃
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopConfiguration.cs#L40-L43`
  关联字段定义：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs#L62-L65`
- **类别**：B2 聚合设计违规 / A1 边界条件
- **根因**：第 40 行 `builder.HasMany<ShopQualification>("Qualifications")` 以字符串形式指定导航属性名，编译期无法检测拼写错误。Shop 聚合根第 62 行 `private readonly List<ShopQualification> _qualifications = new();` 实际 backing field 名为 `_qualifications`，第 65 行公开属性 `Qualifications` 返回的是 `_qualifications.AsReadOnly()` 只读包装。EF Core 通过反射查找名为 "Qualifications" 的属性或 backing field，一旦 Shop 类重命名 `Qualifications` 属性或改为表达式成员，EF Core 配置不会编译失败但运行期抛 `InvalidOperationException`。
- **影响**：重构 Shop 聚合时极易漏改此字符串配置，运行期才暴露错误。EF Core 的字段命名约定（`_qualifications` ↔ `Qualifications`）依赖隐式约定，迁移到 .NET 新版本时行为可能变化。
- **修复建议**：① 优先改为基于表达式的方式：`builder.HasMany(s => s.Qualifications)`（需将 `Qualifications` 改为 `ICollection<ShopQualification>` 或显式配置 `HasField("_qualifications").UsePropertyAccessMode(FieldAccessMode.Field)`）；② 若坚持 backing field 私有性，使用 `HasField("_qualifications")` 显式声明，避免字符串约定。
- **影响范围**：`ShopConfiguration`、Shop 聚合的资质集合加载、所有 `Include` / 隐式加载资质的查询。

### 6. EfCoreShopRepository 的 GetByIdAsync / GetBySellerIdAsync 不 Include Qualifications，触发 N+1 与资质写丢失
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopRepository.cs#L22-L28`
  关联调用方：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L216-L220`（GetQualificationsAsync）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L222-L239`（资质审核）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L287`（ToShopDto 映射 Qualifications）
- **类别**：C1 N+1 查询 / A8 事务边界
- **根因**：第 23-24 行 `GetByIdAsync` 直接 `FirstOrDefaultAsync(s => s.Id == id, ct)` 不调用 `Include(s => s.Qualifications)`。`GetBySellerIdAsync` 同样。但 `ShopAppService` 多个方法依赖资质集合：
  - `GetQualificationsAsync` 第 219 行 `shop.Qualifications.Select(...).ToList()` 直接读 Shop.Qualifications，EF Core 懒加载未启用时返回空集合。
  - `ApproveQualificationAsync` / `RejectQualificationAsync` 第 226 / 236 行调用 `shop.ApproveQualification` / `shop.RejectQualification`，内部 `GetQualification` 第 326 行从 `_qualifications` 集合中查找，集合为空时第 337 行抛 `QUALIFICATION_NOT_FOUND` 异常。
  - `ToShopDto` 第 287 行 `Qualifications = shop.Qualifications.Select(...).ToList()` 映射到 DTO，懒加载未启用时返回空列表。
- **影响**：① 资质审核端点 `/api/admin/shops/{id}/qualifications/{qualId}/approve` / `reject` 永远抛 `QUALIFICATION_NOT_FOUND`，管理员无法审核资质；② `GET /api/shops/me` 与 `GET /api/admin/shops/{id}` 返回的 `Qualifications` 字段永远为空数组，资质列表与详情查询完全失效；③ 即便开启了懒加载（未在配置中显式启用），每个资质访问触发一次 DB 往返，N+1 性能问题严重。
- **修复建议**：在 `GetByIdAsync` 与 `GetBySellerIdAsync` 链式调用 `.Include(s => s.Qualifications)`；或在仓储接口新增 `GetByIdWithQualificationsAsync` 显式区分场景，避免简单查询也加载资质。
- **影响范围**：`ShopAppService.GetQualificationsAsync`、`ApproveQualificationAsync`、`RejectQualificationAsync`、`ToShopDto`、所有返回 Shop DTO 的端点。

### 7. Shop.DecrementProductCount 静默吞掉越界调用，掩盖商品计数不一致 Bug
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs#L293-L301`
  关联消费者：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/ProductEventConsumer.cs#L67-L80`（ProductTakenDownEventConsumer）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs#L285-L288`（IncrementProductCount）
- **类别**：A4 状态机非法迁移 / A2 异常处理不当
- **根因**：第 293-301 行 `DecrementProductCount` 当 `ProductCount <= 0` 时第 297 行 `return;` 直接返回，不抛异常、不记日志。而 `IncrementProductCount` 第 285-288 行无条件 `ProductCount++`。两个方向的不对称导致：① 重复消费 `ProductTakenDownEvent` 时不会产生负数（幂等保护），但 ② 若事件流出现错位（如先收到下架事件后收到上架事件，或重复下架后再次上架），`ProductCount` 与真实在售数会持续累积偏差，且无任何告警。`ProductTakenDownEventConsumer` 第 49 行注释明确依赖该"幂等防负"行为。
- **影响**：店铺的 `ProductCount` 字段会逐渐与商品域真实在售商品数偏离，卖家工作台显示的商品数失真。当偏差足够大时（如显示 0 但实际有商品在售），可能触发错误的运营决策（误关店、误暂停）。排查时无任何日志线索。
- **修复建议**：① `DecrementProductCount` 当 `ProductCount <= 0` 时抛 `SellerShopDomainException("商品数已为 0，无法继续下架", "SHOP_PRODUCT_COUNT_NEGATIVE")`，由消费者 `catch` 后记录 Warning 并跳过（保留幂等但留下可观测痕迹）；② 或返回 `bool` 表示是否实际递减，由消费者记录 Metrics；③ 长期应对：与商品域建立对账机制，定期同步真实在售数。
- **影响范围**：`Shop.DecrementProductCount`、`ProductTakenDownEventConsumer`、所有读取 `Shop.ProductCount` 的端点与工作台。

### 8. ShopsController.UpdateMyShopAsync / SubmitQualificationAsync 多步操作无显式事务，中途失败留半状态
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L49-L54`（UpdateMyShopAsync）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L59-L75`（SubmitQualificationAsync）
- **类别**：A8 事务边界 / A7 异步消息可靠性
- **根因**：`UpdateMyShopAsync` 第 51-52 行：
  ```
  var shop = await _shopAppService.GetMyShopAsync(GetCurrentUserId(), ct);
  var updated = await _shopAppService.UpdateShopInfoAsync(shop.Id, dto, ct);
  ```
  两次独立 AppService 调用，各自内部 `SaveEntitiesAsync` 提交事务。若第一步成功后第二步因校验失败抛异常，第一步已读取的 shop 实体被 ChangeTracker 跟踪，但控制器无事务包裹，下一次请求时 ChangeTracker 已 dispose，不会产生数据问题——但若第二步校验通过但 `SaveEntitiesAsync` 时数据库连接断开，已读取但未持久化的状态丢失，客户端收到 500 错误却不知道哪一步失败。
  
  `SubmitQualificationAsync` 第 69-73 行更严重：先 `GetMyShopAsync` 加载 shop，再 `SubmitQualificationAsync` 内部第 197 行（见 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L193-L213`）调用 `_fileStorageService.UploadAsync` 上传文件，再 `shop.AddQualification(qualification)` + `SaveEntitiesAsync`。若文件上传成功但数据库保存失败，已上传到对象存储的资质图片成为孤儿文件，无人清理；若数据库保存成功但客户端因网络超时重试，第二次请求会再次上传同一文件产生重复孤儿。
- **影响**：① 控制器层无事务，半状态失败无法回滚；② 资质图片上传与数据库保存不在同一事务，孤儿文件累积；③ 客户端重试无幂等保护，重复提交会产生多条资质记录。
- **修复建议**：① 将控制器的多步操作下沉到 AppService 单一方法（如 `UpdateMyShopInfoAsync(userId, dto, ct)` 内部完成 GetBySellerId + Update + Save），由 `_unitOfWork` 统一事务边界；② 资质提交增加幂等键（如客户端生成 `IdempotencyKey`），AppService 内查重跳过；③ 文件上传失败时回滚数据库变更，或先存数据库待提交状态、文件上传成功后置 Active 状态（Saga 模式）。
- **影响范围**：`/api/shops/me` PUT 端点、`/api/shops/me/qualifications` POST 端点、所有卖家自助修改店铺信息的链路。

### 9. ShopDashboardData.OnOrderPaid 不按订单跟踪金额，订单取消时收入不减回，工作台收入永远虚高
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L74-L83`（OnOrderPaid）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L85-L96`（OnOrderCancelled）
  关联消费者：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs#L143-L153`（OrderPaidEventConsumer）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs#L189-L199`（OrderCancelledEventConsumer）
- **类别**：A4 状态机非法迁移 / B2 聚合设计违规
- **根因**：`OnOrderPaid` 第 81 行 `TotalRevenue += amount;` 直接累加金额，不记录是哪个订单支付的。`OnOrderCancelled` 第 88-96 行只 `PendingOrders--`，不回滚 `TotalRevenue`。语义上"订单取消"包含"支付后取消退款"场景，但聚合未区分取消时是否已支付。即便事件流保证 `OrderPaidEvent` 先于 `OrderCancelledEvent` 到达，取消后 `TotalRevenue` 仍包含已退款金额，工作台收入虚高。
- **影响**：卖家工作台显示的 `TotalRevenue` 永远大于真实收入（差额等于已退款订单总额）。运营分析仪表盘的收入指标失真，财务对账时差异显著，且无法从聚合本身定位偏差来源（无订单级明细）。
- **修复建议**：① 在 `ShopDashboardData` 增加 `RefundedAmount` 字段，由 `OrderRefundedEvent`（若存在）驱动累加，工作台显示 `NetRevenue = TotalRevenue - RefundedAmount`；② 或在 `OnOrderCancelled` 接收 `amount` 参数，若该订单此前已支付则 `TotalRevenue -= amount`（需聚合持有订单级状态，改造成本高）；③ 短期方案：在文档/UI 明确"总收入含已退款金额"，避免误用。
- **影响范围**：`ShopDashboardData.TotalRevenue`、`OrderPaidEventConsumer`、`OrderCancelledEventConsumer`、`SellerDashboardAppService.GetDashboardAsync`、所有工作台收入展示。

### 10. ShopAppService.UpdateShopInfoAsync 直接以传入 shopId 操作，缺失卖家归属校验，可越权改任意店铺
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L107-L120`
  关联调用方：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L49-L54`（通过 GetMyShopAsync 先获取 shop.Id 再传入，间接安全）
- **类别**：A2 异常处理不当（安全） / B3 防腐层缺失
- **根因**：第 110 行 `var shop = await RequireShopAsync(shopId, ct);` 直接按 `shopId` 加载店铺，第 112-114 行调用三个 `Update` 方法修改，无任何归属校验。`IShopAppService` 接口暴露该方法为公共方法，任何调用方均可传入任意 `shopId`。当前 `ShopsController.UpdateMyShopAsync` 通过先 `GetMyShopAsync(GetCurrentUserId())` 再传 `shop.Id` 间接保证归属，但 `AdminShopsController` 与未来其他调用方未必遵循该模式。`SubmitQualificationAsync` 第 193-213 行同样问题：直接按 `shopId` 加载并添加资质，无归属校验。
- **影响**：若未来有端点直接接受客户端传入的 `shopId` 调用 `UpdateShopInfoAsync`，卖家 A 可传入卖家 B 的 `shopId` 修改其店铺名称、描述、地址、Logo、联系方式。当前控制器层防御是脆弱的"约定式"安全，非"强制式"安全。
- **修复建议**：① 在 `UpdateShopInfoAsync` / `SubmitQualificationAsync` 增加 `userId` 参数，内部 `_shopRepository.GetBySellerIdAsync(userId)` 加载店铺并校验 `shop.Id == shopId`；② 或在 AppService 提供专门的 `UpdateMyShopInfoAsync(userId, dto)` 方法，控制器只调用该方法，`shopId` 由 AppService 内部解析；③ 对所有公开 AppService 方法做威胁建模，标注是否需要归属校验。
- **影响范围**：`IShopAppService.UpdateShopInfoAsync`、`IShopAppService.SubmitQualificationAsync`、所有未来调用这两个方法的新端点。

### 11. ShopDashboardReadModel 注释引用不存在的 OrderConfirmedShopDashboardSyncConsumer，文档与代码漂移
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs#L9-L11`
  关联实际类：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/OrderCompletedShopDashboardSyncConsumer.cs#L15-L16`、注册入口：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L122-L125`
- **类别**：B7 事件契约一致性 / A2 异常处理不当（文档）
- **根因**：第 9 行 `<item><see cref="OrderConfirmedShopDashboardSyncConsumer"/>（订单完成，<c>OrderCompletedEvent.SellerId</c> 即 ShopId）</item>` 引用 `OrderConfirmedShopDashboardSyncConsumer`，但实际类名为 `OrderCompletedShopDashboardSyncConsumer`（见 LS 结果与 `OrderCompletedShopDashboardSyncConsumer.cs#L15`）。`OrderConfirmed` 与 `OrderCompleted` 在订单域是不同状态（已确认 ≠ 已完成），命名混淆会导致维护者误以为存在处理"订单确认"事件的消费者。`ServiceCollectionExtensions` 第 123-125 行只注册了 `OrderCreatedShopDashboardSyncConsumer` / `OrderCompletedShopDashboardSyncConsumer` / `ReviewSubmittedShopDashboardSyncConsumer` 三个，无 `OrderConfirmed` 变体。
- **影响**：① `<see cref>` 在 IDE 中无法跳转，文档可读性下降；② 维护者按文档查找消费者时会困惑；③ 若未来真有 `OrderConfirmedEvent` 需要同步读模型，可能误以为已存在消费者而漏建。
- **修复建议**：将 `OrderConfirmedShopDashboardSyncConsumer` 改为 `OrderCompletedShopDashboardSyncConsumer`，确保 XML 文档与代码一致。
- **影响范围**：`ShopDashboardReadModel` 类注释、维护者理解、未来事件消费者扩展。

### 12. ShopDashboardDataConfiguration 未显式映射审计字段，DB 列名 PascalCase 与其他表 snake_case 不一致，CreatedBy/UpdatedBy 缺失长度限制
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopDashboardDataConfiguration.cs#L13-L27`
  对比 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopConfiguration.cs#L33-L36`、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopQualificationConfiguration.cs#L29-L32`
  迁移佐证：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Migrations/20260717175445_InitialCreate.cs#L70-L73`
- **类别**：B8 仓储滥用 / C2 缺失索引
- **根因**：`ShopDashboardDataConfiguration.Configure` 第 13-27 行只显式映射了 `Id / ShopId / TotalOrders / PendingOrders / CompletedOrders / TotalRevenue / Currency / LastUpdatedAt`，未映射继承自 `Entity` 基类的 `CreatedAt / UpdatedAt / CreatedBy / UpdatedBy`。对比 `ShopConfiguration` 第 33-36 行与 `ShopQualificationConfiguration` 第 29-32 行均显式映射这四个字段为 snake_case 并对 `CreatedBy/UpdatedBy` 设置 `HasMaxLength(64)`。EF Core 对未显式映射的属性按约定生成列名（PascalCase `CreatedAt` 等），迁移文件第 70-73 行证实 `shop_dashboard_data` 表的审计字段列为 `CreatedAt / UpdatedAt / CreatedBy / UpdatedBy`（PascalCase，`nvarchar(max)`），而其他表为 `created_at / updated_at / created_by / updated_by`（snake_case，`nvarchar(64)`）。
- **影响**：① 数据库 schema 不一致，DBA 维护时需特殊记忆该表；② `CreatedBy/UpdatedBy` 为 `nvarchar(max)` 无长度限制，恶意客户端可写入超长字符串占用存储；③ 跨表 JOIN 审计字段时列名不统一，SQL 编写易错；④ 若未来 BaseDbContext 调整审计字段约定，该表会因未显式映射而行为漂移。
- **修复建议**：在 `ShopDashboardDataConfiguration.Configure` 增加四行映射：`builder.Property(d => d.CreatedAt).HasColumnName("created_at");` `builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");` `builder.Property(d => d.CreatedBy).HasColumnName("created_by").HasMaxLength(64);` `builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);` 并生成迁移重命名列。
- **影响范围**：`shop_dashboard_data` 表 schema、所有审计字段查询、DBA 维护脚本。

### 13. EfCoreShopMetricsRepository.UpsertAsync 用 EntityState.Modified 直接覆盖，绕过审计填充与 rowversion 并发检查
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopMetricsRepository.cs#L73-L97`
- **类别**：A3 并发与竞态 / A8 事务边界
- **根因**：第 90-96 行 `UpsertAsync` 当 `(ShopId, Date)` 已存在时，第 94 行 `_context.ShopMetrics.Attach(metrics);` 将传入的新聚合 Attach 到 ChangeTracker，第 95 行 `_context.Entry(metrics).State = EntityState.Modified;` 强制标记为 Modified。这会：① 用新聚合的所有字段覆盖既有聚合（包括 Id），但既有聚合的 `CreatedAt / CreatedBy` 等不可变审计字段会被新聚合的默认值（`DateTime.MinValue` / null）覆盖——实际上代码注释第 92-93 行声称"保留既有 Id 与审计链"，但 `EntityState.Modified` 会 UPDATE 所有列，并不保留既有值；② 绕过 `BaseDbContext` 的 `SaveChangesAsync` 中审计字段自动填充逻辑（`UpdatedAt` 会被填充但 `CreatedAt` 不会从既有值恢复）；③ `EntityState.Modified` 不读取既有 rowversion，UPDATE 时若该行已被其他事务修改，不会抛 `DbUpdateConcurrencyException`，丢失乐观并发保护。
  
  另外，第 78-81 行 `if (_context.Entry(metrics).State != EntityState.Detached) return;` 检查 Detached 状态，但 `AddAsync` 后状态为 `Added`，`UpdateAsync` 后为 `Modified`，调用方先 `GetByShopIdAsync` 加载再修改后调用 `UpsertAsync` 会直接 return，逻辑正确但语义混乱（`Upsert` 应是幂等写，不是 no-op）。
- **影响**：① `ShopMetrics` 的 `CreatedAt` 字段在 Upsert 路径下被覆盖为 `DateTime.MinValue`，审计链断裂；② 并发场景下多个消费者同时 Upsert 同一 `(ShopId, Date)` 会导致丢失更新（Last-Write-Wins）；③ 调用方需理解 `Upsert` 的隐式 no-op 行为，易误用。
- **修复建议**：① `UpsertAsync` 当存在既有聚合时，从既有聚合读取 `Id / CreatedAt / CreatedBy`，赋值到新聚合后再 `Attach + Modified`，或直接修改既有聚合字段（推荐，遵循聚合不变量）；② 使用 `ConcurrencyToken`（rowversion）做乐观并发控制，UPDATE 时带 `WHERE version = @expected`；③ 明确 `Upsert` 与 `Update` 的语义边界，`Upsert` 用于"不存在则创建"，已存在应交由 `Update` 路径处理。
- **影响范围**：`EfCoreShopMetricsRepository.UpsertAsync`、`OrderCompletedEventConsumer.HandleAsync`（第 49 行调用 Upsert）。

### 14. SellerInternalQueryService 用 try/catch SellerShopDomainException 控制流程，性能与可读性双失
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs#L37-L69`（GetSellerInfoAsync）、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs#L106-L121`（ValidateShopOwnershipAsync）
- **类别**：A2 异常处理不当 / B6 层依赖反向
- **根因**：`GetSellerInfoAsync` 第 40-47 行 `try { seller = await _sellerAppService.GetSellerProfileAsync(sellerId, ct); } catch (SellerShopDomainException ex) when (ex.ErrorCode == "SELLER_NOT_FOUND") { return null; }`，第 52-60 行再次 try/catch `SHOP_NOT_FOUND`。`ValidateShopOwnershipAsync` 第 110-118 行同样模式。AppService 的 `RequireProfileByUserIdAsync` 第 105-114 行与 `ShopAppService.RequireShopAsync` 第 241-250 行抛异常表示"未找到"，InternalQueryService 捕获异常转 null。这是典型的"用异常控制流程"反模式：① 抛异常开销大（堆栈展开、异常对象构造），高频调用时性能损失显著；② 调用栈被异常打断，调试时需禁用"仅我的代码"才能看清流程；③ `when (ex.ErrorCode == "SELLER_NOT_FOUND")` 字符串匹配脆弱，错误码重命名会导致静默失效（异常穿透 catch 进入上层）。
- **影响**：跨 BC 高频查询（如 `ValidateSellerOwnership` 每次订单创建、商品上架都会调用）性能开销大；调试体验差；错误码重命名引发隐蔽 Bug。
- **修复建议**：① 在 `ISellerAppService` 与 `IShopAppService` 增加 `TryGetXxxAsync` 方法返回 `T?` 而非抛异常，InternalQueryService 直接消费；② 或在 AppService 暴露 `GetByIdAsync` 直接返回仓储结果（无 Require 包装）；③ 短期：在 catch 块记录 Debug 日志便于追踪。
- **影响范围**：`SellerInternalQueryService` 全部方法、所有跨 BC 内部查询、gRPC 服务端。

### 15. SellerDashboardAppService.GetDashboardAsync 标记 [Obsolete] 计划 2026-08-01 移除，但无迁移计划与调用方审计
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs#L27-L63`
  关联替代方案：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs#L11-L31`
- **类别**：B5 CQRS 职责混乱 / A2 异常处理不当（生命周期）
- **根因**：第 28 行 `[Obsolete("请使用 IQueryHandler<ShopDashboardQuery, ShopDashboardResult>，将在 2026-08-01 移除")]` 标记 `GetDashboardAsync` 将被移除，替代实现为 `ShopDashboardQueryHandler`（基于 ES 读模型）。但：① 无迁移计划文档说明何时切换控制器到 QueryHandler；② 无调用方审计确认所有调用方已迁移；③ 替代方案依赖 ES 读模型，而读模型存在高风险 #2 / #3 的数据失真问题，迁移后工作台数据质量更差；④ `GetDashboardAsync` 直接读 DB 聚合（实时数据），QueryHandler 读 ES（最终一致），切换后语义变化未告知调用方。
- **影响**：若按计划 2026-08-01 移除 `GetDashboardAsync`，但调用方（如 `SellerDashboardController`）未迁移，编译失败；即便迁移成功，ES 读模型数据失真问题会导致工作台显示错误数据，用户感知为"功能回退"。
- **修复建议**：① 推迟移除日期直至 ES 读模型数据完整（依赖高风险 #2 / #3 修复）；② 在 `[Obsolete]` 注释中明确迁移步骤与调用方清单；③ 增加 Feature Flag 控制读 DB 还是读 ES，灰度切换；④ 在控制器层增加对比指标（DB vs ES 数据差异），监控切换前后数据质量。
- **影响范围**：`SellerDashboardAppService.GetDashboardAsync`、`SellerDashboardController`、所有工作台查询端点。

## 🟢 低风险问题

### 16. BusinessLicense 值对象定义但全 BC 未被使用，死代码
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/ValueObjects/BusinessLicense.cs#L1-L56`
- **类别**：B2 聚合设计违规
- **根因**：`BusinessLicense` record 定义了 `LicenseNo / ImageUrl / ExpireDate` 字段与 `IsValidAt` 方法，但 Shop 聚合根第 45 行 `BusinessLicenseNo` 是简单 `string?` 字段，SellerProfile 第 28 行同样为 `string?`，均未使用 `BusinessLicense` 值对象。ShopQualification 实体也未引用。全 BC grep `BusinessLicense` 仅在该文件内出现（除 using）。
- **影响**：维护者误以为该值对象在使用，重构时困惑；增加编译体积（可忽略）。
- **修复建议**：① 若计划未来使用，在 Shop 聚合的 `BusinessLicenseNo` 字段改为 `BusinessLicense?` 类型并补充图片 URL 与有效期；② 若无计划，删除该文件。
- **影响范围**：无运行时影响，仅维护成本。

### 17. Program.cs 启动时调用 MigrateWithLockAsync，Redis 故障会阻塞整个服务启动
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Program.cs#L52`
- **类别**：A2 异常处理不当 / C7 资源/连接池
- **根因**：第 52 行 `await app.Services.MigrateWithLockAsync<SellerShopDbContext>();` 在 `app.Run()` 之前同步等待迁移完成。该方法使用 Redis 分布式锁避免多实例并发迁移移。若 Redis 不可用，获取锁会超时或抛异常，整个 SellerShop API 无法启动。容器编排（K8s）的 liveness/readiness 探针在 `app.Run()` 之前不响应，重启循环无法自我恢复。
- **影响**：Redis 短暂故障（网络抖动、Redis 维护）会导致 SellerShop 服务全部实例启动失败，需人工介入。
- **修复建议**：① 将迁移改为独立 Job 或 Init Container，与 API 启动解耦；② 或在 `MigrateWithLockAsync` 增加降级逻辑：Redis 不可用时跳过锁直接迁移（依赖 DB 自身的迁移锁，如 SQL Server 的 `sp_getapplock`）；③ 配置启动超时，超时后记录 Error 日志并退出，让 K8s 重启。
- **影响范围**：`Program.cs`、SellerShop API 启动链路、所有依赖该服务的下游 BC。

### 18. QualificationExpiryReminder 硬编码 24 小时扫描间隔与 [30,7,1] 提醒天数，不可配置
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs#L19`、`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs#L44`
- **类别**：C3 缓存策略（配置） / A5 边界条件
- **根因**：第 19 行 `private static readonly int[] ReminderDays = [30, 7, 1];` 与第 44 行 `await Task.Delay(TimeSpan.FromHours(24), stoppingToken);` 均为硬编码。运营无法按业务节奏调整提醒频率（如大促期间提前 60 天提醒），也无法在测试环境加速扫描（测试需等 24 小时才能验证逻辑）。
- **影响**：测试环境验证资质到期提醒需等待 24 小时，开发效率低；运营调整提醒策略需改代码重新发布。
- **修复建议**：① 从 `IOptions<QualificationReminderOptions>` 读取 `ReminderDays` 与 `ScanIntervalHours`，通过 `appsettings.json` 配置；② 测试环境配置为 1 分钟扫描间隔。
- **影响范围**：`QualificationExpiryReminder`、资质到期提醒频率。

### 19. ShopMetrics.RecordOrder 币种校验用简单字符串相等，未做大小写归一化
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopMetrics.cs#L99-L103`
- **类别**：A1 边界条件
- **根因**：第 99 行 `if (SalesAmount.Currency != salesAmount.Currency)` 直接字符串比较。`ShopMetrics.Create` 第 80 行 `Money.Zero(currency.Trim().ToUpperInvariant())` 已将创建时币种大写化，但 `RecordOrder` 接收的 `salesAmount` 若来自事件且未大写化（如 `"cny"`），比较失败抛 `METRICS_CURRENCY_MISMATCH`。`OrderCompletedEventConsumer` 第 43 行 `var currency = string.IsNullOrWhiteSpace(integrationEvent.Currency) ? "CNY" : integrationEvent.Currency;` 未做 `ToUpperInvariant`，若事件发布方传入小写 `"cny"`，第 52 行 `Money.Create(integrationEvent.TotalAmount, currency)` 创建的 Money 币种为 `"cny"`，与 ShopMetrics 的 `"CNY"` 不匹配。
- **影响**：事件发布方未规范币种大小写时，订单完成事件处理抛异常进入重试队列，最终死信。
- **修复建议**：① 在 `Money.Create` 内统一 `ToUpperInvariant`；② 或在 `RecordOrder` 比较前对 `salesAmount.Currency` 做 `ToUpperInvariant`；③ 在事件契约文档明确币种必须大写。
- **影响范围**：`ShopMetrics.RecordOrder`、`OrderCompletedEventConsumer`、所有跨币种事件处理。

### 20. ShopDashboardQueryHandler 静默忽略 StartDate / EndDate 查询参数，调用方无法获知未生效
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs#L22-L31`
- **类别**：A5 边界条件 / B5 CQRS 职责混乱
- **根因**：第 27-28 行 `_ = query.StartDate; _ = query.EndDate;` 显式丢弃查询参数（避免未使用警告），注释"当前读模型为快照型，暂不消费"。调用方传入日期范围期望获取趋势数据，实际返回当前快照，无任何错误提示。
- **影响**：调用方误以为支持日期范围查询，UI 显示空数据或错误图表，排查困难。
- **修复建议**：① 若不支持日期范围，在 `ShopDashboardQuery` 构造函数或 Validator 中校验 `StartDate / EndDate` 必须为 null，否则抛 `ArgumentException`；② 或返回 `null` 表示不支持；③ 长期：实现日期范围查询逻辑，从 `ShopMetrics` 聚合按日期范围读取趋势数据。
- **影响范围**：`ShopDashboardQueryHandler`、所有传入日期范围的工作台查询。

### 21. ShopAppService.UpdateShopInfoAsync 调用三个独立 Update 方法，部分失败时聚合处于半更新状态
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L112-L114`
- **类别**：A8 事务边界 / B2 聚合设计违规
- **根因**：第 112-114 行：
  ```
  shop.UpdateInfo(dto.ShopName, dto.Description, dto.Address);
  shop.UpdateLogo(dto.Logo);
  shop.UpdateContact(dto.ContactPhone, dto.ContactEmail);
  ```
  三个方法各自独立校验。若 `UpdateLogo` 第 263 行 `ValidateLogo(logo)` 抛异常（如 URL 超长），`UpdateInfo` 已修改的 `ShopName / Description / Address` 仍停留在内存中的 shop 实体上。因 `SaveEntitiesAsync` 在异常后不会调用，DB 不受影响——但若调用方 catch 异常后继续使用该 shop 实体（如返回部分更新结果），会得到不一致的聚合状态。
- **影响**：当前因 `SaveEntitiesAsync` 未调用，DB 一致性未破坏；但代码可读性差，维护者可能误以为三个方法是独立事务。未来若引入更复杂的校验逻辑（如跨字段校验），半更新状态难以排查。
- **修复建议**：① 在 Shop 聚合提供 `UpdateAllInfo(shopName, description, address, logo, contactPhone, contactEmail)` 单一方法，内部原子化校验与赋值；② 或在 AppService 用 try-catch 包装，异常时回滚内存状态（复杂，不推荐）。
- **影响范围**：`ShopAppService.UpdateShopInfoAsync`、Shop 聚合的多个 Update 方法。

### 22. OrderCancelledEventConsumer 不区分"未支付取消"与"已支付取消"，PendingOrders 计数在已支付场景下错误
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs#L189-L199`
  关联聚合方法：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L85-L96`
- **类别**：A4 状态机非法迁移 / B7 事件契约一致性
- **根因**：`OrderCancelledEventConsumer.HandleAsync` 第 192-194 行无条件调用 `dashboard.OnOrderCancelled()`，第 88-96 行 `OnOrderCancelled` 只做 `PendingOrders--`（防负）。但订单状态机中，订单可能在"待支付"或"已支付待发货"两个阶段被取消：
  - 待支付取消：`OrderCreatedEvent` 已让 `PendingOrders++`，取消时 `--` 正确。
  - 已支付待发货取消：`OrderPaidEvent` 未改 `PendingOrders`（见 `OnOrderPaid` 第 74-83 行只累加 `TotalRevenue`），此时 `PendingOrders` 已在 `OrderCreatedEvent` 时 `++`，取消时 `--` 正确。
  
  实际分析后发现逻辑自洽，但聚合命名 `OnOrderCancelled` 未表达"取消时是否需回滚收入"的语义，且与中风险 #9 叠加——已支付订单取消后 `TotalRevenue` 不减回，工作台收入虚高。
- **影响**：单独看 `PendingOrders` 计数正确；但与 `TotalRevenue` 联动时语义不一致（见中风险 #9）。低风险在于该问题需结合 #9 一起修复。
- **修复建议**：在 `OrderCancelledEvent` 契约中增加 `WasPaid` 字段或 `RefundAmount`，`OnOrderCancelled` 接收参数决定是否回滚 `TotalRevenue`。
- **影响范围**：`OrderCancelledEventConsumer`、`ShopDashboardData.OnOrderCancelled`、工作台收入与待处理订单计数。

### 23. GrpcProductAntiCorruptionClient 与 GrpcOrderAntiCorruptionClient 失败时 fail-closed 返回 null，但日志级别仅 Warning，无 Metrics 告警
- **位置**：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs#L59-L64`
  关联基类：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs#L83-L96`
- **类别**：A7 异步消息可靠性 / C8 限流/熔断
- **根因**：`GetOrderSellerIdAsync` 第 59-64 行 `catch (AntiCorruptionException ex) { _logger.LogWarning(ex, "订单域 GetOrderSellerId 调用失败，fail-closed 返回 null OrderId={OrderId}", orderId); return null; }`。`SellerInternalQueryService.ValidateOrderOwnershipAsync` 第 130-135 行收到 null 后 `return orderSellerId.HasValue && orderSellerId.Value == sellerId;` 返回 false。fail-closed 是正确的安全策略（不归属即拒绝），但：① 仅 Warning 日志，无 Metrics 计数，运维无法快速感知 ACL 故障率；② 无熔断机制，订单域持续故障时每次归属校验都走完整 gRPC 超时链路（即便有 Polly 重试），拖慢主流程；③ `ValidateSellerOwnership` gRPC 端点被高频调用（每次订单/商品操作），故障放大。
- **影响**：订单域或商品域故障期间，SellerShop 的归属校验全部返回 false，卖家无法操作自己的资源（误判为越权），用户体验下降；且因无 Metrics，运维可能未及时发现根因在下游 BC。
- **修复建议**：① 在 fail-closed 路径增加 `AntiCorruptionMetrics.RecordFailure` 计数（基类已调用，但 fail-closed catch 在子类，需补埋点）；② 配置告警规则，ACL 失败率 > 5% 触发告警；③ 引入熔断（Polly Circuit Breaker），连续失败 N 次后短路返回 null，避免持续超时；④ 在 gRPC 响应中区分"故障"与"确属不归属"，避免 fail-closed 误伤。
- **影响范围**：`GrpcOrderAntiCorruptionClient`、`GrpcProductAntiCorruptionClient`、`SellerInternalQueryService.ValidateOwnershipAsync`、所有跨 BC 归属校验。

## BC 健康度评分

**总评：7.0 / 10**

### 评分维度

| 维度 | 得分 | 说明 |
|------|------|------|
| 分层架构（Domain/Application/Infrastructure/Api） | 9.0 | 严格遵循 DDD 分层，依赖方向正确，无反向引用 |
| 聚合设计（状态机、不变量、封装） | 8.0 | Shop / SellerProfile / ShopMetrics 状态机严谨，但 ShopDashboardData 缺少订单级跟踪导致收入回滚缺失（中风险 #9），DecrementProductCount 静默吞错（中风险 #7） |
| 防腐层（ACL）实现 | 7.5 | gRPC ACL 采用 fail-closed + Polly 重试，但失败可观测性不足（低风险 #23），SpuId→ShopId 映射缺失（高风险 #2） |
| 事件驱动（Outbox / 幂等） | 8.5 | Outbox 模式规范，IntegrationEventConsumerBase 幂等去重到位 |
| CQRS 职责分离 | 7.0 | QueryHandler 已建立但 Obsolete 标记的 AppService 未清理（中风险 #15），QueryHandler 静默忽略参数（低风险 #20） |
| 数据一致性（事务边界） | 6.0 | 控制器多步操作无事务（中风险 #8），UpsertAsync 绕过审计与并发检查（中风险 #13），UpdateShopInfoAsync 三步非原子（低风险 #21） |
| 安全（鉴权、归属校验、凭据管理） | 5.5 | 设计期工厂硬编码密码（高风险 #1），UpdateShopInfoAsync 缺失归属校验（中风险 #10），gRPC 服务 Guid→long 不可逆映射（高风险 #4） |
| 可观测性（日志、Metrics、追踪） | 6.5 | Serilog + OpenTelemetry 已集成，但 fail-closed 路径无 Metrics（低风险 #23），静默吞错（中风险 #7） |
| 代码质量（命名、注释、DRY） | 7.0 | 命名规范，注释详尽，但存在文档与代码漂移（中风险 #11）、死代码（低风险 #16） |
| 测试覆盖（推断） | 6.0 | Tests 目录已排除扫描，但从代码复杂度看，ACL 与消费者链路测试覆盖度待验证 |

### 主要风险点
1. **数据失真风险**：高风险 #2 + #3 + #9 叠加，卖家工作台的收入、订单数、评价数、评分等核心指标全面失真，卖家无法据信经营。
2. **安全风险**：高风险 #1（硬编码密码）+ 中风险 #10（归属校验缺失）构成安全防线两处缺口。
3. **跨 BC 集成风险**：高风险 #4（gRPC 不可逆映射）+ 中风险 #11（文档与代码漂移）影响所有下游 BC 的 SellerShop 调用。
4. **可维护性风险**：中风险 #5（字符串 backing field）+ #12（审计字段未映射）+ #14（异常控制流程）增加维护成本。

### 改进优先级
1. **P0（立即修复）**：高风险 #1 硬编码密码、高风险 #4 gRPC 映射、中风险 #10 归属校验、中风险 #6 资质加载 N+1
2. **P1（本迭代修复）**：高风险 #2 SpuId→ShopId、高风险 #3 读模型占位字段、中风险 #9 收入回滚、中风险 #12 审计字段映射
3. **P2（下迭代修复）**：中风险 #5/#7/#8/#13/#14/#15、低风险全部

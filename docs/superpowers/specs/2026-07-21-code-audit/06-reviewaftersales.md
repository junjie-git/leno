# ReviewAfterSales 评价与售后域代码静态分析报告

> 扫描日期：2026-07-21  
> 扫描范围：src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Api,Application,Domain,Infrastructure}/  
> 排除项：Tests 目录、Migrations Designer、ModelSnapshot、Generated

## 1. 概览

- **业务代码行数**：约 4400 行（Domain ~1300、Application ~800、Api ~600、Infrastructure ~1700）
- **问题统计**：🔴 高 11 项 / 🟡 中 12 项 / 🟢 低 8 项
- **风险评级**：🔴 高 = 数据一致性破坏/资损/安全漏洞/可用性故障；🟡 中 = 边界场景 Bug/性能隐患；🟢 低 = 代码质量/可维护性

## 2. 🔴 高风险问题

### 2.1 买家提交售后申请时 SellerId 完全由客户端伪造
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L52-L67  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/AfterSalesDtos.cs#L8-L19  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IAfterSalesEligibilityChecker.cs#L9-L19  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L38-L73
- **类别**：A1 / A5 / B2
- **现象**：`SubmitAfterSalesDto.SellerId` 是请求体字段，由客户端直接传入。`AfterSalesAppService.SubmitAfterSalesAsync` 在第 58 行直接把 `dto.SellerId` 透传给 `AfterSalesAggregate.Create`，写入聚合的 `SellerId` 字段。`IAfterSalesEligibilityChecker.EnsureEligibleAsync` 签名根本没有 `sellerId` 参数，`AfterSalesEligibilityChecker` 实现也完全不校验 `dto.SellerId` 是否为该订单的真实卖家。`IOrderStatusProvider.OrderStatusInfo` 中也没有 `SellerId` 字段可供校验。
- **影响**：
  1. 恶意买家 A 对订单 O 提交售后时把 `SellerId` 写成受害者卖家 B 之外的任意 Guid（包括买家 A 自己的小号、不存在的卖家、竞争对手卖家）。聚合落库后 `AfterSales.SellerId` 即为伪造值。
  2. 后续 `ApproveAfterSalesAsync` / `ConfirmReturnAsync` 的 `RequireOwnedAfterSales(afterSales, operatorId)`（第 238-248 行）以 `afterSales.SellerId` 为准做归属判定，伪造的 SellerId 将成为唯一可审核者，真实卖家永远看不到该售后单，导致买家永远拿不到退款。
  3. `AfterSalesSubmittedDomainEvent` / `AfterSalesApprovedDomainEvent` 都会带上伪造的 SellerId，下游消息通知域会向错误卖家推送通知。
  4. `GetBySellerAsync` 端点会把伪造 SellerId 关联的售后单注入到无辜卖家的列表中，造成卖家端信息污染。
- **修复建议**：
  ```csharp
  // 1) 在 OrderStatusInfo 增加 SellerId 字段
  public sealed class OrderStatusInfo
  {
      public Guid SellerId { get; init; }   // 新增
      // ...既有字段
  }
  // 2) 让 IAfterSalesEligibilityChecker 接收 dto.SellerId 并由 checker 校验
  Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, Guid sellerId, AfterSalesType type, CancellationToken ct = default);
  // 3) AfterSalesEligibilityChecker 内校验
  if (order.SellerId != sellerId)
      throw new ReviewDomainException("SellerId 与订单实际卖家不符", "AFTERSALES_SELLER_MISMATCH");
  // 4) 应用层忽略 dto.SellerId，直接使用 order.SellerId 创建聚合
  ```

### 2.2 评价提交 SpuId / SkuId 由客户端伪造，可污染商品评分
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L40-L54  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/ReviewDtos.cs#L8-L17  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IReviewEligibilityChecker.cs#L7-L16  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs#L36-L67
- **类别**：A1 / A5 / B2
- **现象**：`SubmitReviewDto.SpuId` / `SkuId` 由客户端传入，`ReviewAppService.SubmitReviewAsync` 第 46 行直接透传给 `ReviewAggregate.Create`。`IReviewEligibilityChecker.EnsureEligibleAsync(orderId, orderLineId, userId)` 签名根本不带 `spuId`/`skuId`，`ReviewEligibilityChecker` 实现也未从 `OrderStatusInfo.Items` 中按 `orderLineId` 反查真实 `SkuId` 做比对（`OrderItemStatusInfo.SkuId` 字段已具备但完全未被使用）。
- **影响**：
  1. 恶意买家完成一笔便宜订单后，可对该订单行提交评价时把 `SpuId` 改成任意商品（例如自家商品、竞争对手商品），1 星差评可以打压竞品，5 星好评可以刷自家商品。
  2. `ReviewSubmittedDomainEvent` 携带伪造 `SpuId`，商品域据此重算评分摘要，竞品评分会被污染。
  3. ES 读模型同步消费者会按伪造 `SpuId` 写入索引，商品详情页评价列表错乱。
  4. `GetReviewsBySpuAsync` 端点会返回错误商品的评价。
- **修复建议**：
  ```csharp
  // 1) 让 IReviewEligibilityChecker 接收 dto.SpuId/SkuId 并由 checker 校验
  Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, Guid spuId, Guid skuId, CancellationToken ct = default);
  // 2) ReviewEligibilityChecker 内按 OrderLineId 查 OrderItemStatusInfo
  var lineItem = order.Items.FirstOrDefault(i => i.OrderLineId == orderLineId)
      ?? throw new ReviewDomainException("订单行不存在", "REVIEW_ORDER_LINE_NOT_FOUND");
  if (lineItem.SkuId != skuId)
      throw new ReviewDomainException("SkuId 与订单行不符", "REVIEW_SKU_MISMATCH");
  // SpuId 同理（需要 OrderItemStatusInfo 增加 SpuId 字段，或通过 Product BC 反查）
  // 3) 应用层忽略 dto.SpuId/SkuId，使用 order 中真实值
  ```

### 2.3 AfterSales.Cancel / MarkRefundFailed 领域事件缺失，下游无感知
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L11-L73
- **类别**：A4
- **现象**：`AfterSales` 聚合的 `MarkRefundFailed`（388-399 行）和 `Cancel`（445-462 行）方法均未调用 `AddDomainEvent`，对比同聚合的 `Approve/Reject/ReturnGoods/ConfirmReturn/MarkRefundCompleted/AddRefundRequestedEvent` 都发布了对应领域事件。`ReviewAfterSalesIntegrationEventMapper` 中也无对应集成事件映射规则。
- **影响**：
  1. **Cancel 缺事件**：买家撤销售后单后，卖家端不会被通知，卖家可能仍按原计划备货/审核，造成运营成本。消息通知域无法推送撤销通知。
  2. **MarkRefundFailed 缺事件**：退款失败后买家不会被通知，需要买家主动查询才能发现退款失败。促销域（退还优惠券）/订单域（恢复销量）/消息通知域都不会回滚或告警，售后单陷入 `Failed` 终态却无人知晓。
  3. 系统管理员无法通过事件流追溯失败/撤销原因，只能查数据库。
- **修复建议**：
  ```csharp
  public void MarkRefundFailed(string reason)
  {
      if (Status != AfterSalesStatus.Refunding)
          throw new ReviewDomainException($"当前状态 {Status} 不可标记退款失败，仅 Refunding 可标记", "AFTERSALES_REFUND_FAILED_STATUS_INVALID");
      if (string.IsNullOrWhiteSpace(reason))
          throw new ReviewDomainException("失败原因不可为空", "AFTERSALES_FAIL_REASON_EMPTY");
      Status = AfterSalesStatus.Failed;
      FailReason = reason;
      AddDomainEvent(new AfterSalesRefundFailedDomainEvent(Id, OrderId, UserId, reason));
  }

  public void Cancel(Guid userId, string reason)
  {
      if (Status != AfterSalesStatus.Pending && Status != AfterSalesStatus.Approved)
          throw new ReviewDomainException($"当前状态 {Status} 不可撤销，仅 Pending 或 Approved 可撤销", "AFTERSALES_CANCEL_STATUS_INVALID");
      if (userId == Guid.Empty)
          throw new ReviewDomainException("UserId 不可为空", "AFTERSALES_USER_EMPTY");
      if (userId != UserId)
          throw new ReviewDomainException("仅申请人可撤销售后单", "AFTERSALES_CANCEL_NOT_OWNER");
      Status = AfterSalesStatus.Cancelled;
      CancelledAt = DateTime.UtcNow;
      CancelReason = reason;
      AddDomainEvent(new AfterSalesCancelledDomainEvent(Id, OrderId, UserId, SellerId, reason));
  }
  // 并在 ReviewAfterSalesIntegrationEventMapper 注册对应的集成事件映射
  ```

### 2.4 RefundSucceededEventConsumer 未保存渠道退款单号 ChannelRefundNo
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L67  
  file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L355-L382
- **类别**：A4 / A6 / B7
- **现象**：`RefundSucceededEventConsumer` 第 67 行调用 `afterSales.MarkRefundCompleted(integrationEvent.RefundId, integrationEvent.RefundAmount, channelRefundNo: null)`，第三个参数硬编码为 `null`。但 `AfterSales.MarkRefundCompleted` 第 380 行 `ChannelRefundNo = channelRefundNo;` 是会持久化该字段的，配置层 `AfterSalesConfiguration` 第 36 行也映射了 `channel_refund_no` 列（最长 128）。根因是 `RefundCompletedEvent` 契约（PaymentEvents.cs 第 107-163 行）根本没有 `ChannelRefundNo` 字段。
- **影响**：
  1. 售后单聚合的 `ChannelRefundNo` 永远为 `null`，无法对账第三方支付渠道（微信支付、支付宝）的真实退款流水号。
  2. 财务对账、退款冲正、用户投诉举证时无法追溯到渠道侧退款凭证。
  3. 后续若调用渠道侧"查询退款"接口需要渠道退款单号，本域无法提供。
- **修复建议**：
  ```csharp
  // 1) 扩展 RefundCompletedEvent 契约
  public sealed class RefundCompletedEvent : IntegrationEventBase
  {
      public string ChannelRefundNo { get; init; } = string.Empty;  // 新增
      // ...既有字段
  }
  // 2) Payment BC 在 RefundOrder 聚合发布事件时填充 ChannelRefundNo
  // 3) RefundSucceededEventConsumer 透传
  afterSales.MarkRefundCompleted(integrationEvent.RefundId, integrationEvent.RefundAmount, integrationEvent.ChannelRefundNo);
  ```

### 2.5 ReviewGrpcService Guid→long 转换使用 GetHashCode 严重失真
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L75-L103  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L40-L43
- **类别**：A1 / A6
- **现象**：`MapToProto` 在第 78 行和第 95 行使用 `SpuId = (long)dto.SpuId.GetHashCode()` 把 `Guid` 转 `long`，注释自欺欺人地标"向后兼容"。.NET 中 `Guid.GetHashCode()` 返回 `int`（仅 32 位），且**进程内随机化**（不同进程对同一 Guid 返回不同 hash），不同 Guid 完全可能产生相同 hash（碰撞）。请求路径第 42 行 `spuId = new Guid((int)request.SpuId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)` 把 `long` 强转 `int`（高位截断）后嵌入 Guid 低 4 字节。请求路径与响应路径**完全不是互逆运算**：客户端发送 `SpuId=42`，服务端构造 Guid，响应时 `GetHashCode()` 返回一个完全无关的 int 值再 cast 为 long。
- **影响**：
  1. 旧客户端（依赖 `SpuId` int64 字段）拿到的 SpuId 是**垃圾值**，无法关联到正确的商品。
  2. 多实例部署下，同一 SpuId 在不同实例上返回不同的 long 值，客户端缓存/去重彻底失效。
  3. 不同 SpuId 可能碰撞到同一 long 值，跨 BC 数据关联错误。
- **修复建议**：
  ```csharp
  // 不要再用 GetHashCode。在 proto 中保留 SpuIdStr 为唯一权威字段，旧 int64 字段直接返回 0 或抛弃：
  private static ProductRating MapToProto(ProductRatingDto dto) => new()
  {
      SpuId = 0,                                  // 已 deprecated，强制新客户端读 SpuIdStr
      SpuIdStr = dto.SpuId.ToString(),
      AverageRating = dto.AverageRating,
      TotalCount = dto.TotalCount,
      PositiveCount = dto.PositiveCount
  };
  // 或在迁移期间用稳定哈希（如 xxHash3）替代 GetHashCode，至少保证跨进程一致：
  SpuId = BitConverter.ToInt64(xxHash3.Hash(dto.SpuId.ToByteArray(), 8).AsSpan(0, 8))
  // 同时修复请求路径：拒绝接收非零 SpuId 的旧客户端，要求 SpuIdStr
  ```

### 2.6 买家撤销售后 / 买家退货 / 卖家驳回售后均缺失申请人/卖家归属校验
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L105-L113  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L183-L202  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L276-L306  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462
- **类别**：A1 / B2
- **现象**：
  - `RejectAfterSalesAsync`（105-113 行）只调用 `afterSales.Reject(operatorId, reason)`，**没有调用** `RequireOwnedAfterSales`，对比同类的 `ApproveAfterSalesAsync`（第 76 行）和 `ConfirmReturnAsync`（第 122 行）都做了归属校验。
  - `ReturnGoodsAsync`（183-191 行）入参 `userId` 但完全未使用，方法体内只调用 `afterSales.ReturnGoods(trackingNo)`，聚合的 `ReturnGoods` 方法（276-306 行）也不接收 `userId`，不校验调用者是否为 `afterSales.UserId`。
  - `CancelAfterSalesAsync`（194-202 行）调用 `afterSales.Cancel(userId, reason)`，但 `AfterSales.Cancel`（445-462 行）只校验 `userId != Guid.Empty`，**不校验** `userId == this.UserId`，任意买家传入自己的 userId 即可撤销他人的售后单。
- **影响**：
  1. **卖家驳回越权**：卖家 A 可调用 `POST /api/seller/after-sales/{id}/reject` 驳回任何售后单，包括属于卖家 B 的单，造成买家被无故拒绝。
  2. **买家退货越权**：买家 A 可调用 `POST /api/after-sales/{id}/return-goods` 对他人售后单填写物流单号，把他人 Pending→Approved 的售后单错误推进到 ReturnGoods 状态。
  3. **买家撤销越权**：买家 A 可调用 `POST /api/after-sales/{id}/cancel` 撤销他人售后单，他人退款流程被恶意中断。
- **修复建议**：
  ```csharp
  // 1) RejectAfterSalesAsync 增加归属校验
  public async Task RejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default)
  {
      var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
          ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");
      RequireOwnedAfterSales(afterSales, operatorId);   // 新增
      afterSales.Reject(operatorId, reason);
      // ...
  }
  // 2) ReturnGoodsAsync 校验买家归属
  public async Task ReturnGoodsAsync(Guid afterSalesId, Guid userId, string trackingNo, CancellationToken ct = default)
  {
      var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
          ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");
      if (afterSales.UserId != userId)
          throw new ReviewDomainException("无权操作此售后单", "AFTERSALES_NOT_OWNED");
      afterSales.ReturnGoods(trackingNo);
      // ...
  }
  // 3) AfterSales.Cancel 在聚合内部校验
  public void Cancel(Guid userId, string reason)
  {
      // ...既有状态校验
      if (userId != UserId)
          throw new ReviewDomainException("仅申请人可撤销售后单", "AFTERSALES_CANCEL_NOT_OWNER");
      // ...
  }
  ```

### 2.7 SellerReply 完全缺失卖家归属校验，任意卖家可回复任意评价
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L125-L133  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L57-L65  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs#L174-L194
- **类别**：A1 / B2
- **现象**：`ReviewsController.SellerReplyAsync` 端点 `[Authorize(Roles = "Seller")]`，但控制器只调用 `_reviewAppService.SellerReplyAsync(id, dto.Content, ct)`，**完全不传当前卖家标识**。应用服务 `SellerReplyAsync` 也不接收 `sellerId` 参数，聚合 `Review.SellerReply(content)` 同样不接收 `sellerId`，不校验回复者是否为该商品的实际卖家。`Review` 聚合本身也没有 `SellerId` 字段，无从校验。
- **影响**：
  1. 任意卖家可对竞品商品评价进行嘲讽、推卸责任、广告植入等恶意回复。
  2. 评价回复无审计字段（无 `SellerReplyBy` / `SellerReplyAt`），事后无法追溯是谁回复的。
  3. 评价被恶意回复后，买家体验受损，平台公信力下降。
- **修复建议**：
  ```csharp
  // 1) Review 聚合增加 SellerId 字段（Create 时由应用层从订单/商品域查询并传入）
  // 2) SellerReply 方法接收 sellerId 并校验
  public void SellerReply(Guid sellerId, string content)
  {
      if (Status != ReviewStatus.Approved)
          throw new ReviewDomainException($"当前状态 {Status} 不可回复，仅 Approved 可回复", "REVIEW_REPLY_STATUS_INVALID");
      if (sellerId != SellerId)
          throw new ReviewDomainException("无权回复此评价", "REVIEW_NOT_OWNED");
      if (string.IsNullOrWhiteSpace(content))
          throw new ReviewDomainException("回复内容不可为空", "REVIEW_REPLY_EMPTY");
      if (content.Length > 500)
          throw new ReviewDomainException("回复内容不可超过 500 字", "REVIEW_REPLY_TOO_LONG");
      SellerReplyContent = content;
      SellerReplyBy = sellerId;
      SellerReplyAt = DateTime.UtcNow;
  }
  // 3) 应用服务透传 sellerId
  // 4) 控制器读取 GetCurrentUserId() 并透传
  ```

### 2.8 聚合内部 List 通过 Images 属性直接暴露，外部可绕过聚合方法修改状态
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L43-L44  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs#L40-L41  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L262  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L132
- **类别**：B6 / B5
- **现象**：两个聚合根的 `Images` 属性都声明为 `public List<string> Images { get => _images; private set => _images = value ?? []; }`。getter 直接返回内部 `_images` 引用，调用方可直接 `afterSales.Images.Add(...)` / `afterSales.Images.Clear()` / `afterSales.Images[0] = "..."` 绕过任何聚合不变量修改状态。更严重的是 `AfterSalesAppService.ToDto` 第 262 行 `Images = afterSales.Images` 和 `ReviewAppService.ToDto` 第 132 行 `Images = review.Images` 都把内部 List **同一个引用**赋给 DTO，DTO 序列化或被外部 mutate 时会反向污染聚合状态。
- **影响**：
  1. 聚合不变量（最多 5/9 张、URL 校验等）形同虚设，外部任何代码都可绕过。
  2. DTO 与聚合共享 List 引用，DTO 修改时聚合状态被污染，下次 SaveChangesAsync 时持久化错误数据。
  3. 多线程场景下，DTO 序列化与聚合方法同时遍历 List 会抛 `InvalidOperationException: Collection was modified`。
- **修复建议**：
  ```csharp
  // 1) 聚合暴露为只读集合
  private List<string> _images = [];
  public IReadOnlyList<string> Images => _images.AsReadOnly();
  // EF Core 通过 backing field 配置：
  // builder.Property<string>("_images").HasColumnName("images")... 或继续用 HasConversion 配置 backing field
  // 2) ToDto 复制一份新 List
  Images = afterSales.Images.ToList(),   // 防御性拷贝
  ```

### 2.9 HasActiveByOrderLineAsync 活跃状态过滤不全，允许同订单行重复售后
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L34-L45  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/ValueObjects/AfterSalesEnums.cs#L24-L52
- **类别**：A1 / A2
- **现象**：`HasActiveByOrderLineAsync` 把"进行中"的售后单状态硬编码为 `[Pending, Approved, Refunding]`（第 36-41 行），但 `AfterSalesStatus` 枚举中 `ReturnGoods=7` 和 `ConfirmReturn=8` 也是"进行中"状态（买家已寄回商品待卖家确认 / 卖家已确认待退款）。这两个状态被遗漏。
- **影响**：
  1. 买家对订单行 L 提交售后单 AS1（ReturnRefund），审核通过后买家寄回商品，AS1 进入 `ReturnGoods` 状态。
  2. 买家再次对同一订单行 L 提交售后单 AS2（ReturnRefund），`HasActiveByOrderLineAsync` 查不到活跃单（AS1 已不在过滤列表），通过资格校验，AS2 被创建。
  3. 系统中出现两个进行中的售后单，退款可能被重复执行，资损。
  4. 整单售后（`orderLineId == null`）时根本不调用本方法，可无限重复提交。
- **修复建议**：
  ```csharp
  var activeStatuses = new List<AfterSalesStatus>
  {
      AfterSalesStatus.Pending,
      AfterSalesStatus.Approved,
      AfterSalesStatus.ReturnGoods,        // 新增
      AfterSalesStatus.ConfirmReturn,      // 新增
      AfterSalesStatus.Refunding
  };
  // 同时对 orderLineId == null 的整单售后增加去重检查（按 OrderId + type + activeStatuses）
  // 或在数据库上为 (order_line_id, type, status) 建立部分唯一索引
  ```

### 2.10 买家按订单查询售后单 / 按订单行查询评价均缺失订单归属校验
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L69-L77  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L47-L55  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L205-L209  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L97-L102
- **类别**：A1 / B2
- **现象**：`AfterSalesController.GetAfterSalesByOrderAsync` 端点 `[Authorize(Roles = "Buyer")]`，但只接收 `orderId` 路径参数，**完全不校验**当前买家是否拥有该订单。应用服务 `GetByOrderIdAsync` 也仅按 `orderId` 查询返回所有售后单。`ReviewsController.GetReviewByOrderLineAsync` 同样问题：按 `orderLineId` 查询评价，且仓储 `GetByOrderLineAsync` 不按 `ReviewStatus` 过滤，会返回 `Hidden` 状态的评价。
- **影响**：
  1. 买家 A 可枚举/猜测 `orderId` 调用 `GET /api/after-sales/order/{orderId}` 查看买家 B 的售后申请详情，泄露申请原因、物流单号、退款金额、驳回原因等敏感信息。
  2. 买家 A 可调用 `GET /api/reviews/order-line/{orderLineId}` 查看 `Hidden` 状态的评价（运营隐藏的违规评价），绕过运营审核结果，泄露审核原因。
  3. 普通买家可借此做竞品调研（查他人售后投诉率、退款金额等）。
- **修复建议**：
  ```csharp
  // 1) AfterSalesController 增加归属校验
  [Authorize(Roles = "Buyer")]
  [HttpGet("api/after-sales/order/{orderId:guid}")]
  public async Task<IActionResult> GetAfterSalesByOrderAsync(Guid orderId, CancellationToken ct)
  {
      var userId = GetCurrentUserId();
      var result = await _afterSalesAppService.GetByOrderIdForUserAsync(orderId, userId, ct);  // 新方法
      return Ok(ApiResponse.Success(result));
  }
  // 应用层：
  public async Task<List<AfterSalesDto>> GetByOrderIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct)
  {
      // 通过 IOrderStatusProvider 校验订单归属
      var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct)
          ?? throw new InvalidOperationException("订单不存在");
      if (order.UserId != userId)
          throw new ReviewDomainException("无权查询此订单售后", "AFTERSALES_FORBIDDEN");
      var items = await _afterSalesRepository.GetByOrderIdAsync(orderId, ct);
      return items.ConvertAll(ToDto);
  }
  // 2) ReviewsController.GetReviewByOrderLineAsync 同样校验，并仅返回 Approved 评价或仅本人评价
  ```

### 2.11 RefundCompleted 事件回环：本 BC 发布的 RefundCompletedEvent 会被自身消费者重复消费
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L43-L46  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L15-L74  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L355-L382
- **类别**：A4 / C8
- **现象**：Payment BC 退款成功 → 发布 `RefundCompletedEvent`（EventId=A）→ ReviewAfterSales BC 的 `RefundSucceededEventConsumer` 消费 → 调用 `afterSales.MarkRefundCompleted` → 聚合发布 `AfterSalesRefundCompletedDomainEvent` → mapper 第 43-46 行翻译为 `RefundCompletedEvent`（EventId=B）→ 经发件箱对外发布 → ReviewAfterSales BC 的 `RefundSucceededEventConsumer` 再次消费 EventId=B → 状态检查发现已 Completed 跳过（但已浪费一次 DB 查询 + 幂等存储写入）→ 同时其他 BC（Order/Promotion/Notification）也消费 EventId=B，与消费 EventId=A 的逻辑可能重复执行（如订单销量回滚两次）。
- **影响**：
  1. ReviewAfterSales BC 自身做了一次无意义的 DB 查询与幂等写入。
  2. 其他 BC 收到两个 RefundCompletedEvent（一个来自 Payment、一个来自 ReviewAfterSales），如果它们没做幂等，会重复执行副作用（如订单销量被回滚两次、优惠券被退还两次）。
  3. 事件溯源/审计日志中同一退款事实出现两次，干扰对账。
- **修复建议**：
  ```csharp
  // 1) 推荐：ReviewAfterSales BC 不再发布 RefundCompletedEvent，改用独立的 AfterSalesRefundCompletedEvent
  // 在 SharedContracts 新增独立集成事件
  public sealed class AfterSalesRefundCompletedEvent : IntegrationEventBase { /* ... */ }
  // mapper 中改为
  RegisterHandler<AfterSalesRefundCompletedDomainEvent, AfterSalesRefundCompletedEvent>(e => ...);
  // 2) 或在 RefundSucceededEventConsumer 中按 source/origin 字段过滤自身发布的事件
  // 3) 强制所有消费方做幂等（既检查 EventId，也检查业务状态）
  ```

## 3. 🟡 中风险问题

### 3.1 AfterSales.Reject 误用 ApprovedAt 字段记录驳回时间
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L241-L270
- **类别**：A1 / B5
- **现象**：`Reject` 方法第 268 行 `ApprovedAt = DateTime.UtcNow;`，但语义上 `ApprovedAt` 应为"审核同意时间"。驳回时也写入该字段会让数据消费者无法区分"何时被审核"与"审核结果"。
- **影响**：审计/报表把驳回时间当作同意时间统计，售后审核 SLA 数据失真；买家侧 UI 若显示"审核时间"会让买家误以为被同意过。
- **修复建议**：新增 `AuditedAt` 字段统一记录审核时间，或新增 `RejectedAt` 字段；同时校验 `ApprovedAt` 在驳回路径下保持 null。

### 3.2 AfterSales.ConfirmReturn 未记录操作人，审计缺失
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L311-L324  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L116-L141
- **类别**：A1
- **现象**：`AfterSales.ConfirmReturn()` 方法不接收 `operatorId` 参数，聚合无 `ReturnConfirmedBy` 字段。`AfterSalesAppService.ConfirmReturnAsync` 虽然传了 `operatorId` 但只用于 `RequireOwnedAfterSales`，未写入聚合。
- **影响**：卖家确认收货后无法追溯是谁确认的，争议时无审计证据。
- **修复建议**：方法签名增加 `Guid operatorId`，聚合新增 `ReturnConfirmedBy` 字段并在方法内赋值。

### 3.3 整单售后（orderLineId 为 null）不做重复申请校验
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L65-L72  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L21-L22
- **类别**：A1 / A2
- **现象**：`EnsureEligibleAsync` 第 65 行 `if (orderLineId.HasValue)` 才去调用 `HasActiveByOrderLineAsync`。当 `orderLineId == null`（整单售后）时，完全跳过去重检查。买家可对同一订单无限提交整单售后。
- **影响**：买家对订单 O 反复提交整单 ReturnRefund 售后，每个都通过审核走退款流程，资损。
- **修复建议**：新增 `HasActiveByOrderAsync(orderId, type)` 方法，整单售后场景下按 `OrderId + OrderLineId IS NULL + Type + activeStatuses` 去重。

### 3.4 ReviewInternalQueryService.GetProductRatingAsync 加载全部 Approved 评价到内存计算聚合
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L21-L41  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IReviewRepository.cs#L40-L46  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L64-L75
- **类别**：C2 / C1
- **现象**：`GetProductRatingAsync` 调用 `_reviewRepository.GetBySpuIdAsync(spuId, ReviewStatus.Approved, ct)` 加载该 SPU 下**全部**已通过评价到内存（无分页），再在内存中 `Count`/`Average`/`Count(r => r.Rating >= 4)`。`GetBySpuIdAsync` 仓储实现也未做 `AsNoTracking`，每个评价都被 change tracker 跟踪。
- **影响**：爆款商品评价数过万时，单次 gRPC 调用可能加载几万条记录到内存，CPU/内存暴增，DB 连接长时间占用，可能触发 OOM。
- **修复建议**：
  ```csharp
  // 仓储层新增聚合查询接口
  Task<ProductRatingSnapshot?> GetRatingSnapshotAsync(Guid spuId, CancellationToken ct);
  // 实现使用 SQL 聚合
  SELECT COUNT(*) AS TotalCount,
         AVG(CAST(rating AS FLOAT)) AS AverageRating,
         SUM(CASE WHEN rating >= 4 THEN 1 ELSE 0 END) AS PositiveCount
  FROM reviews WHERE spu_id = @spuId AND status = 1 AND is_deleted = 0
  ```

### 3.5 GrpcOrderStatusProvider 返回 OrderLineId=Guid.Empty 且 SkuId 可能丢失
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs#L60-L90  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/HttpOrderStatusProvider.cs#L68-L82
- **类别**：A5 / B7
- **现象**：`GrpcOrderStatusProvider.MapToInfo` 第 78-86 行注释明示"proto OrderItem 无 order_line_id 字段，POC 简化为 Guid.Empty"，把所有 `OrderItemStatusInfo.OrderLineId` 都填成 `Guid.Empty`。SkuId 第 83 行只读 `SkuIdStr`，若 gRPC 服务端未填则也是 `Guid.Empty`。同时 `OrderId` 解析失败时第 73 行静默返回 `Guid.Empty`，`UserId` 第 66 行同样静默返回 `Guid.Empty`，不抛错。
- **影响**：当前 `AfterSalesEligibilityChecker` / `ReviewEligibilityChecker` 没用 `Items` 校验，所以暂时未爆炸。一旦按 2.1/2.2 修复增加校验，gRPC 路径下所有请求都会因 `OrderLineId=Guid.Empty` 或 `UserId=Guid.Empty` 而拒绝或匹配到错误的订单。
- **修复建议**：
  ```csharp
  // 1) proto 契约补 order_line_id 字段
  // 2) 关键字段解析失败抛 AntiCorruptionException 而非静默 Guid.Empty
  OrderId = Guid.TryParse(proto.OrderId, out var oid)
      ? oid
      : throw new AntiCorruptionException($"订单域返回无效 OrderId: {proto.OrderId}", "ORDER_REMOTE_FAILED")
  ```

### 3.6 ReviewReadModelSyncConsumer 未实现 EventId 幂等去重
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L57  
  file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L16-L74
- **类别**：A3 / C8
- **现象**：`ReviewReadModelSyncConsumer` 直接实现 `IConsumer<T>` 而非继承 `IntegrationEventConsumerBase<T>`，未注入 `IIdempotencyStore`，未做 EventId 幂等。虽然 ES `IndexAsync` 是 upsert 天然幂等，但每次重复消费都会触发一次 DB 查询 + ES 索引写入 + 日志记录，浪费资源。
- **影响**：MassTransit 重试或 Outbox 重复发布时，每次重试都执行一次完整 DB+ES 调用；高并发下可能放大 DB/ES 压力。
- **修复建议**：改为继承 `IntegrationEventConsumerBase<ReviewSubmittedEvent>` 等，或实现 `IsProcessedAsync`/`MarkAsProcessedAsync` 委托给 `IIdempotencyStore`。

### 3.7 ApproveAfterSalesAsync 在数据库事务内执行远程支付查询，长事务持锁
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L70-L102  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L116-L141  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L144-L169
- **类别**：C5 / C8
- **现象**：`ApproveAfterSalesAsync`（仅退款分支）、`ConfirmReturnAsync`、`AdminApproveAfterSalesAsync` 都在调用 `afterSales.Approve(...)` 后立即在同一事务内调用 `_paymentInfoQueryService.GetByOrderIdAsync`（远程 HTTP/gRPC 调用，可能数秒），之后才调用 `SaveEntitiesAsync`。整个远程调用期间 `after_sales` 行被事务锁定（rowversion + UPDATE 锁）。
- **影响**：
  1. 远程支付域超时（默认 30s）期间，售后单行被锁死，其他卖家/运营对该售后单的查询/操作都会等待。
  2. 高并发审核场景下连接池耗尽。
  3. 远程失败导致整个事务回滚，已 Approve 的状态变更丢失，买家看到"还在 Pending"而非"审核通过待退款"。
- **修复建议**：拆分事务：先 `Approve + SaveEntities`，再异步发起退款流程（独立事务查询 Payment + 发 RefundRequested 事件）；或预先在资格校验阶段缓存 PaymentId。

### 3.8 仓储层全部未使用 AsNoTracking，只读查询进入 Change Tracker
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L22-L127  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L22-L134
- **类别**：C1
- **现象**：所有 `GetByOrderIdAsync` / `QueryAsync` / `GetBySpuIdAsync` / `GetByOrderIdAsync` / `ExistsByOrderLineAsync` / `HasActiveByOrderLineAsync` / `CountAsync` 都直接 `_context.AfterSales.Where(...)`，未 `.AsNoTracking()`。`ReviewReadModelSyncConsumer.BuildReadModelAsync` 调用 `GetByIdAsync` 也是 tracked。
- **影响**：每次只读查询都把实体加入 Change Tracker，内存占用升高，SaveChanges 时遍历变更集变慢；高并发查询场景下 GC 压力显著。
- **修复建议**：只读路径全部加 `.AsNoTracking()`，或拆分 `IReviewQueryStore` 接口专门处理只读。

### 3.9 订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L17-L18  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs#L16
- **类别**：B7 / A5
- **现象**：两个 eligibility checker 都用 `private const int OrderStatusShipped = 2;` / `OrderStatusCompleted = 3;` 硬编码订单状态。订单域若调整状态枚举（插入新状态、重排序号），本域会静默错配。
- **影响**：订单域调整状态码后，本域可能把"已发货"识别为"待支付"，导致售后/评价入口在错误时间开放或关闭。
- **修复建议**：在 `Leno.SharedContracts` 中定义 `OrderStatus` 共享枚举，所有 BC 引用该枚举而非魔法数。

### 3.10 上传图片流未 using，依赖框架兜底
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L129  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L116
- **类别**：A8
- **现象**：`UploadAfterSalesImagesAsync` 第 129 行 `await _fileStorage.UploadAsync(file.OpenReadStream(), ...)` 与 ReviewsController 第 116 行同样模式，`file.OpenReadStream()` 返回的 Stream 未包裹 `using`。`IFileStorageService.UploadAsync` 契约未约定是否会 dispose stream。
- **影响**：若文件存储实现未 dispose，每次上传都泄漏一个 stream 引用直到 GC；大文件上传并发时可能耗尽文件句柄。IFormFile 底层流通常随请求释放兜底，但仍是坏味道。
- **修复建议**：
  ```csharp
  await using var stream = file.OpenReadStream();
  var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "aftersales", ct);
  ```

### 3.11 图片上传仅校验扩展名，未校验文件内容/Magic Number
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L90-L134  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L77-L121
- **类别**：A6 / B2
- **现象**：两个上传端点都用 `Path.GetExtension(file.FileName)` 校验白名单扩展名（.jpg/.jpeg/.png/.webp）+ 文件大小（5MB），但未读取文件头部 Magic Number 验证实际是图片。`file.ContentType` 也来自客户端，未与服务端校验对齐。
- **影响**：攻击者可上传伪装成 .jpg 的 SVG（含 JS）/HTML/EXE，存储后通过 CDN 直链触发 MIME sniffing 攻击（XSS）。
- **修复建议**：读取前 512 字节用 `FileSignatureDetector` 校验图片 magic number；强制下载时设置 `Content-Disposition: attachment` 与 `X-Content-Type-Options: nosniff`。

### 3.12 ReviewReadModelSyncConsumer 不处理评价被删除场景
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L107
- **类别**：A4 / C8
- **现象**：消费者只订阅 `ReviewSubmittedEvent` / `ReviewApprovedEvent` / `ReviewHiddenEvent` 三种事件，没有 `ReviewDeletedEvent`（聚合也未支持删除）。Hidden 后 ES 文档仍保留可被搜索。
- **影响**：评价被隐藏后 ES 中仍可被检索（取决于 ES 查询是否过滤 Status），与"运营隐藏违规评价"语义不一致。
- **修复建议**：Hidden 事件触发时把 ES 文档 Status 字段更新为 "Hidden"，并要求 ES 查询默认过滤 `Status != "Hidden"`；或直接 DeleteAsync 从 ES 删除文档。

## 4. 🟢 低风险问题

### 4.1 OrderCompletedEventConsumer 仅打日志无副作用
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/OrderCompletedEventConsumer.cs#L14-L33
- **类别**：C8
- **现象**：消费者只调用 `Logger.LogInformation` 后 return Task.CompletedTask。注释说"实际评价资格校验在评价提交时执行"。但既然如此，该消费者存在的意义仅是占位，仍占用 MassTransit 消费线程与幂等存储写入。
- **影响**：无功能影响，但浪费消息中间件资源。
- **修复建议**：删除该消费者，或在消费者中维护"可评价订单行"读模型以加速资格校验。

### 4.2 MarkRefundFailed 与 Cancel 未校验 reason 是否为 null/空
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462
- **类别**：A6
- **现象**：`MarkRefundFailed(string reason)` 直接 `FailReason = reason;`，未校验 `reason` 是否为 null/空。`Cancel(Guid userId, string reason)` 同样直接 `CancelReason = reason;`，未校验。
- **影响**：FailReason/CancelReason 可能为 null，UI 显示异常；日志/审计追溯失败原因时缺数据。
- **修复建议**：增加 `if (string.IsNullOrWhiteSpace(reason)) throw new ReviewDomainException(...)` 校验。

### 4.3 AfterSales.Create 接收 images 列表共享引用给内部 _images
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L169-L194
- **类别**：B6 / B5
- **现象**：第 169 行 `var imageList = images ?? [];` 第 193 行 `Images = imageList`（private setter 把 `_images = imageList`）。`_images` 与调用方传入的 `images` 是同一引用。调用方在 Create 后 mutate `images` 会污染聚合。
- **影响**：与 2.8 同源问题。
- **修复建议**：`var imageList = (images ?? []).ToList();` 防御性拷贝。

### 4.4 AntiCorruptionOptions 解析在 UseGrpc=true 时硬抛异常，无降级
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L66-L105
- **类别**：C8
- **现象**：第 70 行 `?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Payment 配置缺失")`，若 Consul KV 未及时同步，应用启动直接失败，无降级到 HttpClient 模式。
- **影响**：配置中心故障导致服务无法启动。
- **修复建议**：缺失 gRPC endpoint 时记录警告并降级到 HttpClient 模式。

### 4.5 RefundFailedEventConsumer 失败原因未做长度校验
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundFailedEventConsumer.cs#L34-L74  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/AfterSalesConfiguration.cs#L38
- **类别**：A6
- **现象**：消费者第 67 行 `afterSales.MarkRefundFailed(integrationEvent.Reason);` 未校验 `Reason` 长度。聚合的 `MarkRefundFailed` 也不校验（见 4.2）。但 `AfterSalesConfiguration` 第 38 行 `FailReason` 限制 `HasMaxLength(512)`。若 Reason 超过 512 字符，EF Core 在 SaveChanges 时抛 `DbUpdateException`，被 MassTransit 重试，仍失败进入死信队列。
- **影响**：异常路径绕过领域校验直接打 DB 层错误，重试无意义浪费资源。
- **修复建议**：聚合层 `MarkRefundFailed` 校验 `reason.Length <= 512`，超长抛 `ReviewDomainException`。

### 4.6 ApplyFilters 中 status.HasValue 与 status.Value 的冗余判断
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L99-L127  
  file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L112-L134
- **类别**：C1
- **现象**：`ApplyFilters` 多次 `if (xx.HasValue) query = query.Where(... .Value)`，EF Core 9+ 已能正确翻译 Nullable 的 Where 条件，可省略 HasValue 判断直接 `Where(x => x.SellerId == sellerId)`。
- **影响**：代码冗余，无功能问题。
- **修复建议**：简化为 `if (sellerId is not null) query = query.Where(a => a.SellerId == sellerId.Value);` 或直接传 `Guid sellerId` 重载。

### 4.7 ReviewInternalQueryService.GetOrderReviewsAsync 返回 null 而非空集合
- **文件**：file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L44-L64
- **类别**：A6
- **现象**：第 48-51 行 `if (reviews is null || reviews.Count == 0) return null;`。gRPC 服务端 `ReviewGrpcService.GetOrderReviews` 第 67-70 行据此抛 `NotFound`。语义上"订单无可见评价"和"订单不存在"是不同的，但都返回 NotFound，调用方无法区分。
- **影响**：商品域/订单域调用本服务时无法区分"无评价"与"订单不存在"，可能错误处理。
- **修复建议**：返回空 `OrderReviewsDto { Reviews = [] }` 而非 null，让调用方按 `Reviews.Count == 0` 判断。

### 4.8 RefundCompletedEvent 契约中 AfterSalesId 默认 Guid.Empty 兼容旧版
- **文件**：file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163
- **类别**：A4 / B7
- **现象**：`RefundCompletedEvent` 第 128 行 `public Guid AfterSalesId { get; init; }` 默认 `Guid.Empty`，注释说"兼容旧版消费方"。但 `RefundSucceededEventConsumer` 第 38 行 `if (integrationEvent.AfterSalesId == Guid.Empty)` 直接跳过，意味着旧版 Payment BC 发布的事件根本不会触发售后单状态更新。
- **影响**：升级期间未带上 AfterSalesId 的退款完成事件被本 BC 静默跳过，售后单永远停留在 Refunding 状态。
- **修复建议**：要求 Payment BC 必须填充 AfterSalesId（即使没有也用 Guid.Empty 但需要记录告警），或本 BC 用 OrderId+RefundId 反查关联售后单。

## 5. 修复路线建议

| 优先级 | 问题数 | 建议周期 |
|-|-|-|
| P0（必修）| 11 项（2.1-2.11）| 7 天内 |
| P1（应修）| 12 项（3.1-3.12）| 4 周内 |
| P2（建议）| 8 项（4.1-4.8）| 3 个月内 |

**P0 必修优先级排序**：
1. 2.1 / 2.2 / 2.7（资损/数据污染）- 立即修复
2. 2.6 / 2.10（越权/信息泄露）- 立即修复
3. 2.3 / 2.4 / 2.11（事件链断裂/事件回环）- 紧急修复
4. 2.5（gRPC 字段失真）- 紧急修复
5. 2.8 / 2.9（聚合不变量破坏）- 紧急修复

## 6. 附录：扫描覆盖的关键文件

### Domain 层
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/ReviewAfterSalesDomainEvents.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Exceptions/ReviewDomainException.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IAfterSalesRepository.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IReviewRepository.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IAfterSalesEligibilityChecker.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IOrderStatusProvider.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IReviewEligibilityChecker.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/ValueObjects/AfterSalesEnums.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/ValueObjects/ReviewEnums.cs

### Application 层
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IAfterSalesAppService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewAppService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewInternalQueryService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/IPaymentInfoQueryService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/AfterSalesDtos.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/ReviewDtos.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/ImageUploadDtos.cs

### Infrastructure 层
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReviewAfterSalesDbContext.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/AfterSalesConfiguration.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/OrderCompletedEventConsumer.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundFailedEventConsumer.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModel.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/HttpOrderStatusProvider.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcPaymentInfoQueryService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/OrderStatusDispatcherAdapter.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/PaymentInfoQueryDispatcherAdapter.cs

### Api 层
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewControllerBase.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs
- /workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs

### 共享契约/基础内核（仅用于理解上下文，未单独评估）
- /workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs
- /workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs
- /workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/IRepository.cs
- /workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs
- /workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs
- /workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs
- /workspace/src/BuildingBlocks/Leno.Infrastructure.Abstractions/IFileStorageService.cs
- /workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs
- /workspace/src/BuildingBlocks/Leno.SharedContracts/Events/RefundRequestedIntegrationEvent.cs

---

**特别说明**：本次扫描按用户要求重点核查了买家身份与售后单归属校验（发现 2.6 / 2.10）、SellerId 客户端伪造（发现 2.1）、评价提交 SpuId/SkuId 伪造（发现 2.2）、聚合内部 List 暴露（发现 2.8）、ReviewGrpcService Guid→long 转换（发现 2.5）、AfterSales.Cancel/MarkRefundFailed 领域事件缺失（发现 2.3）、RefundSucceededEventConsumer 渠道退款单号保存（发现 2.4）等问题，均已纳入 P0 必修清单。

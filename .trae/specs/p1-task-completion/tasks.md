# P1 任务完成 - 任务列表

> **执行模式**: Master Agent 全流程自主编排
> **任务选择**: 按优先级 P1 + 依赖关系顺序
> **总任务数**: 41 | **预计批次**: 5 批

---

## 第一批：独立无跨模块依赖（20 个任务，可并行）

### Task 1: SK-02 对象存储 MinIO 适配器

- [x] 1.1: 添加 `Minio` NuGet 包到 `Leno.Infrastructure`
- [x] 1.2: 创建 `FileStorageOptions` 配置类（Provider、MinIO 连接参数、Bucket 名称）
- [x] 1.3: 创建 `ObjectStorageService` 实现 `IFileStorageService` 全部方法
- [x] 1.4: 实现 UploadAsync/DownloadAsync/DeleteAsync/ValidateUrl/ExistsAsync
- [x] 1.5: 添加 DI 扩展方法 `AddFileStorage` 根据配置切换 Local/MinIO
- [x] 1.6: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 1.7: 运行全部测试并确保通过（23/23 passed）

### Task 2: UA-03 双因子认证 TOTP

- [x] 2.1: 添加 `Otp.NET` NuGet 包
- [x] 2.2: 在基础设施层实现 `TotpTokenVerifier` 实现 `ITokenVerifier`
- [x] 2.3: 在 User 聚合中实现 EnableTwoFactor/ConfirmTwoFactor/DisableTwoFactor
- [x] 2.4: 实现 POST 端点：enable/confirm/disable
- [x] 2.5: 登录时检测双因子启用状态，返回 twoFactorRequired 标志
- [x] 2.6: 实现 POST /api/auth/two-factor/verify 二次验证后返回 JWT
- [x] 2.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 2.8: 编写应用层单元测试
- [x] 2.9: 运行全部测试并确保通过

### Task 3: UA-04 密码找回

- [x] 3.1: 在领域层定义 `ForgotPasswordRequestedEvent` 领域事件
- [x] 3.2: 实现一次性重置令牌生成（10 分钟过期，Redis 存储）
- [x] 3.3: 实现 POST /api/auth/forgot-password（接收邮箱/手机号）
- [x] 3.4: 实现 POST /api/auth/reset-password（验证令牌 + 新密码）
- [x] 3.5: 重置令牌一次性使用，使用后立即删除（防重放）
- [x] 3.6: 密码重置后发布 PasswordChangedEvent
- [x] 3.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 3.8: 运行全部测试并确保通过

### Task 4: UA-05 RBAC 权限策略管理

- [x] 4.1: 创建 Role 实体（RoleId、Name、Description、Permissions、IsBuiltIn）
- [x] 4.2: 创建 Permission 值对象（ResourceKey 格式: api:/path 或 ui:module:action）
- [x] 4.3: 实现 IPermissionRepository 接口 + EfCorePermissionRepository
- [x] 4.4: 实现角色 CRUD API 端点（列表/新增/编辑/删除）
- [x] 4.5: 实现角色权限查看与更新端点
- [x] 4.6: 内置角色保护（Buyer/Seller/Operator/Admin 不可删除）
- [x] 4.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 4.8: 编写应用层单元测试
- [x] 4.9: 运行全部测试并确保通过（117 passed, 0 failed）

### Task 5: PRD-02 商品审核历史记录

- [x] 5.1: 创建 AuditInfo 值对象（OperatorId、OperatorName、Result、Reason、AuditedAt）
- [x] 5.2: 在 Product 聚合中维护 \_auditHistory 列表
- [x] 5.3: 修改 Approve/Reject 方法追加审核历史
- [x] 5.4: 提供 GetAuditHistory() 方法
- [x] 5.5: 配置 EF Core 值转换存储 AuditInfo 列表（JSON 列）
- [x] 5.6: 查询商品详情时返回审核历史列表
- [x] 5.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 5.8: 运行全部测试并确保通过

### Task 6: PRD-03 价格变更历史

- [x] 6.1: 创建 PriceChangeRecord 值对象（SkuId、OldPrice、NewPrice、ChangedAt、ChangedBy）
- [x] 6.2: 在 Product 聚合中维护 \_priceChangeHistory 列表
- [x] 6.3: 修改 AdjustPrice 方法追加历史记录
- [x] 6.4: 提供 GetPriceHistory(skuId) 方法
- [x] 6.5: 配置 EF Core 值转换存储价格历史列表
- [x] 6.6: 实现 GET /api/products/{id}/price-history 查询端点
- [x] 6.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 6.8: 运行全部测试并确保通过

### Task 7: PRD-05 库存补货与盘点

- [x] 7.1: 完善 Product 聚合中 UpdateStock(skuId, delta) 方法（校验结果 ≥ 0）
- [x] 7.2: 实现 POST /api/products/{id}/skus/{skuId}/stock 端点
- [x] 7.3: 发布 StockAdjustedEvent（SkuId、ProductId、Delta、NewStock）
- [x] 7.4: 在基础设施层实现 StockAdjustedEventConsumer 同步 ES 读模型
- [x] 7.5: 库存变更记录操作日志
- [x] 7.6: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 7.7: 运行全部测试并确保通过

### Task 8: ORD-04 积分抵现

- [x] 8.1: 在 Order 聚合中实现 ApplyPointsOffset(pointsOffsetAmount) 方法（已在现有代码中完整实现）
- [x] 8.2: 校验 PointsOffsetAmount ≤ ItemsAmount - DiscountAmount
- [x] 8.3: 校验单笔订单积分抵扣上限
- [x] 8.4: 更新 TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount
- [x] 8.5: 应用层下单前调用积分域确认接口冻结积分
- [x] 8.6: 支付成功时通过 OrderPaidEvent 驱动积分域正式扣减
- [x] 8.7: 订单取消时通过 OrderCancelledEvent 驱动积分域释放冻结积分
- [x] 8.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 8.9: 编写应用层单元测试
- [x] 8.10: 运行全部测试并确保通过

### Task 9: ORD-05 优惠分摊

- [x] 9.1: 在 Order 聚合中实现 ApplyDiscount(discountAmount, discountAllocations) 方法（已在现有代码中完整实现）
- [x] 9.2: 校验各行分摊之和等于优惠总额
- [x] 9.3: 校验各行分摊不超行小计
- [x] 9.4: 更新各 OrderItem 的 DiscountAllocation 字段
- [x] 9.5: 更新 DiscountAmount 与 TotalAmount
- [x] 9.6: 下单时调用促销域计算结果后应用分摊
- [x] 9.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 9.8: 运行全部测试并确保通过

### Task 10: ORD-08 物流轨迹查询

- [x] 10.1: 在领域层定义 ILogisticsTrackingService 接口
- [x] 10.2: 在基础设施层实现物流公司 API 适配器
- [x] 10.3: 实现 GET /api/orders/{id}/logistics-trace 端点
- [x] 10.4: 校验物流公司 SupportTracking 属性
- [x] 10.5: 物流轨迹缓存到 Redis（TTL 1 小时）
- [x] 10.6: 轨迹查询失败时返回缓存数据并标记
- [x] 10.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 10.8: 运行全部测试并确保通过（93 passed, 0 failed）

### Task 11: PAY-04 支付回调验签

- [x] 11.1: 在 WeChatPayChannel 中实现 VerifySignature 方法（微信支付 V3 签名验证：RSA-SHA256 + 时间戳容差 + 随机数防重放）
- [x] 11.2: 在 AlipayChannel 中实现 VerifySignature 方法（支付宝 RSA 签名验证）
- [x] 11.3: 在 NotifyController 中先验签再处理业务逻辑
- [x] 11.4: 验签失败返回 401，不处理业务
- [x] 11.5: 验签通过后发布 PaymentSucceededIntegrationEvent 或 PaymentFailedIntegrationEvent
- [x] 11.6: 回调接口幂等（以渠道交易号去重，Redis SET NX 原子操作，30 天 TTL）
- [x] 11.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 11.8: 编写应用层单元测试
- [x] 11.9: 运行全部测试并确保通过（86/86 passed）

### Task 12: PROMO-06 优惠券过期处理

- [x] 12.1: 创建后台服务 CouponExpiryService（BackgroundService）
- [x] 12.2: 定时扫描已领取未使用的优惠券（Status = Claimed 且 ExpireAt < now）
- [x] 12.3: 批量调用 Coupon.Expire 标记过期
- [x] 12.4: 批处理每批 500 条，避免大事务
- [x] 12.5: 过期券不可再使用（Use 方法校验 ExpireAt）
- [x] 12.6: 扫描频率：每小时一次
- [x] 12.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 12.8: 运行全部测试并确保通过

### Task 13: PM-05 成长值与会员等级

- [x] 13.1: 实现 MemberLevel 聚合（V0-V4 等级，成长值阈值）
- [x] 13.2: 成长值计算规则：消费积分发放时同步增加成长值
- [x] 13.3: 等级评定规则：近 12 个月成长值累计达标
- [x] 13.4: 创建定时任务 MemberLevelEvaluationJob 每日评估会员等级
- [x] 13.5: 等级变更时发布 MemberLevelChangedEvent
- [x] 13.6: 等级变更记录历史
- [x] 13.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 13.8: 运行全部测试并确保通过

### Task 14: PM-07 积分过期处理

- [x] 14.1: 创建后台服务 PointsExpiryService（BackgroundService）
- [x] 14.2: 定时扫描积分流水中的过期积分（按先进先出原则）
- [x] 14.3: 调用 PointsAccount.ExpirePoints(points) 标记过期
- [x] 14.4: 发布 PointsExpiredEvent（userId、points、expiredAt）
- [x] 14.5: 批处理每批 500 条，避免大事务
- [x] 14.6: 扫描频率：每日一次
- [x] 14.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 14.8: 运行全部测试并确保通过

### Task 15: NTF-06 发送失败重试与死信处理

- [x] 15.1: 在领域层实现 IRetryPolicy 领域服务（ShouldRetry、NextDelay）
- [x] 15.2: 错误分级：可重试（SMTP 421/450、限流、超时）→ 退避重试；不可重试（邮箱不存在 550）→ 直接死信
- [x] 15.3: 重试时间间隔：指数退避 30s / 2min / 10min
- [x] 15.4: 创建后台 worker NotificationRetryJob 周期扫描 NextRetryAt 到期的 Retried 记录
- [x] 15.5: 重试达 MaxRetry（3 次）仍失败 → MoveToDeadLetter 进入死信终态
- [x] 15.6: 实现死信管理 API（列表/批量重发/丢弃）
- [x] 15.7: 丢弃原因必填，批量操作记录审计日志
- [x] 15.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 15.9: 运行全部测试并确保通过

### Task 16: NTF-07 模板渲染服务

- [x] 16.1: 在领域层实现 ITemplateRenderService.RenderAsync(template, variables)
- [x] 16.2: 必填变量缺失 → 抛领域异常，拒绝发送
- [x] 16.3: 可选变量缺失 → 渲染成功
- [x] 16.4: 正文含未定义占位符 → 保存模板时返回 400
- [x] 16.5: 变量值含 HTML 特殊字符 → 转义防注入
- [x] 16.6: 渲染结果固化到 NotificationRecord.ContentSnapshot
- [x] 16.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 16.8: 运行全部测试并确保通过

### Task 17: NTF-08 渠道参数配置管理

- [x] 17.1: 实现 GET/PUT /api/admin/notification-config 端点
- [x] 17.2: 实现 POST /api/admin/notification-config/test 测试发送验证
- [x] 17.3: 敏感参数加密存储，展示脱敏为 **\*\***
- [x] 17.4: 配置变更热更新适配器实例重建
- [x] 17.5: 在途发送沿用旧适配器实例，新发送使用新实例
- [x] 17.6: 配置变更记录审计日志
- [x] 17.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 17.8: 运行全部测试并确保通过

### Task 18: NTF-09 通知频率限制与防骚扰

- [x] 18.1: 在领域层定义 IRateLimiter.AcquireAsync 接口
- [x] 18.2: 在基础设施层实现 RedisRateLimiter（基于 Redis 滑动窗口）
- [x] 18.3: 限流规则：邮件 10 条/小时/收件人、短信 5 条/小时/收件人、20 条/天/收件人
- [x] 18.4: 验证码类通知可配置豁免或单独限流
- [x] 18.5: 发送前调用频率校验，超限拒绝并记录 errorCode=RATE_LIMITED
- [x] 18.6: Redis 不可用时降级为放行并告警
- [x] 18.7: 实现 GET/PUT /api/admin/notification-rate-limits 端点
- [x] 18.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 18.9: 运行全部测试并确保通过（259 passed, 0 failed）

### Task 19: SYS-07 系统健康监控

- [x] 19.1: 在领域层定义 IModuleHealthProbe.ProbeAsync 接口
- [x] 19.2: 在基础设施层实现 HttpModuleHealthProbe
- [x] 19.3: 实现 IHealthAggregator.AggregateAsync() 领域服务
- [x] 19.4: 创建 ModuleHealth 值对象（Module、Status、Dependencies、CheckedAt）
- [x] 19.5: 整体状态取各模块最差状态
- [x] 19.6: 实现 GET /api/admin/health 和 GET /api/admin/health/modules
- [x] 19.7: 健康端点拉取超时 3s 归为 Unhealthy 并告警
- [x] 19.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 19.9: 运行全部测试并确保通过

### Task 20: SYS-09 基础设施抽象实现

- [x] 20.1: 实现 RabbitMqDeadLetterManager（对接 RabbitMQ Management HTTP API）
- [x] 20.2: 实现 ElasticsearchRebuildTrigger（调用各域 ES reindex API）
- [x] 20.3: 实现 HttpModuleHealthProbe（HTTP GET 各模块 /health 端点）
- [x] 20.4: 实现 RedisRateLimitCounter（基于 Redis Lua 脚本原子计数）
- [x] 20.5: 编写各基础设施抽象集成测试
- [x] 20.6: 运行全部测试并确保通过

---

## 第二批：跨模块事件驱动（13 个任务）

### Task 21: PRD-04 店铺暂停/恢复联动

- [x] 21.1: 在基础设施层创建 ShopEventConsumer 消费者
- [x] 21.2: 消费 ShopSuspendedEvent：按 sellerId 查询所有已上架商品，调用 SuspendByShop
- [x] 21.3: 消费 ShopResumedEvent：按 sellerId 查询所有店铺暂停态商品，调用 ResumeByShop
- [x] 21.4: 消费 ShopClosedEvent：按 sellerId 查询所有商品，调用 TakeDown
- [x] 21.5: 幂等消费以 EventId 去重
- [x] 21.6: 批量操作使用分页处理避免大事务
- [x] 21.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 21.8: 运行全部测试并确保通过（9/9 passed）

### Task 22: CART-03 登录时匿名购物车合并

- [x] 22.1: 在 ICartAppService 中实现 MergeAnonymousCartAsync(userId, anonymousId) 方法
- [x] 22.2: 合并逻辑：遍历匿名购物车项，逐项调用 AddItem（同 SKU 自动合并数量）
- [x] 22.3: 合并后单 SKU 总量不超 99、种类不超 50
- [x] 22.4: 选中状态按"任一来源选中即选中"合并
- [x] 22.5: 合并完成后删除匿名购物车 Redis 键
- [x] 22.6: 发布 CartMergedEvent（UserId、AnonymousId、MergedItemCount）
- [x] 22.7: 实现 POST /api/cart/merge 端点
- [x] 22.8: 合并幂等：同一匿名标识重复触发合并无操作
- [x] 22.9: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 22.10: 运行全部测试并确保通过（47/47 passed）

### Task 23: CART-04 商品事件消费

- [x] 23.1: 在基础设施层创建 ProductEventConsumer 消费者
- [x] 23.2: 消费 ProductTakenDownEvent：按 skuIds 调用 Cart.MarkInvalid(skuId, reason)，自动取消选中
- [x] 23.3: 消费 ProductPublishedEvent：按 skuIds 调用 Cart.MarkValid(skuId)，恢复有效
- [x] 23.4: 消费 ProductUpdatedEvent：刷新购物车项展示快照
- [x] 23.5: 消费 OrderCreatedEvent：调用 Cart.ClearCheckedOutItems 清空已结算项
- [x] 23.6: 幂等消费以 EventId 去重
- [x] 23.7: 批量更新购物车时使用乐观锁防并发冲突
- [x] 23.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 23.9: 运行全部测试并确保通过（47/47 passed）

### Task 24: ORD-06 售后期结束事件

- [x] 24.1: 在 Order 聚合中实现 CloseAfterSalesWindow() 方法（含时间校验）
- [x] 24.2: 校验 Status == Completed 且当前时间 ≥ AfterSalesWindowEndsAt
- [x] 24.3: 附加 OrderAfterSalesWindowClosedEvent（携带 PaidAmount）
- [x] 24.4: 订单完成时投递延迟消息（MassTransit ScheduleSend，售后窗口 7 天后触发）
- [x] 24.5: 创建 AfterSalesWindowConsumer 消费延迟消息，调用 CloseAfterSalesWindow
- [x] 24.6: 积分域消费 OrderAfterSalesWindowClosedEvent 发放消费返积分（已存在）
- [x] 24.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 24.8: 运行全部测试并确保通过

### Task 25: ORD-07 会员订阅订单

- [x] 25.1: 在 Order 聚合中实现 CompleteMembershipOrder() 方法
- [x] 25.2: 校验 OrderType = MembershipSubscription 且 Status = Paid
- [x] 25.3: 直接流转至 Completed，AfterSalesWindowEndsAt = CompletedAt（无发货流程）
- [x] 25.4: 附加 OrderCompletedEvent 和 OrderAfterSalesWindowClosedEvent
- [x] 25.5: 会员订阅订单创建时不要求 SellerId（Guid? 可空）
- [x] 25.6: 消费 PaymentSucceededIntegrationEvent 时检测 OrderType 自动调用完成方法
- [x] 25.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 25.8: 运行全部测试并确保通过（114 passed, 0 failed）

### Task 26: PROMO-03 秒杀异步落单

- [x] 26.1: Redis 预占成功后发布 SeckillOrderCreatedEvent（经发件箱模式）
- [x] 26.2: 事件携带：UserId、SkuId、SeckillPrice、Quantity、ActivityId、OrderId
- [x] 26.3: 订单域消费该事件创建订单（以秒杀价固化）
- [x] 26.4: 消息通知域发送秒杀成功通知
- [x] 26.5: 落单失败时回滚 Redis 库存（HINCRBY +1）
- [x] 26.6: 实现落单失败补偿机制（定时任务扫描未落单的预占记录）
- [x] 26.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 26.8: 运行全部测试并确保通过（90/90 Domain + 11/11 Infra passed）

### Task 27: PROMO-04 优惠券核销与退还

- [x] 27.1: 在基础设施层创建 OrderEventConsumer 消费者
- [x] 27.2: 消费 OrderPaidEvent：调用 Coupon.Use 核销券
- [x] 27.3: 消费 OrderCancelledEvent：调用 Coupon.Return 退还券
- [x] 27.4: 消费 RefundCompletedEvent：退还已核销券（恢复可使用状态）
- [x] 27.5: 幂等消费以 EventId 去重
- [x] 27.6: 退还券有效期不变（不延长）
- [x] 27.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 27.8: 运行全部测试并确保通过（11/11 passed）

### Task 28: PROMO-05 积分兑换优惠券

- [x] 28.1: 在基础设施层创建 PointsExchangeConsumer 消费者
- [x] 28.2: 消费 PointsExchangeCouponRequestedEvent
- [x] 28.3: 校验积分兑换券模板存在且有效
- [x] 28.4: 创建优惠券实例（关联用户，设置有效期）
- [x] 28.5: 发布 CouponExchangeSucceededEvent
- [x] 28.6: 模板不存在或已停用时兑换失败
- [x] 28.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 28.8: 运行全部测试并确保通过（11/11 passed）

### Task 29: PM-03 评价返积分与新人积分

- [x] 29.1: 在基础设施层创建 ReviewEventConsumer 消费者
- [x] 29.2: 消费 ReviewApprovedEvent：发放评价积分（10 积分/条）
- [x] 29.3: 校验每日评价积分上限（Redis 计数，每日最多 5 条评价获积分）
- [x] 29.4: 在基础设施层创建 UserEventConsumer 消费者
- [x] 29.5: 消费 UserRegisteredEvent：发放新人积分（100 积分）
- [x] 29.6: 幂等消费以 EventId 去重
- [x] 29.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 29.8: 运行全部测试并确保通过

### Task 30: PM-04 积分冻结/释放/抵扣/扣回

- [x] 30.1: 在 PointsAccount 中实现 FreezePoints/ReleasePoints/ConsumePoints/RevertPoints
- [x] 30.2: 实现内部接口 POST internal/points/freeze（已存在）
- [x] 30.3: 消费 OrderPaidEvent：将冻结积分转为正式扣减（已存在）
- [x] 30.4: 消费 OrderCancelledEvent：释放冻结积分（已存在）
- [x] 30.5: 消费 RefundCompletedEvent：扣回已发放积分（1 元=1 积分，允许负余额）
- [x] 30.6: 积分余额可为负（后续获取优先抵扣）
- [x] 30.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 30.8: 运行全部测试并确保通过

### Task 31: PM-06 积分兑换优惠券

- [x] 31.1: 实现 POST /api/points/exchange-coupon 端点
- [x] 31.2: 校验积分余额充足（≥ 兑换所需积分）
- [x] 31.3: 发布 PointsExchangeCouponRequestedEvent
- [x] 31.4: 在基础设施层创建 CouponExchangeConsumer 消费者
- [x] 31.5: 消费 CouponExchangeSucceededEvent：正式扣减积分
- [x] 31.6: 消费 CouponExchangeFailedEvent：释放积分
- [x] 31.7: 兑换失败时释放积分
- [x] 31.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 31.9: 运行全部测试并确保通过（149 passed, 0 failed）

### Task 32: RAS-02 评价审核与回复

- [x] 32.1: 在 Review 聚合中实现 Approve/Hide/Reply 方法
- [x] 32.2: Approve 时发布 ReviewApprovedEvent（驱动积分发放）
- [x] 32.3: Hide 时发布 ReviewHiddenEvent（驱动商品评分更新）
- [x] 32.4: 实现 POST /api/admin/reviews/{id}/approve 端点（已存在）
- [x] 32.5: 实现 POST /api/admin/reviews/{id}/hide 端点（已存在）
- [x] 32.6: 实现 POST /api/seller/reviews/{id}/reply 端点（已存在）
- [x] 32.7: 回复内容长度限制（≤ 500 字）
- [x] 32.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 32.9: 运行全部测试并确保通过（46 passed, 0 failed）

### Task 33: RAS-03 售后状态机与审核

- [x] 33.1: 在 AfterSales 聚合中实现完整状态机（Submit→Cancel→Approve→Reject→ReturnGoods→ConfirmReturn→CompleteRefund）
- [x] 33.2: 状态流转校验（不可跳转/回退，仅允许合法转换）
- [x] 33.3: 实现卖家审核端点（approve/reject/confirm-return）
- [x] 33.4: 实现运营审核端点（approve/reject）
- [x] 33.5: 每个状态变更发布对应事件
- [x] 33.6: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 33.7: 运行全部测试并确保通过（67 passed, 0 failed）

---

## 第三批：店铺域 + 售后域（5 个任务）

### Task 34: SS-02 店铺资质管理

- [x] 34.1: 创建 ShopQualification 实体（QualificationType、Number、ImageUrl、ValidFrom、ValidTo、Status）
- [x] 34.2: 店铺入驻申请时强制提交资质证照
- [x] 34.3: 运营审核资质证照（通过/驳回，驳回需填写原因）
- [x] 34.4: 资质证照上传通过 IFileStorageService 存储
- [x] 34.5: 创建后台服务 QualificationExpiryReminder 定时检测资质有效期
- [x] 34.6: 资质到期前 30 天/7 天/1 天提醒卖家更新
- [x] 34.7: 资质过期后限制店铺部分功能
- [x] 34.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 34.9: 运行全部测试并确保通过（301 passed）

### Task 35: SS-03 店铺暂停/恢复/关闭

- [x] 35.1: 在 Shop 聚合中实现 Suspend/Resume/Close 方法（已存在）
- [x] 35.2: 暂停时发布 ShopSuspendedEvent（已存在）
- [x] 35.3: 恢复时发布 ShopResumedEvent（已存在）
- [x] 35.4: 关闭时发布 ShopClosedEvent（已存在）
- [x] 35.5: 事件经发件箱模式保证原子性（已存在）
- [x] 35.6: 实现 POST /api/admin/shops/{id}/suspend 端点（已存在）
- [x] 35.7: 实现 POST /api/admin/shops/{id}/resume 端点（已存在）
- [x] 35.8: 实现 POST /api/admin/shops/{id}/close 端点（已存在）
- [x] 35.9: 编写领域层单元测试（覆盖率 ≥ 80%）（已存在）
- [x] 35.10: 运行全部测试并确保通过（301 passed）

### Task 36: SS-04 店铺经营数据

- [x] 36.1: 在基础设施层创建 OrderEventConsumer 消费者
- [x] 36.2: 消费 OrderCreatedEvent/OrderPaidEvent/OrderCancelledEvent/OrderCompletedEvent 更新店铺数据
- [x] 36.3: 在基础设施层创建 ProductEventConsumer 消费者
- [x] 36.4: 消费 ProductPublishedEvent/ProductTakenDownEvent 更新商品数
- [x] 36.5: 实现 GET /api/seller/dashboard 经营概览
- [x] 36.6: 实现 GET /api/seller/sales-trend 销售趋势
- [x] 36.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 36.8: 运行全部测试并确保通过（301 Domain + 15 ShopDashboardData passed）

### Task 37: RAS-04 退款请求发起

- [x] 37.1: 售后审核通过后，发布 RefundRequestedIntegrationEvent
- [x] 37.2: 事件携带：PaymentId、RefundAmount、RefundReason、AfterSalesId
- [x] 37.3: 在基础设施层创建 RefundEventConsumer 消费者（已存在）
- [x] 37.4: 消费 RefundSucceededIntegrationEvent：流转售后单至退款完成（已存在）
- [x] 37.5: 消费 RefundFailedIntegrationEvent：记录失败原因（已存在）
- [x] 37.6: 发布 RefundCompletedEvent 驱动订单域回滚、积分扣回（已存在）
- [x] 37.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 37.8: 运行全部测试并确保通过（115 passed）

### Task 38: RAS-05 评价评分回写商品域

- [x] 38.1: 评价提交时发布 ReviewSubmittedEvent（携带 productId、newScore、reviewCount）（已存在）
- [x] 38.2: 评价隐藏时发布 ReviewHiddenEvent（携带 productId）（已存在）
- [x] 38.3: 在基础设施层创建 ReviewEventConsumer 消费事件
- [x] 38.4: 商品域消费 ReviewSubmittedEvent 更新 Product.Score 字段
- [x] 38.5: 商品域消费 ReviewHiddenEvent 重新计算评分
- [x] 38.6: 评分计算正确（加权平均，考虑隐藏评价）
- [x] 38.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 38.8: 运行全部测试并确保通过（Product.Domain 110 + Product.Infra 17 + ReviewAfterSales.Domain 115 = 242 passed）

---

## 第四批：系统管理域（3 个任务）

### Task 39: SYS-05 跨域审计日志聚合

- [x] 39.1: 创建 AuditLogEntry 只读聚合根
- [x] 39.2: 数据来源于消费各域审计事件或查询各域审计接口的投影
- [x] 39.3: 在基础设施层创建 AuditLogConsumer 消费各域审计事件（17 种跨域事件）
- [x] 39.4: 请求摘要脱敏存储（敏感参数掩码）
- [x] 39.5: 实现 GET /api/admin/audit-logs 聚合查询
- [x] 39.6: 实现 GET /api/admin/audit-logs/{id} 详情
- [x] 39.7: 保留期 180 天，超期归档冷存储
- [x] 39.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 39.9: 运行全部测试并确保通过（62 passed）

### Task 40: SYS-06 接口限流配置

- [x] 40.1: 创建 RateLimitRule 聚合根（TargetApi、TargetContext、Limit、WindowSeconds、Algorithm、Scope、Enabled）
- [x] 40.2: 创建值对象：LimitAlgorithm（SlidingWindow/TokenBucket/FixedWindow）、LimitScope（Ip/User/Global/Shop）
- [x] 40.3: 实现 IRateLimitPolicyResolver.ResolveAsync 领域服务
- [x] 40.4: 规则变更后发布 RateLimitRuleUpdatedEvent，各域网关订阅热加载
- [x] 40.5: 实现 6 个限流规则 API 端点（列表/新增/详情/更新/启用/禁用）
- [x] 40.6: 并发编辑乐观锁冲突返回 409
- [x] 40.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 40.8: 运行全部测试并确保通过（62 passed）

### Task 41: SYS-08 统计数据源一致性保障

- [x] 41.1: 统计看板与各域统计使用相同的事件源
- [x] 41.2: 本域只读消费各域集成事件做跨域聚合
- [x] 41.3: 统计投影读模型以事件源为准
- [x] 41.4: 实现定期对账校验任务（每日凌晨执行）
- [x] 41.5: 对账差异记录告警并触发修正
- [x] 41.6: 本域不回写各域写库
- [x] 41.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 41.8: 运行全部测试并确保通过（361 passed）

---

## 第五批：进度更新与总结

### Task 42: 进度更新与报告生成

- [x] 42.1: 更新 progress.md 中所有已完成任务状态
- [x] 42.2: 更新各模块完成率统计
- [x] 42.3: 生成阶段性成果报告
- [ ] 42.4: 提交代码（每个任务独立 commit）

---

# Task Dependencies

- **第一批（Task 1-20）**: 所有任务无跨模块依赖，可全部并行执行
- **第二批（Task 21-33）**: 部分依赖第一批中的事件发布方，建议第一批完成后执行
- **第三批（Task 34-38）**: 依赖第二批中 SS-03 和 RAS-02/03 完成
- **第四批（Task 39-41）**: 依赖前三批的事件体系就绪
- **第五批（Task 42）**: 依赖所有任务完成

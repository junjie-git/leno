# P1 任务完成 - 质量检查清单

## 第一批：独立无跨模块依赖

### SK-02: 对象存储 MinIO 适配器
- [ ] SK-02.1: MinIO NuGet 包正确添加
- [ ] SK-02.2: FileStorageOptions 配置类完整（Provider、Endpoint、AccessKey、SecretKey、BucketName）
- [ ] SK-02.3: ObjectStorageService 实现 IFileStorageService 全部方法
- [ ] SK-02.4: UploadAsync/DownloadAsync/DeleteAsync/ValidateUrl/ExistsAsync 功能正确
- [ ] SK-02.5: AddFileStorage 扩展方法支持 Local/MinIO 切换
- [ ] SK-02.6: 敏感参数从环境变量/配置中心读取
- [ ] SK-02.7: 保留 LocalFileStorageService 作为开发环境默认实现
- [ ] SK-02.8: 领域层测试覆盖率 ≥ 80%
- [ ] SK-02.9: 所有测试通过（100% 通过率）

### UA-03: 双因子认证 TOTP
- [ ] UA-03.1: Otp.NET NuGet 包正确添加
- [ ] UA-03.2: TotpTokenVerifier 正确实现 ITokenVerifier
- [ ] UA-03.3: EnableTwoFactor/ConfirmTwoFactor/DisableTwoFactor 流程完整
- [ ] UA-03.4: 未验证的双因子配置不生效
- [ ] UA-03.5: 二维码 URI 正确生成
- [ ] UA-03.6: 登录时检测双因子启用状态
- [ ] UA-03.7: POST /api/auth/two-factor/verify 二次验证正确
- [ ] UA-03.8: 领域层测试覆盖率 ≥ 80%
- [ ] UA-03.9: 所有测试通过（100% 通过率）

### UA-04: 密码找回
- [ ] UA-04.1: ForgotPasswordRequestedEvent 领域事件定义正确
- [ ] UA-04.2: 重置令牌 10 分钟过期，一次性使用
- [ ] UA-04.3: 支持邮箱/手机号找回密码
- [ ] UA-04.4: 验证码经 INotificationService 发送
- [ ] UA-04.5: 密码重置后发布 PasswordChangedEvent
- [ ] UA-04.6: 重置令牌使用后立即删除
- [ ] UA-04.7: 领域层测试覆盖率 ≥ 80%
- [ ] UA-04.8: 所有测试通过（100% 通过率）

### UA-05: RBAC 权限策略管理
- [ ] UA-05.1: Role 实体定义正确（RoleId、Name、Description、Permissions、IsBuiltIn）
- [ ] UA-05.2: Permission 值对象 ResourceKey 格式正确
- [ ] UA-05.3: IPermissionRepository + EfCorePermissionRepository 实现正确
- [ ] UA-05.4: 角色 CRUD 完整
- [ ] UA-05.5: 内置角色（Buyer/Seller/Operator/Admin）不可删除
- [ ] UA-05.6: 权限资源绑定格式正确
- [ ] UA-05.7: 领域层测试覆盖率 ≥ 80%
- [ ] UA-05.8: 所有测试通过（100% 通过率）

### PRD-02: 商品审核历史记录
- [ ] PRD-02.1: AuditInfo 值对象定义正确
- [ ] PRD-02.2: _auditHistory 列表维护正确
- [ ] PRD-02.3: Approve/Reject 方法追加审核历史
- [ ] PRD-02.4: 审核历史包含操作人、时间、结果、原因
- [ ] PRD-02.5: 审核历史不可修改
- [ ] PRD-02.6: EF Core 值转换存储正确（JSON 列）
- [ ] PRD-02.7: 领域层测试覆盖率 ≥ 80%
- [ ] PRD-02.8: 所有测试通过（100% 通过率）

### PRD-03: 价格变更历史
- [ ] PRD-03.1: PriceChangeRecord 值对象定义正确
- [ ] PRD-03.2: _priceChangeHistory 列表维护正确
- [ ] PRD-03.3: AdjustPrice 方法追加历史记录
- [ ] PRD-03.4: 变更历史包含新旧价格、时间、操作人
- [ ] PRD-03.5: 可按 SKU 查询价格变更历史
- [ ] PRD-03.6: GET /api/products/{id}/price-history 端点正常工作
- [ ] PRD-03.7: 领域层测试覆盖率 ≥ 80%
- [ ] PRD-03.8: 所有测试通过（100% 通过率）

### PRD-05: 库存补货与盘点
- [ ] PRD-05.1: UpdateStock(skuId, delta) 方法校验结果 ≥ 0
- [ ] PRD-05.2: POST /api/products/{id}/skus/{skuId}/stock 端点正常工作
- [ ] PRD-05.3: StockAdjustedEvent 正确发布
- [ ] PRD-05.4: StockAdjustedEventConsumer 同步 ES 读模型
- [ ] PRD-05.5: 库存变更记录操作日志
- [ ] PRD-05.6: 领域层测试覆盖率 ≥ 80%
- [ ] PRD-05.7: 所有测试通过（100% 通过率）

### ORD-04: 积分抵现
- [ ] ORD-04.1: ApplyPointsOffset 方法正确实现
- [ ] ORD-04.2: PointsOffsetAmount ≤ ItemsAmount - DiscountAmount
- [ ] ORD-04.3: 单笔订单积分抵扣上限校验
- [ ] ORD-04.4: TotalAmount 正确计算
- [ ] ORD-04.5: 下单前调用积分域冻结接口
- [ ] ORD-04.6: 支付成功时 OrderPaidEvent 驱动正式扣减
- [ ] ORD-04.7: 订单取消时 OrderCancelledEvent 驱动释放冻结
- [ ] ORD-04.8: 领域层测试覆盖率 ≥ 80%
- [ ] ORD-04.9: 所有测试通过（100% 通过率）

### ORD-05: 优惠分摊
- [ ] ORD-05.1: ApplyDiscount 方法正确实现
- [ ] ORD-05.2: 各行分摊之和等于优惠总额
- [ ] ORD-05.3: 各行分摊不超过行小计
- [ ] ORD-05.4: DiscountAllocation 字段正确更新
- [ ] ORD-05.5: DiscountAmount 与 TotalAmount 正确计算
- [ ] ORD-05.6: 下单时调用促销域计算结果
- [ ] ORD-05.7: 领域层测试覆盖率 ≥ 80%
- [ ] ORD-05.8: 所有测试通过（100% 通过率）

### ORD-08: 物流轨迹查询
- [ ] ORD-08.1: ILogisticsTrackingService 接口定义正确
- [ ] ORD-08.2: 物流公司 API 适配器实现正确
- [ ] ORD-08.3: GET /api/orders/{id}/logistics-trace 端点正常工作
- [ ] ORD-08.4: 仅支持轨迹查询的物流公司可查
- [ ] ORD-08.5: 物流轨迹缓存到 Redis（TTL 1 小时）
- [ ] ORD-08.6: 轨迹查询失败时返回缓存数据
- [ ] ORD-08.7: 领域层测试覆盖率 ≥ 80%
- [ ] ORD-08.8: 所有测试通过（100% 通过率）

### PAY-04: 支付回调验签
- [ ] PAY-04.1: WeChatPayChannel VerifySignature 微信 V3 签名验证正确
- [ ] PAY-04.2: AlipayChannel VerifySignature 支付宝 RSA 签名验证正确
- [ ] PAY-04.3: NotifyController 先验签再处理业务
- [ ] PAY-04.4: 验签失败返回 401
- [ ] PAY-04.5: 验签通过后正确发布事件
- [ ] PAY-04.6: 回调接口幂等（渠道交易号去重）
- [ ] PAY-04.7: 领域层测试覆盖率 ≥ 80%
- [ ] PAY-04.8: 所有测试通过（100% 通过率）

### PROMO-06: 优惠券过期处理
- [ ] PROMO-06.1: CouponExpiryService BackgroundService 正确实现
- [ ] PROMO-06.2: 定时扫描已领取未使用的优惠券
- [ ] PROMO-06.3: 批量调用 Coupon.Expire 标记过期
- [ ] PROMO-06.4: 批处理每批 500 条
- [ ] PROMO-06.5: 过期券不可再使用
- [ ] PROMO-06.6: 扫描频率：每小时一次
- [ ] PROMO-06.7: 领域层测试覆盖率 ≥ 80%
- [ ] PROMO-06.8: 所有测试通过（100% 通过率）

### PM-05: 成长值与会员等级
- [ ] PM-05.1: MemberLevel 聚合（V0-V4 等级，成长值阈值）正确
- [ ] PM-05.2: 消费积分发放时同步增加成长值
- [ ] PM-05.3: 近 12 个月成长值累计达标评定
- [ ] PM-05.4: MemberLevelEvaluationJob 每日自动评估
- [ ] PM-05.5: MemberLevelChangedEvent 正确发布
- [ ] PM-05.6: 等级变更记录历史
- [ ] PM-05.7: 领域层测试覆盖率 ≥ 80%
- [ ] PM-05.8: 所有测试通过（100% 通过率）

### PM-07: 积分过期处理
- [ ] PM-07.1: PointsExpiryService BackgroundService 正确实现
- [ ] PM-07.2: 按先进先出原则标记过期积分
- [ ] PM-07.3: PointsAccount.ExpirePoints 正确调用
- [ ] PM-07.4: PointsExpiredEvent 正确发布
- [ ] PM-07.5: 批处理每批 500 条
- [ ] PM-07.6: 扫描频率：每日一次
- [ ] PM-07.7: 领域层测试覆盖率 ≥ 80%
- [ ] PM-07.8: 所有测试通过（100% 通过率）

### NTF-06: 发送失败重试与死信处理
- [ ] NTF-06.1: IRetryPolicy 领域服务正确实现
- [ ] NTF-06.2: 可重试错误指数退避重试（30s/2min/10min）
- [ ] NTF-06.3: 不可重试错误直接死信不重试
- [ ] NTF-06.4: NotificationRetryJob 周期扫描正确
- [ ] NTF-06.5: 重试 3 次仍失败进入死信
- [ ] NTF-06.6: 死信管理 API 完整（列表/批量重发/丢弃）
- [ ] NTF-06.7: 丢弃原因必填，批量操作记录审计日志
- [ ] NTF-06.8: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-06.9: 所有测试通过（100% 通过率）

### NTF-07: 模板渲染服务
- [ ] NTF-07.1: ITemplateRenderService.RenderAsync 正确实现
- [ ] NTF-07.2: 必填变量缺失返回 400 拒绝发送
- [ ] NTF-07.3: 可选变量缺失渲染成功
- [ ] NTF-07.4: 正文含未定义占位符保存时返回 400
- [ ] NTF-07.5: HTML 特殊字符转义防注入
- [ ] NTF-07.6: 渲染结果固化到 ContentSnapshot
- [ ] NTF-07.7: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-07.8: 所有测试通过（100% 通过率）

### NTF-08: 渠道参数配置管理
- [ ] NTF-08.1: GET/PUT /api/admin/notification-config 端点正常
- [ ] NTF-08.2: POST /api/admin/notification-config/test 测试发送正常
- [ ] NTF-08.3: 敏感参数加密存储，脱敏展示为 ******
- [ ] NTF-08.4: 配置变更热更新适配器实例重建
- [ ] NTF-08.5: 在途发送沿用旧实例，新发送使用新实例
- [ ] NTF-08.6: 配置变更记录审计日志
- [ ] NTF-08.7: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-08.8: 所有测试通过（100% 通过率）

### NTF-09: 通知频率限制与防骚扰
- [ ] NTF-09.1: IRateLimiter.AcquireAsync 接口定义正确
- [ ] NTF-09.2: RedisRateLimiter 基于 Redis 滑动窗口实现
- [ ] NTF-09.3: 短信 5 条/小时/收件人、20 条/天/收件人
- [ ] NTF-09.4: 验证码类通知可单独限流
- [ ] NTF-09.5: 超限拒绝并记录 errorCode=RATE_LIMITED
- [ ] NTF-09.6: Redis 不可用时降级放行并告警
- [ ] NTF-09.7: GET/PUT /api/admin/notification-rate-limits 端点正常
- [ ] NTF-09.8: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-09.9: 所有测试通过（100% 通过率）

### SYS-07: 系统健康监控
- [ ] SYS-07.1: IModuleHealthProbe.ProbeAsync 接口定义正确
- [ ] SYS-07.2: HttpModuleHealthProbe 实现正确
- [ ] SYS-07.3: IHealthAggregator.AggregateAsync() 正确实现
- [ ] SYS-07.4: ModuleHealth 值对象定义正确
- [ ] SYS-07.5: 整体状态取各模块最差状态
- [ ] SYS-07.6: GET /api/admin/health 和 /api/admin/health/modules 正常
- [ ] SYS-07.7: 健康端点超时 3s 归为 Unhealthy 并告警
- [ ] SYS-07.8: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-07.9: 所有测试通过（100% 通过率）

### SYS-09: 基础设施抽象实现
- [ ] SYS-09.1: RabbitMqDeadLetterManager 对接 RabbitMQ 死信队列
- [ ] SYS-09.2: ElasticsearchRebuildTrigger 对接 ES reindex API
- [ ] SYS-09.3: HttpModuleHealthProbe 聚合各模块 /health 端点
- [ ] SYS-09.4: RedisRateLimitCounter 基于 Lua 脚本原子限流计数
- [ ] SYS-09.5: 各基础设施抽象集成测试通过
- [ ] SYS-09.6: 所有测试通过（100% 通过率）

---

## 第二批：跨模块事件驱动

### PRD-04: 店铺暂停/恢复联动
- [ ] PRD-04.1: ShopEventConsumer 正确消费店铺事件
- [ ] PRD-04.2: 店铺暂停时关联商品自动置为店铺暂停态
- [ ] PRD-04.3: 店铺恢复时商品恢复已上架态
- [ ] PRD-04.4: 店铺关闭时商品全部下架
- [ ] PRD-04.5: 事件消费幂等（EventId 去重）
- [ ] PRD-04.6: 批量操作分页处理避免大事务
- [ ] PRD-04.7: 领域层测试覆盖率 ≥ 80%
- [ ] PRD-04.8: 所有测试通过（100% 通过率）

### CART-03: 登录时匿名购物车合并
- [ ] CART-03.1: MergeAnonymousCartAsync 合并逻辑正确
- [ ] CART-03.2: 同 SKU 合并数量（不超 99）
- [ ] CART-03.3: 选中状态按"任一来源选中即选中"合并
- [ ] CART-03.4: 合并后清空匿名购物车
- [ ] CART-03.5: CartMergedEvent 正确发布
- [ ] CART-03.6: POST /api/cart/merge 端点正常
- [ ] CART-03.7: 合并幂等
- [ ] CART-03.8: 领域层测试覆盖率 ≥ 80%
- [ ] CART-03.9: 所有测试通过（100% 通过率）

### CART-04: 商品事件消费
- [ ] CART-04.1: ProductEventConsumer 正确消费商品事件
- [ ] CART-04.2: 商品下架时购物车项自动标记失效
- [ ] CART-04.3: 商品重新上架时购物车项恢复有效
- [ ] CART-04.4: 商品信息变更时刷新展示快照
- [ ] CART-04.5: 下单后清空已结算项
- [ ] CART-04.6: 事件消费幂等
- [ ] CART-04.7: 批量更新使用乐观锁
- [ ] CART-04.8: 领域层测试覆盖率 ≥ 80%
- [ ] CART-04.9: 所有测试通过（100% 通过率）

### ORD-06: 售后期结束事件
- [ ] ORD-06.1: CloseAfterSalesWindow() 方法正确实现
- [ ] ORD-06.2: 仅已完成订单可关闭售后期
- [ ] ORD-06.3: OrderAfterSalesWindowClosedEvent 携带 PaidAmount
- [ ] ORD-06.4: 订单完成时投递延迟消息（默认 7 天）
- [ ] ORD-06.5: AfterSalesWindowConsumer 正确消费
- [ ] ORD-06.6: 积分域消费事件发放消费返积分
- [ ] ORD-06.7: 领域层测试覆盖率 ≥ 80%
- [ ] ORD-06.8: 所有测试通过（100% 通过率）

### ORD-07: 会员订阅订单
- [ ] ORD-07.1: CompleteMembershipOrder() 方法正确实现
- [ ] ORD-07.2: 仅 OrderType = MembershipSubscription 且 Status = Paid
- [ ] ORD-07.3: 直接流转至 Completed，无发货流程
- [ ] ORD-07.4: OrderCompletedEvent 和 OrderAfterSalesWindowClosedEvent 正确发布
- [ ] ORD-07.5: 会员订阅订单 SellerId 可空
- [ ] ORD-07.6: 消费 PaymentSucceededIntegrationEvent 自动完成
- [ ] ORD-07.7: 领域层测试覆盖率 ≥ 80%
- [ ] ORD-07.8: 所有测试通过（100% 通过率）

### PROMO-03: 秒杀异步落单
- [ ] PROMO-03.1: SeckillOrderCreatedEvent 经发件箱模式发布
- [ ] PROMO-03.2: 事件携带完整字段
- [ ] PROMO-03.3: 订单域消费事件创建订单（秒杀价固化）
- [ ] PROMO-03.4: 消息通知域发送秒杀成功通知
- [ ] PROMO-03.5: 落单失败时回滚 Redis 库存
- [ ] PROMO-03.6: 落单失败补偿机制（定时任务扫描）
- [ ] PROMO-03.7: 领域层测试覆盖率 ≥ 80%
- [ ] PROMO-03.8: 所有测试通过（100% 通过率）

### PROMO-04: 优惠券核销与退还
- [ ] PROMO-04.1: 支付成功时核销已使用优惠券
- [ ] PROMO-04.2: 订单取消时退还优惠券
- [ ] PROMO-04.3: 退款完成时退还优惠券
- [ ] PROMO-04.4: 事件消费幂等
- [ ] PROMO-04.5: 退还券有效期不变
- [ ] PROMO-04.6: 领域层测试覆盖率 ≥ 80%
- [ ] PROMO-04.7: 所有测试通过（100% 通过率）

### PROMO-05: 积分兑换优惠券
- [ ] PROMO-05.1: PointsExchangeConsumer 正确消费事件
- [ ] PROMO-05.2: 校验积分兑换券模板存在且有效
- [ ] PROMO-05.3: 创建优惠券实例正确
- [ ] PROMO-05.4: CouponExchangeSucceededEvent 正确发布
- [ ] PROMO-05.5: 模板不存在或已停用时兑换失败
- [ ] PROMO-05.6: 领域层测试覆盖率 ≥ 80%
- [ ] PROMO-05.7: 所有测试通过（100% 通过率）

### PM-03: 评价返积分与新人积分
- [ ] PM-03.1: ReviewEventConsumer 正确消费 ReviewApprovedEvent
- [ ] PM-03.2: 评价积分 10 积分/条，每日上限 5 条
- [ ] PM-03.3: UserEventConsumer 正确消费 UserRegisteredEvent
- [ ] PM-03.4: 新人积分 100 积分
- [ ] PM-03.5: 事件消费幂等
- [ ] PM-03.6: 领域层测试覆盖率 ≥ 80%
- [ ] PM-03.7: 所有测试通过（100% 通过率）

### PM-04: 积分冻结/释放/抵扣/扣回
- [ ] PM-04.1: FreezePoints/ReleasePoints/ConsumePoints/RevertPoints 正确实现
- [ ] PM-04.2: POST internal/points/freeze 端点正常
- [ ] PM-04.3: 支付成功时冻结积分转为正式扣减
- [ ] PM-04.4: 订单取消时释放冻结积分
- [ ] PM-04.5: 退款时扣回已发放积分
- [ ] PM-04.6: 积分余额可为负
- [ ] PM-04.7: 领域层测试覆盖率 ≥ 80%
- [ ] PM-04.8: 所有测试通过（100% 通过率）

### PM-06: 积分兑换优惠券
- [ ] PM-06.1: POST /api/points/exchange-coupon 端点正常
- [ ] PM-06.2: 积分余额不足时拒绝兑换
- [ ] PM-06.3: PointsExchangeCouponRequestedEvent 正确发布
- [ ] PM-06.4: 兑换成功后正式扣减积分
- [ ] PM-06.5: 兑换失败时释放积分
- [ ] PM-06.6: 超时 30s 未收到成功事件自动释放
- [ ] PM-06.7: 领域层测试覆盖率 ≥ 80%
- [ ] PM-06.8: 所有测试通过（100% 通过率）

### RAS-02: 评价审核与回复
- [ ] RAS-02.1: Approve/Hide/Reply 方法正确实现
- [ ] RAS-02.2: Approve 时发布 ReviewApprovedEvent
- [ ] RAS-02.3: Hide 时发布 ReviewHiddenEvent
- [ ] RAS-02.4: 运营可审核通过/隐藏评价
- [ ] RAS-02.5: 卖家可回复评价
- [ ] RAS-02.6: 回复内容长度限制 ≤ 500 字
- [ ] RAS-02.7: 领域层测试覆盖率 ≥ 80%
- [ ] RAS-02.8: 所有测试通过（100% 通过率）

### RAS-03: 售后状态机与审核
- [ ] RAS-03.1: AfterSales 状态机完整流转（7 态）
- [ ] RAS-03.2: 状态流转校验（不可跳转/回退）
- [ ] RAS-03.3: 卖家审核端点正常
- [ ] RAS-03.4: 运营审核端点正常
- [ ] RAS-03.5: 每个状态变更发布对应事件
- [ ] RAS-03.6: 领域层测试覆盖率 ≥ 80%
- [ ] RAS-03.7: 所有测试通过（100% 通过率）

---

## 第三批：店铺域 + 售后域

### SS-02: 店铺资质管理
- [ ] SS-02.1: ShopQualification 实体定义正确
- [ ] SS-02.2: 入驻申请时强制提交资质证照
- [ ] SS-02.3: 运营审核资质证照（通过/驳回）
- [ ] SS-02.4: 资质证照通过 IFileStorageService 存储
- [ ] SS-02.5: QualificationExpiryReminder 定时检测有效期
- [ ] SS-02.6: 资质到期前 30/7/1 天提醒
- [ ] SS-02.7: 资质过期后限制店铺功能
- [ ] SS-02.8: 领域层测试覆盖率 ≥ 80%
- [ ] SS-02.9: 所有测试通过（100% 通过率）

### SS-03: 店铺暂停/恢复/关闭
- [ ] SS-03.1: Suspend/Resume/Close 方法正确实现
- [ ] SS-03.2: ShopSuspendedEvent 正确发布
- [ ] SS-03.3: ShopResumedEvent 正确发布
- [ ] SS-03.4: ShopClosedEvent 正确发布
- [ ] SS-03.5: 事件经发件箱模式保证原子性
- [ ] SS-03.6: 管理端点正常（suspend/resume/close）
- [ ] SS-03.7: 领域层测试覆盖率 ≥ 80%
- [ ] SS-03.8: 所有测试通过（100% 通过率）

### SS-04: 店铺经营数据
- [ ] SS-04.1: 订单事件驱动店铺数据更新
- [ ] SS-04.2: 商品事件驱动店铺商品数更新
- [ ] SS-04.3: GET /api/seller/dashboard 经营概览正确
- [ ] SS-04.4: GET /api/seller/sales-trend 销售趋势正确
- [ ] SS-04.5: 领域层测试覆盖率 ≥ 80%
- [ ] SS-04.6: 所有测试通过（100% 通过率）

### RAS-04: 退款请求发起
- [ ] RAS-04.1: RefundRequestedIntegrationEvent 正确发布
- [ ] RAS-04.2: 事件携带完整字段
- [ ] RAS-04.3: RefundEventConsumer 正确消费
- [ ] RAS-04.4: 退款成功流转售后单状态
- [ ] RAS-04.5: RefundCompletedEvent 正确发布
- [ ] RAS-04.6: 领域层测试覆盖率 ≥ 80%
- [ ] RAS-04.7: 所有测试通过（100% 通过率）

### RAS-05: 评价评分回写商品域
- [ ] RAS-05.1: ReviewSubmittedEvent 正确发布
- [ ] RAS-05.2: ReviewHiddenEvent 正确发布
- [ ] RAS-05.3: 商品域消费事件更新评分
- [ ] RAS-05.4: 评分计算正确（加权平均）
- [ ] RAS-05.5: 隐藏评价重新计算评分
- [ ] RAS-05.6: 领域层测试覆盖率 ≥ 80%
- [ ] RAS-05.7: 所有测试通过（100% 通过率）

---

## 第四批：系统管理域

### SYS-05: 跨域审计日志聚合
- [ ] SYS-05.1: AuditLogEntry 只读聚合根定义正确
- [ ] SYS-05.2: AuditLogConsumer 消费各域审计事件
- [ ] SYS-05.3: 请求摘要脱敏存储
- [ ] SYS-05.4: GET /api/admin/audit-logs 聚合查询正常
- [ ] SYS-05.5: GET /api/admin/audit-logs/{id} 详情正常
- [ ] SYS-05.6: 保留期 180 天
- [ ] SYS-05.7: 审计日志只读不可篡改
- [ ] SYS-05.8: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-05.9: 所有测试通过（100% 通过率）

### SYS-06: 接口限流配置
- [ ] SYS-06.1: RateLimitRule 聚合根定义正确
- [ ] SYS-06.2: LimitAlgorithm/LimitScope 值对象正确
- [ ] SYS-06.3: IRateLimitPolicyResolver 实现正确
- [ ] SYS-06.4: RateLimitRuleUpdatedEvent 发布后网关热加载
- [ ] SYS-06.5: 6 个限流规则 API 端点正常
- [ ] SYS-06.6: 并发编辑乐观锁冲突返回 409
- [ ] SYS-06.7: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-06.8: 所有测试通过（100% 通过率）

### SYS-08: 统计数据源一致性保障
- [ ] SYS-08.1: 看板与各域统计使用相同事件源
- [ ] SYS-08.2: 本域只读消费各域集成事件
- [ ] SYS-08.3: 统计投影以事件源为准
- [ ] SYS-08.4: 定期对账校验任务（每日凌晨）
- [ ] SYS-08.5: 对账差异告警并触发修正
- [ ] SYS-08.6: 本域不回写各域业务库
- [ ] SYS-08.7: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-08.8: 所有测试通过（100% 通过率）

---

## 第五批：进度更新

- [ ] 所有 41 个 P1 任务完成
- [ ] 整体完成率从 31.0% 提升至 78.2%
- [ ] progress.md 状态更新正确
- [ ] 每个任务独立 commit，commit 消息包含任务 ID
- [ ] 代码符合 DDD 分层架构（Domain→Application→Infrastructure→API）
- [ ] 所有测试通过（单元测试 100% 通过率）
- [ ] 领域层测试覆盖率 ≥ 80%
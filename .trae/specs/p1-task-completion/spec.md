# P1 任务完成 - Master Agent 执行规范

## Why
当前项目已完成 27/87 任务（31.0%），所有 P0 关键任务已全部完成。剩余 41 个 P1 重要任务和 19 个 P2 一般任务。P1 任务影响主要功能或用户体验，需按 Master Agent 架构设计，全流程自主编排完成所有 P1 任务。

## What Changes
按模块分组，分 5 批执行 41 个 P1 任务：

**第一批：独立无跨模块依赖（20 个任务）**
- SK-02: 对象存储适配器（MinIO 对接）
- UA-03: 双因子认证 TOTP / UA-04: 密码找回 / UA-05: RBAC 权限管理
- PRD-02: 商品审核历史 / PRD-03: 价格变更历史 / PRD-05: 库存补货盘点
- ORD-04: 积分抵现 / ORD-05: 优惠分摊 / ORD-08: 物流轨迹查询
- PAY-04: 支付回调验签
- PROMO-06: 优惠券过期处理
- PM-05: 成长值与会员等级 / PM-07: 积分过期处理
- NTF-06: 重试与死信 / NTF-07: 模板渲染 / NTF-08: 渠道配置管理 / NTF-09: 频率限制
- SYS-07: 系统健康监控 / SYS-09: 基础设施抽象实现

**第二批：跨模块事件驱动（13 个任务）**
- PRD-04: 店铺暂停/恢复联动（需 SS-03 事件）
- CART-03: 匿名购物车合并（需 CART-02 完成）
- CART-04: 商品事件消费（需 PRD 事件）
- ORD-06: 售后期结束事件 / ORD-07: 会员订阅订单
- PROMO-03: 秒杀异步落单 / PROMO-04: 优惠券核销退还 / PROMO-05: 积分兑换优惠券
- PM-03: 评价/新人积分 / PM-04: 积分冻结/释放/抵扣/扣回 / PM-06: 积分兑换优惠券
- RAS-02: 评价审核与回复 / RAS-03: 售后状态机与审核

**第三批：店铺域 + 售后域（5 个任务）**
- SS-02: 店铺资质管理 / SS-03: 店铺暂停/恢复/关闭 / SS-04: 店铺经营数据
- RAS-04: 退款请求发起 / RAS-05: 评价评分回写商品域

**第四批：系统管理域（3 个任务）**
- SYS-05: 跨域审计日志聚合 / SYS-06: 接口限流配置 / SYS-08: 统计数据源一致性保障

**第五批：进度更新与总结**
- 更新 progress.md / 生成报告 / 代码提交

## Impact
- Affected specs: 所有 12 个模块（shared-kernel, user-auth, product, cart, order, promotion, payment, points-membership, review-aftersales, seller-shop, notification, system-admin）
- Affected code: 所有限界上下文的 Domain/Application/Infrastructure/API 层
- 整体完成率目标: 31.0% → 78.2%（68/87）

## ADDED Requirements

### Requirement: 对象存储 MinIO 适配器
系统 SHALL 实现 MinIO 适配器，补全 IFileStorageService 的生产环境实现，支持通过配置切换 Local/MinIO 后端。

### Requirement: 双因子认证 TOTP
系统 SHALL 基于 OTP.NET 实现 TOTP 双因子认证，支持启用/确认/关闭流程，登录时检测双因子状态。

### Requirement: 密码找回
系统 SHALL 实现基于邮箱/手机号的密码找回流程，生成一次性重置令牌（10 分钟过期），经通知域发送验证码。

### Requirement: RBAC 权限策略管理
系统 SHALL 实现角色 CRUD 与权限资源绑定，内置角色（Buyer/Seller/Operator/Admin）不可删除。

### Requirement: 商品审核历史
系统 SHALL 在每次审核操作时记录 AuditInfo（操作人、时间、结果、原因）到审核历史列表，不可修改。

### Requirement: 价格变更历史
系统 SHALL 在每次价格调整时记录 PriceChangeRecord（新旧价格、时间、操作人），支持按 SKU 查询。

### Requirement: 库存补货与盘点
系统 SHALL 支持卖家调整指定 SKU 库存，校验结果 ≥ 0，发布 StockAdjustedEvent 同步 ES。

### Requirement: 积分抵现
系统 SHALL 在订单中支持积分抵扣金额，校验抵扣上限，下单前冻结积分，支付成功正式扣减。

### Requirement: 优惠分摊
系统 SHALL 支持优惠金额按订单行分摊，各行分摊之和等于优惠总额，各行分摊不超过行小计。

### Requirement: 物流轨迹查询
系统 SHALL 提供已发货订单的物流轨迹查询，支持缓存优化，仅支持轨迹查询的物流公司可查。

### Requirement: 支付回调验签
系统 SHALL 实现微信支付 V3 签名验证和支付宝 RSA 签名验证，验签失败返回 401，回调接口幂等。

### Requirement: 优惠券过期处理
系统 SHALL 定时扫描已领取未使用的优惠券，批量标记过期，过期券不可再使用。

### Requirement: 成长值与会员等级
系统 SHALL 基于近 12 个月成长值累计评定会员等级，每日自动评估，等级变更发布事件。

### Requirement: 积分过期处理
系统 SHALL 定时扫描积分流水，按先进先出原则标记过期积分，批处理避免大事务。

### Requirement: 通知重试与死信
系统 SHALL 实现可重试错误指数退避重试（3 次），不可重试错误直接死信，提供死信管理 API。

### Requirement: 模板渲染服务
系统 SHALL 实现模板变量渲染，必填变量缺失拒绝发送，可选变量缺失渲染成功，HTML 特殊字符转义。

### Requirement: 渠道参数配置管理
系统 SHALL 支持渠道参数 CRUD，敏感参数加密存储脱敏展示，配置变更热更新适配器。

### Requirement: 通知频率限制
系统 SHALL 基于 Redis 滑动窗口实现频率限制，短信 5 条/小时/收件人，Redis 不可用降级放行。

### Requirement: 系统健康监控
系统 SHALL 聚合各模块健康状态，整体取最差状态，健康端点不可达标记 Unhealthy 并告警。

### Requirement: 基础设施抽象实现
系统 SHALL 实现 RabbitMqDeadLetterManager、ElasticsearchRebuildTrigger、HttpModuleHealthProbe、RedisRateLimitCounter 四个基础设施组件。

## MODIFIED Requirements
无

## REMOVED Requirements
无
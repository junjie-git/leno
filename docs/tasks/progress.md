# 开发进度总览 (Progress)

> **项目**: Leno 电商平台 DDD 微服务系统  
> **最后更新**: 2026-07-10  
> **技术栈**: .NET 10 / ASP.NET Core / EF Core / SQL Server / Redis / RabbitMQ / Elasticsearch  
> **架构**: DDD + CQRS + 事件驱动 + 模块化单体可拆分

---

## 模块完成状态

| # | 模块 | 限界上下文 | 任务数 | 状态 | 任务文件 |
|---|------|-----------|--------|------|---------|
| 0 | 共享内核与基础设施 | Shared Kernel | 10 | [x] 已完成 | [shared-kernel.md](./shared-kernel.md) |
| 1 | 用户与认证授权域 | BC1 | 10 | [x] 已完成 | [user-auth.md](./user-auth.md) |
| 2 | 商品域 | BC2 | 11 | [ ] 未开始 | [product.md](./product.md) |
| 3 | 购物车域 | BC3 | 6 | [ ] 未开始 | [cart.md](./cart.md) |
| 4 | 订单与交易域 | BC4 | 12 | [ ] 未开始 | [order.md](./order.md) |
| 5 | 促销域 | BC5 | 10 | [ ] 未开始 | [promotion.md](./promotion.md) |
| 6 | 评价与售后域 | BC6 | 9 | [ ] 未开始 | [review-aftersales.md](./review-aftersales.md) |
| 7 | 积分与会员域 | BC7 | 11 | [ ] 未开始 | [points-membership.md](./points-membership.md) |
| 8 | 支付集成域 | BC8 | 10 | [ ] 未开始 | [payment.md](./payment.md) |
| 9 | 消息通知域 | BC9 | 10 | [ ] 未开始 | [notification.md](./notification.md) |
| 10 | 卖家与店铺管理域 | BC10 | 8 | [x] 已完成 | [seller-shop.md](./seller-shop.md) |
| 11 | 系统管理域 | BC11 | 13 | [ ] 未开始 | [system-admin.md](./system-admin.md) |

**合计**: 12 个模块 / 120 个任务 / 28 个已完成

---

## 模块完成 Checklist

- [x] **共享内核与基础设施** — 值对象、领域基础、发件箱、仓储抽象、横切关注点、消息总线、ES 读模型、配置中心
- [x] **用户与认证授权域** — User/Address 聚合、JWT 鉴权、密码管理、地址管理、审计日志
- [ ] **商品域** — SPU/SKU 聚合、分类品牌、库存基线、ES 搜索、商品发布审核
- [ ] **购物车域** — Cart 聚合、Redis 缓存、结算预览、订单事件消费
- [ ] **订单与交易域** — Order 聚合、库存预占、状态机、ES 读模型、支付/发货/收货全流程
- [ ] **促销域** — 满减活动、优惠券、秒杀活动、Redis 秒杀库存、订单事件消费
- [ ] **评价与售后域** — Review 聚合、AfterSales 聚合、退款流程、评价审核
- [ ] **积分与会员域** — 积分账户、签到、会员等级、会员订阅、积分抵现
- [ ] **支付集成域** — 支付订单、退款订单、微信/支付宝适配器、回调处理、补偿任务
- [ ] **消息通知域** — 通知模板、站内信/短信/邮件渠道、事件消费、重试调度
- [x] **卖家与店铺管理域** — Shop 聚合、卖家入驻审核、店铺状态联动、工作台数据
- [ ] **系统管理域** — 运营人员、系统配置、审计日志、数据字典、公告、功能开关、定时任务

---

## 模块依赖关系

```
shared-kernel (0) ─────────────────────────────────────────────────
    │
    ├── user-auth (1) ──────────────────────┐
    ├── seller-shop (10) ───────────────────┤
    │       │                                │
    │       ▼                                │
    ├── product (2) ◄── seller-shop (10)     │
    │       │                                │
    │       ▼                                │
    ├── cart (3) ◄── product (2)             │
    │       │                                │
    │       ▼                                │
    ├── promotion (5)                        │
    │       │                                │
    │       ▼                                │
    ├── points-membership (7)                │
    │       │                                │
    │       ▼                                │
    ├── order (4) ◄── product(2) cart(3)     │
    │                   promotion(5)         │
    │                   points(7) user-auth(1)│
    │       │                                │
    │       ▼                                │
    ├── payment (8) ◄── order (4)            │
    │       │                                │
    │       ▼                                │
    ├── review-aftersales (6) ◄── order(4)   │
    │                               payment(8)│
    │                                        │
    ├── notification (9) ◄── 所有域事件       │
    │                                        │
    └── system-admin (11) ◄── 所有域审计日志  │
```

---

## 推荐开发顺序

### 阶段一: 基础设施 (必须先行)
1. **shared-kernel** — 所有模块的基础，必须首先完成

### 阶段二: 核心身份与商品 (可并行)
2. **user-auth** — 用户注册登录是所有业务的前提
3. **seller-shop** — 卖家入驻是商品发布的前提

### 阶段三: 商品与购物 (依赖阶段二)
4. **product** — 依赖 seller-shop 的 ShopId
5. **cart** — 依赖 product 的 SKU 价格与库存

### 阶段四: 促销与积分 (可并行，依赖阶段三)
6. **promotion** — 独立模块，订单创建时调用
7. **points-membership** — 独立模块，订单创建时调用

### 阶段五: 核心交易 (依赖阶段三+四)
8. **order** — 依赖 product/cart/promotion/points/user-auth，核心交易链路

### 阶段六: 支付与售后 (依赖阶段五)
9. **payment** — 依赖 order 的支付请求事件
10. **review-aftersales** — 依赖 order 完成事件与 payment 退款事件

### 阶段七: 横切服务 (可并行，依赖事件契约定义后)
11. **notification** — 消费所有域事件，各域事件定义后即可开发
12. **system-admin** — 消费审计日志，可后期接入

---

## 各模块任务明细统计

| 模块 | 领域层任务 | 基础设施层任务 | 应用层任务 | 表现层任务 | 合计 |
|------|-----------|--------------|-----------|-----------|------|
| shared-kernel | 3 | 7 | 0 | 0 | 10 |
| user-auth | 4 | 2 | 2 | 2 | 10 |
| product | 5 | 2 | 2 | 2 | 11 |
| cart | 2 | 1 | 1 | 2 | 6 |
| order | 5 | 2 | 4 | 1 | 12 |
| promotion | 5 | 2 | 2 | 1 | 10 |
| review-aftersales | 4 | 2 | 2 | 1 | 9 |
| points-membership | 6 | 2 | 2 | 1 | 11 |
| payment | 3 | 5 | 1 | 1 | 10 |
| notification | 2 | 6 | 1 | 1 | 10 |
| seller-shop | 4 | 1 | 2 | 1 | 8 |
| system-admin | 5 | 3 | 3 | 2 | 13 |
| **合计** | **44** | **35** | **24** | **17** | **120** |

---

## 关键里程碑

- [x] **M1: 基础设施就绪** — shared-kernel 完成，所有模块可开始开发
- [ ] **M2: 身份与店铺就绪** — user-auth + seller-shop 完成，可注册登录、卖家入驻审核（product 完成后达成）
- [ ] **M3: 购物车可用** — cart 完成，可添加购物车、结算预览
- [ ] **M4: 交易闭环** — order + payment + promotion + points-membership 完成，可下单支付
- [ ] **M5: 售后完整** — review-aftersales 完成，可评价、退款退货
- [ ] **M6: 通知与管理** — notification + system-admin 完成，全功能可用
- [ ] **M7: Leno 系统上线** — 所有模块完成，集成测试通过，部署上线

---

## 事件流总览

### 核心交易事件链
```
用户注册 → UserRegisteredEvent
    ↓
商品发布 → ProductPublishedEvent
    ↓
加入购物车 → (无事件，购物车内部操作)
    ↓
创建订单 → OrderCreatedEvent
    ├──→ 购物车域: 清空已结算项
    ├──→ 促销域: 锁定优惠券
    ├──→ 积分域: 冻结积分
    ├──→ 通知域: 下单成功通知
    └──→ MQ延迟消息: 30分钟超时取消
    ↓
发起支付 → PaymentRequestedIntegrationEvent
    ↓
支付成功 → PaymentSucceededEvent
    ├──→ 订单域: MarkAsPaid → OrderPaidEvent
    ├──→ 积分域: 确认积分扣减
    ├──→ 促销域: 核销优惠券
    └──→ 通知域: 支付成功通知
    ↓
卖家发货 → OrderShippedEvent
    └──→ 通知域: 发货通知
    ↓
确认收货 → OrderCompletedEvent
    ├──→ 评价域: 开放评价入口
    ├──→ 积分域: 消费奖励积分
    ├──→ 卖家域: 更新销量
    └──→ MQ延迟消息: 售后期结束
    ↓
售后期结束 → OrderAfterSalesWindowClosedEvent
```

### 退款事件链
```
售后申请 → AfterSalesSubmittedEvent
    ↓
审核通过 → AfterSalesApprovedEvent
    ├──→ RefundRequestedIntegrationEvent → 支付域
    └──→ 通知域: 售后通过通知
    ↓
退款完成 → RefundSucceededEvent → 售后域
    ↓
RefundCompletedEvent
    ├──→ 订单域: 回滚销量库存
    ├──→ 通知域: 退款到账通知
    └──→ 系统管理域: 记录操作日志
```

### 店铺状态联动事件链
```
店铺暂停 → ShopSuspendedEvent → 商品域: 商品不可售
店铺恢复 → ShopResumedEvent → 商品域: 商品恢复可售
店铺关闭 → ShopClosedEvent
    ├──→ 商品域: 下架全部商品
    └──→ 用户域: 移除卖家角色
```

---

## 开发规范提醒

- **DDD 分层**: 每个模块严格按 Domain → Infrastructure → Application → API 四层组织
- **事件驱动**: 跨上下文通信仅通过集成事件，禁止直接数据库共享或 RPC 调用
- **防腐层**: 下游域通过接口抽象访问上游域，不直接依赖上游领域模型
- **发件箱模式**: 所有事件发布通过 Outbox 表保证事务一致性
- **幂等消费**: 所有事件消费者以 EventId 去重，防止重复处理
- **CQRS**: 写侧 EF Core + 聚合根，读侧 Elasticsearch 读模型
- **测试优先**: 每个任务包含单元测试或集成测试验证
- **提交规范**: `feat(<scope>): <description>` / `fix(<scope>): <description>` / `chore: <description>`

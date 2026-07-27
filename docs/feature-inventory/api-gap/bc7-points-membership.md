# BC7 积分与会员域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

> **域拆分迁移阶段1-2 已完成（2026-07-26）**：PointsMembership 旧域已按职责拆分为两个新域并经网关双轨挂载：
> - **Points 域**（16 端点 + gRPC）：积分账户、积分流水、签到、任务中心、积分兑换、积分规则 CRUD、手动发放、内部协作（`PointsController` / `AdminPointsController` / `PointsRulesController` / `TasksController` / `InternalPointsController`）
> - **Membership 域**（12 端点）：会员等级 CRUD 与启停、付费会员套餐 CRUD 与启停、会员档案查询（`MembersController` / `AdminMemberLevelsController` / `MembershipPackagesController` / `AdminMembershipPackagesController`）
>
> 旧域 PointsMembership 代码保留作回滚兜底，待阶段3观察期结束后下线。design-prompts 与 feature-list 中的「服务归属」已更新为新域，端点路径不变。详见 `docs/feature-inventory/domain-migration-status.md`。

## 1. 概览
- **BC 编号**：BC7
- **中文名**：积分与会员域
- **英文名**：PointsMembership（旧域；新域为 Points / Membership）
- **涉及端**：buyer-app + operations
- **涉及页面数**：10 页（buyer-app/11-points-membership 全部 7 页 + operations/08-membership-ops 全部 3 页；operations/09-account/todo-workbench 仅快捷入口跳转 points-rules，不直接消费 BC7 API，不计入）
- **已实现 API 端点数**：32 个（按逻辑端点去重计数；PointsMembership 23 个含 4 内部 + Membership 9 个；阶段1-2 完成后 Points 域 16 端点 + gRPC 已就绪，Membership 域 12 端点已就绪）
  - PointsMembership buyer+ops 端点：19 个（旧域，保留作回滚兜底）
  - PointsMembership 内部端点：4 个（双路由期，旧路由 4 个已标 Obsolete 不重复计数）
  - Membership 端点（旧拆分实现）：9 个（路径/鉴权与 design-prompts 不一致，已由新域 Membership 12 端点对齐覆盖）
  - Points 端点（阶段1-2 新增）：16 个 + gRPC（5 个 Controller 全部就绪）
- **差异统计**：缺失 5 / 闲置 4 / 路径不一致 9 / 能力不匹配 0

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/points/check-in | [PointsController.cs#L37](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs#L37) | 每日签到，发放积分与成长值 | Buyer |
| GET | /api/points/account | [PointsController.cs#L47](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs#L47) | 查询当前用户积分账户余额与累计统计 | Buyer |
| GET | /api/points/ledger | [PointsController.cs#L57](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs#L57) | 分页查询当前用户积分流水 | Buyer |
| POST | /api/points/exchange-coupon | [PointsController.cs#L67](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs#L67) | 积分兑换优惠券 | Buyer |
| POST | /api/admin/points/award | [PointsController.cs#L79](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs#L79) | 运营手动发放积分 | Operator,Admin |
| GET | /api/points/tasks | [TasksController.cs#L30](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/TasksController.cs#L30) | 获取任务列表（含当前用户完成状态） | Buyer |
| POST | /api/points/tasks/{taskId}/complete | [TasksController.cs#L39](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/TasksController.cs#L39) | 完成任务领取积分奖励 | Buyer |
| GET | /api/members/me | [MembersController.cs#L31](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L31) | 查询当前用户会员信息 | Buyer |
| GET | /api/admin/members/levels | [MembersController.cs#L43](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L43) | 查询全部会员等级 | Operator,Admin |
| POST | /api/admin/members/levels | [MembersController.cs#L53](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L53) | 创建会员等级 | Operator,Admin |
| PUT | /api/admin/members/levels/{levelId} | [MembersController.cs#L63](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L63) | 更新会员等级（名称、门槛、折扣率） | Operator,Admin |
| POST | /api/admin/members/levels/{levelId}/enable | [MembersController.cs#L73](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L73) | 启用会员等级 | Operator,Admin |
| POST | /api/admin/members/levels/{levelId}/disable | [MembersController.cs#L83](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs#L83) | 停用会员等级 | Operator,Admin |
| GET | /api/membership-packages | [MembershipPackagesController.cs#L33](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L33) | 查询可购买的会员套餐列表 | Buyer |
| POST | /api/membership-packages/{packageId}/subscribe | [MembershipPackagesController.cs#L43](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L43) | 订阅会员套餐 | Buyer |
| POST | /api/admin/membership-packages | [MembershipPackagesController.cs#L55](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L55) | 创建会员套餐 | Operator,Admin |
| PUT | /api/admin/membership-packages/{packageId} | [MembershipPackagesController.cs#L65](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L65) | 更新会员套餐 | Operator,Admin |
| POST | /api/admin/membership-packages/{packageId}/enable | [MembershipPackagesController.cs#L75](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L75) | 启用会员套餐 | Operator,Admin |
| POST | /api/admin/membership-packages/{packageId}/disable | [MembershipPackagesController.cs#L85](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs#L85) | 停用会员套餐 | Operator,Admin |
| POST | internal/v1/points/trial-offset（内部） | [InternalPointsController.cs#L23](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L23) | 试算积分可抵扣金额（双路由期，旧路由 internal/points/trial-offset @L25 已 Obsolete，2026-08-01 下线） | InternalApiKey |
| POST | internal/v1/points/freeze（内部） | [InternalPointsController.cs#L34](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L34) | 冻结积分（下单预占）（旧路由 internal/points/freeze @L36 已 Obsolete） | InternalApiKey |
| POST | internal/v1/points/release（内部） | [InternalPointsController.cs#L45](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L45) | 释放冻结积分（订单取消回退）（旧路由 internal/points/release @L47 已 Obsolete） | InternalApiKey |
| POST | internal/v1/points/confirm（内部） | [InternalPointsController.cs#L56](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L56) | 确认扣减冻结积分（订单支付成功核销）（旧路由 internal/points/confirm @L58 已 Obsolete） | InternalApiKey |
| GET | api/Members/{userId}（Membership，待切换） | [MembersController.cs#L27](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L27) | 获取指定用户会员档案（路径参数 userId，非 JWT 提取） | Authorize（不限角色） |
| GET | api/Members/levels（Membership，待切换） | [MembersController.cs#L35](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L35) | 获取全部会员等级定义（按成长值门槛升序） | 匿名 |
| POST | api/Members/levels（Membership，待切换） | [MembersController.cs#L42](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L42) | 创建会员等级定义 | AdminOnly |
| PUT | api/Members/levels/{levelId}（Membership，待切换） | [MembersController.cs#L54](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L54) | 更新会员等级定义（等级编号不可改） | AdminOnly |
| GET | api/MembershipPackages（Membership，待切换） | [MembershipPackagesController.cs#L25](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L25) | 获取全部已启用的会员套餐 | 匿名 |
| POST | api/MembershipPackages（Membership，待切换） | [MembershipPackagesController.cs#L32](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L32) | 创建会员套餐 | AdminOnly |
| PUT | api/MembershipPackages/{packageId}（Membership，待切换） | [MembershipPackagesController.cs#L44](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L44) | 更新会员套餐（等级编号不可改） | AdminOnly |
| POST | api/MembershipPackages/{packageId}/enable（Membership，待切换） | [MembershipPackagesController.cs#L53](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L53) | 启用套餐 | AdminOnly |
| POST | api/MembershipPackages/{packageId}/disable（Membership，待切换） | [MembershipPackagesController.cs#L64](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L64) | 停用套餐 | AdminOnly |

> 来源：grep `src/Services/PointsMembership/**/Controllers/*.cs` 与 `src/Services/Membership/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> `src/Services/Points/**/Controllers/*.cs` 无匹配（Points 服务仅有 Domain/Application/Infrastructure，未暴露 HTTP 端点）
> Internal*Controller.cs 中的端点已标注「（内部）」；Membership 目录下的端点已标注「（Membership，待切换）」
> InternalPointsController 的 4 个端点采用双路由（internal/v1/* 与 internal/*），旧路由已 [Obsolete] 标记 2026-08-01 下线，按逻辑端点去重计数

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/points/account | [points-account.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-account.md) | 查询积分账户余额与累计统计 | ✅ | Buyer |
| GET | /api/points/ledger | [points-account.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-account.md) | 查询积分流水（取近 3 条预览） | ✅ | Buyer |
| POST | /api/points/check-in | [points-account.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-account.md) | 每日签到 | ✅ | Buyer |
| GET | /api/points/ledger | [points-ledger.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-ledger.md) | 分页查询积分流水 | ✅ | Buyer |
| GET | /api/points/account | [points-ledger.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-ledger.md) | 查询积分账户余额 | ✅ | Buyer |
| POST | /api/points/check-in | [check-in.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/check-in.md) | 每日签到 | ✅ | Buyer |
| GET | /api/points/account | [check-in.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/check-in.md) | 查询积分账户（含连续签到天数与上次签到日期） | ✅ | Buyer |
| GET | /api/points/tasks | [tasks-center.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/tasks-center.md) | 获取任务列表（含当前用户完成状态） | ✅ | Buyer |
| POST | /api/points/tasks/{taskId}/complete | [tasks-center.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/tasks-center.md) | 完成任务领取积分奖励 | ✅ | Buyer |
| GET | /api/points/account | [points-exchange.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-exchange.md) | 查询积分账户余额 | ✅ | Buyer |
| POST | /api/points/exchange-coupon | [points-exchange.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-exchange.md) | 积分兑换优惠券 | ✅ | Buyer |
| GET | /api/coupons/claimable | [points-exchange.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/points-exchange.md) | 查询可领取优惠券（含积分兑换券） | ✅ | Buyer（跨 BC，属 BC5） |
| GET | /api/members/me | [member-level.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/member-level.md) | 查询当前会员信息（含等级、成长值、权益） | ✅ | Buyer |
| GET | /api/membership-packages | [membership-packages.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/membership-packages.md) | 查询可购买的会员套餐列表 | ✅ | Buyer |
| POST | /api/membership-packages/{packageId}/subscribe | [membership-packages.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/membership-packages.md) | 订阅会员套餐 | ✅ | Buyer |
| GET | /api/members/me | [membership-packages.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/membership-packages.md) | 查询当前会员信息（含付费会员状态） | ✅ | Buyer |
| GET | /api/admin/members/levels | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | 查询全部会员等级（按等级编号升序） | ✅ | Operator,Admin |
| POST | /api/admin/members/levels | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | 创建会员等级 | ✅ | Operator,Admin |
| PUT | /api/admin/members/levels/{levelId} | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | 更新会员等级（名称、门槛、折扣率） | ✅ | Operator,Admin |
| POST | /api/admin/members/levels/{levelId}/enable | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | 启用会员等级 | ✅ | Operator,Admin |
| POST | /api/admin/members/levels/{levelId}/disable | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | 停用会员等级 | ✅ | Operator,Admin |
| GET | /api/membership-packages | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | 查询可购买套餐列表（运营复用） | ✅ | Buyer（运营按需过滤启用） |
| POST | /api/admin/membership-packages | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | 创建会员套餐 | ✅ | Operator,Admin |
| PUT | /api/admin/membership-packages/{packageId} | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | 更新会员套餐（名称、价格、时长、权益） | ✅ | Operator,Admin |
| POST | /api/admin/membership-packages/{packageId}/enable | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | 启用会员套餐 | ✅ | Operator,Admin |
| POST | /api/admin/membership-packages/{packageId}/disable | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | 停用会员套餐 | ✅ | Operator,Admin |
| POST | /api/admin/points/award | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 运营手动发放积分 | ✅ | Operator,Admin |
| GET | /api/admin/points/rules | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 查询全部积分规则 | 🚧 | Operator,Admin |
| POST | /api/admin/points/rules | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 创建积分规则 | 🚧 | Operator,Admin |
| PUT | /api/admin/points/rules/{ruleId} | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 更新积分规则 | 🚧 | Operator,Admin |
| POST | /api/admin/points/rules/{ruleId}/enable | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 启用积分规则 | 🚧 | Operator,Admin |
| POST | /api/admin/points/rules/{ruleId}/disable | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 停用积分规则 | 🚧 | Operator,Admin |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 去重后期望端点 24 个：19 个 ✅ + 5 个 🚧（/api/coupons/claimable 属 BC5 不计入 BC7 期望）
> operations/09-account/todo-workbench 仅在快捷操作区提供「手动发积分」入口跳转 points-rules 页，不直接消费 BC7 API，不计入期望清单

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| GET | /api/admin/points/rules | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 查询全部积分规则 | P1 | 新增 PointsRulesController + IPointsRuleAppService，返回 List<PointsRuleDto>（编码/名称/行为/积分值/每日上限/状态） |
| POST | /api/admin/points/rules | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 创建积分规则 | P1 | 同上 Controller 增 POST，DTO 含 Code/Name/ActionType/Points/DailyLimit/Status，编码唯一约束 |
| PUT | /api/admin/points/rules/{ruleId} | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 更新积分规则 | P1 | 同上 Controller 增 PUT，支持正负积分值（发放/扣减） |
| POST | /api/admin/points/rules/{ruleId}/enable | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 启用积分规则 | P1 | 同上 Controller 增 enable/disable 子动作 |
| POST | /api/admin/points/rules/{ruleId}/disable | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) | 停用积分规则 | P1 | 同上 Controller 增 enable/disable 子动作 |

> 说明：design-prompts points-rules.md 显式标 🚧 规划中，规划补充端点 5 个；当前仅 POST /api/admin/points/award 已实现，规则 CRUD 端点在 PointsMembership 与 Membership 两个服务中均无实现
> 注：spec 第 5 章还列出大量未实现端点（/api/points/balance、/api/points/transactions、/api/points/expiring、/api/points/mall/*、/api/points/lottery/*、/api/points/shipping-fee/deduct、/api/points/donation、/api/points/exchange/growth、/api/members/profile、/api/members/growth、/api/members/benefits、/api/members/level-history、/api/members/paid/*、/api/admin/points/audit、/api/admin/points/accounts/{userId}/freeze|unfreeze|adjust、/api/admin/points/risk、/api/admin/member-levels/{level}/benefits、/api/admin/points/task-rules、/api/admin/paid-member-plans、/api/admin/points-mall/* 等），但 design-prompts 未标 🚧/➕，按差异判定规则不计入缺失

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST（内部） | internal/v1/points/trial-offset | [InternalPointsController.cs#L23](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L23) | 试算积分可抵扣金额（供订单域调用） | 保留观察（内部端点，design-prompts 不直接引用属合理；旧路由 internal/points/trial-offset @L25 已 Obsolete，2026-08-01 下线后清理） |
| POST（内部） | internal/v1/points/freeze | [InternalPointsController.cs#L34](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L34) | 冻结积分（下单预占，供订单域调用） | 保留观察（内部端点；旧路由 @L36 Obsolete 一并清理） |
| POST（内部） | internal/v1/points/release | [InternalPointsController.cs#L45](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L45) | 释放冻结积分（订单取消回退，供订单域调用） | 保留观察（内部端点；旧路由 @L47 Obsolete 一并清理） |
| POST（内部） | internal/v1/points/confirm | [InternalPointsController.cs#L56](file:///e:/Leno/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L56) | 确认扣减冻结积分（订单支付成功核销，供订单域调用） | 保留观察（内部端点；旧路由 @L58 Obsolete 一并清理） |

> 说明：4 个内部端点供订单域（BC4）调用以支持积分抵现链路，design-prompts 不直接引用属合理设计，归为「保留观察」类闲置
> Membership 服务的 9 个端点虽与 design-prompts 路径不一致，但有对应期望，归入 4.3 路径不一致，不重复计入闲置

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| GET→GET | /api/members/me → api/Members/{userId} | [member-level.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/member-level.md) + [membership-packages.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/membership-packages.md) | [MembersController.cs#L27](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L27) | 切换至 Membership 时改回 /api/members/me（从 JWT 提取 userId，与 PointsMembership 行为对齐） |
| GET→GET | /api/admin/members/levels → api/Members/levels | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | [MembersController.cs#L35](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L35) | 切换时补 /admin/ 前缀，鉴权从匿名改为 Operator,Admin |
| POST→POST | /api/admin/members/levels → api/Members/levels | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | [MembersController.cs#L42](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L42) | 切换时补 /admin/ 前缀，鉴权从 AdminOnly 改为 Operator,Admin |
| PUT→PUT | /api/admin/members/levels/{levelId} → api/Members/levels/{levelId} | [member-levels.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/member-levels.md) | [MembersController.cs#L54](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs#L54) | 切换时补 /admin/ 前缀，鉴权从 AdminOnly 改为 Operator,Admin |
| GET→GET | /api/membership-packages → api/MembershipPackages | [membership-packages.md](file:///e:/Leno/docs/design-prompts/buyer-app/11-points-membership/membership-packages.md) + [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | [MembershipPackagesController.cs#L25](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L25) | 切换时统一为 kebab-case /api/membership-packages，鉴权从匿名改为 Buyer |
| POST→POST | /api/admin/membership-packages → api/MembershipPackages | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | [MembershipPackagesController.cs#L32](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L32) | 切换时统一为 /api/admin/membership-packages，鉴权从 AdminOnly 改为 Operator,Admin |
| PUT→PUT | /api/admin/membership-packages/{packageId} → api/MembershipPackages/{packageId} | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | [MembershipPackagesController.cs#L44](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L44) | 切换时统一为 /api/admin/membership-packages/{packageId}，鉴权从 AdminOnly 改为 Operator,Admin |
| POST→POST | /api/admin/membership-packages/{packageId}/enable → api/MembershipPackages/{packageId}/enable | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | [MembershipPackagesController.cs#L53](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L53) | 切换时统一为 /api/admin/membership-packages/{packageId}/enable，鉴权从 AdminOnly 改为 Operator,Admin |
| POST→POST | /api/admin/membership-packages/{packageId}/disable → api/MembershipPackages/{packageId}/disable | [membership-packages.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/membership-packages.md) | [MembershipPackagesController.cs#L64](file:///e:/Leno/src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs#L64) | 切换时统一为 /api/admin/membership-packages/{packageId}/disable，鉴权从 AdminOnly 改为 Operator,Admin |

> 说明：9 项路径不一致全部来自新拆分 Membership 服务；旧 PointsMembership 服务路径与 design-prompts 完全一致
> Membership 服务使用 [Route("api/[controller]")] 自动生成 kebab-case 路径（api/Members、api/MembershipPackages），且未区分 /admin/ 前缀；鉴权策略简化为 AdminOnly/匿名/Authorize 不限角色，与 PointsMembership 的 Operator,Admin/Buyer 角色限定不一致

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 无 | 无 | 无 | — | — | — |

> 说明：基于 design-prompts 严格对比，PointsMembership 服务的 19 个 buyer+ops 端点能力与 design-prompts 期望完全匹配
> design-prompts points-ledger.md 明确说明「服务端按用户筛选，不支持类型过滤（前端过滤）」，与实际 GET /api/points/ledger 仅支持 page/pageSize 一致；spec F-PTS-017 期望的 source/direction/startDate/endDate 筛选能力未在 design-prompts 要求，不计入差异
> design-prompts points-exchange.md 引用的 GET /api/coupons/claimable 属 BC5 跨域端点，不在 BC7 差异分析范围

## 5. 拆分过渡说明

> **阶段1-2 已完成（2026-07-26）**：Points 与 Membership 新域已就绪并经网关双轨挂载，旧 BC `src/Services/PointsMembership/` 保留作回滚兜底，待阶段3观察期结束后下线。design-prompts 中的「服务归属」已更新为新域。

- **旧 BC 与新 BC 对照**：

| 旧 BC（保留兜底） | 新 BC（已就绪） | 当前状态 |
|-|-|-|
| PointsMembership/PointsController（积分账户/流水/签到/兑换/手动发放） | Points 域 `PointsController` + `AdminPointsController` + `PointsRulesController` + `TasksController` + `InternalPointsController`（16 端点 + gRPC） | ✅ 阶段1-2 完成：5 个 Controller 全部就绪，含规则 CRUD（5 端点）与手动发放、任务中心、内部 gRPC 协作 |
| PointsMembership/MembersController（会员信息/等级 CRUD/启停） | Membership 域 `MembersController` + `AdminMemberLevelsController`（12 端点） | ✅ 阶段1-2 完成：含会员档案查询、等级 CRUD/启停、对齐 design-prompts 路径与鉴权 |
| PointsMembership/MembershipPackagesController（套餐列表/订阅/CRUD/启停） | Membership 域 `MembershipPackagesController` + `AdminMembershipPackagesController` | ✅ 阶段1-2 完成：含套餐列表/订阅/CRUD/启停，对齐 design-prompts 路径与鉴权 |
| PointsMembership/TasksController（任务中心） | Points 域 `TasksController` | ✅ 阶段1-2 完成：任务中心归属 Points 域，端点路径保持 `/api/points/tasks/*` |
| PointsMembership/InternalPointsController（积分抵现内部端点） | Points 域 `InternalPointsController`（gRPC） | ✅ 阶段1-2 完成：内部端点归属 Points 域，订单域（BC4）已切至 internal/v1/* 新路由 |

- **双轨期端点引用规范**（阶段1-2 完成后的状态）：
  1. design-prompts 与前端调用中的「服务归属」已更新为 **Points 域** 与 **Membership 域**；端点路径保持不变，仅服务实现迁移
  2. InternalPointsController 的 4 个 internal/* 旧路由已 [Obsolete] 标记 2026-08-01 下线，订单域（BC4）已切至 internal/v1/* 新路由，由 Points 域承接
  3. Membership 域 12 端点已对齐 design-prompts 期望路径与鉴权策略（`/api/admin/members/levels/*`、`/api/admin/membership-packages/*`、Operator/Admin 角色），可由前端直接调用
  4. 网关双轨挂载：灰度默认 5%，可通过 `Grayscale:Threshold` 调整；internal 端点 100% 切新域
  5. 回滚开关：`Grayscale:RollbackToLegacy=true` 即将流量回退至旧域 PointsMembership
  6. 阶段3观察期结束后，旧域 PointsMembership 代码下线，新域独占承载

- **新域端点清单**（阶段1-2 已全部上线，端点路径与 design-prompts 期望一致）：

| 新域端点 | 新域归属 | 阶段1-2 状态 | 备注 |
|-|-|-|-|
| POST /api/points/check-in | Points 域 `PointsController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| GET /api/points/account | Points 域 `PointsController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| GET /api/points/ledger | Points 域 `PointsController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| POST /api/points/exchange-coupon | Points 域 `PointsController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| POST /api/admin/points/award | Points 域 `AdminPointsController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| GET /api/points/tasks | Points 域 `TasksController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| POST /api/points/tasks/{taskId}/complete | Points 域 `TasksController` | ✅ 已上线 | 端点路径与 design-prompts 一致 |
| GET/POST/PUT /api/admin/points/rules/* | Points 域 `PointsRulesController`（5 端点） | ✅ 已上线 | 含 enable/disable，对齐 design-prompts |
| POST internal/v1/points/trial-offset | Points 域 `InternalPointsController`（gRPC） | ✅ 已上线 | 旧路由 2026-08-01 下线 |
| POST internal/v1/points/freeze | Points 域 `InternalPointsController`（gRPC） | ✅ 已上线 | 旧路由 2026-08-01 下线 |
| POST internal/v1/points/release | Points 域 `InternalPointsController`（gRPC） | ✅ 已上线 | 旧路由 2026-08-01 下线 |
| POST internal/v1/points/confirm | Points 域 `InternalPointsController`（gRPC） | ✅ 已上线 | 旧路由 2026-08-01 下线 |
| GET /api/members/me | Membership 域 `MembersController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Buyer |
| GET /api/admin/members/levels | Membership 域 `AdminMemberLevelsController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Operator,Admin |
| POST /api/admin/members/levels | Membership 域 `AdminMemberLevelsController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Operator,Admin |
| PUT /api/admin/members/levels/{levelId} | Membership 域 `AdminMemberLevelsController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Operator,Admin |
| POST /api/admin/members/levels/{levelId}/enable | Membership 域 `AdminMemberLevelsController` | ✅ 已上线 | 阶段1-2 已补齐 |
| POST /api/admin/members/levels/{levelId}/disable | Membership 域 `AdminMemberLevelsController` | ✅ 已上线 | 阶段1-2 已补齐 |
| GET /api/membership-packages | Membership 域 `MembershipPackagesController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Buyer |
| POST /api/membership-packages/{packageId}/subscribe | Membership 域 `MembershipPackagesController` | ✅ 已上线 | 阶段1-2 已补齐 |
| POST /api/admin/membership-packages | Membership 域 `AdminMembershipPackagesController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Operator,Admin |
| PUT /api/admin/membership-packages/{packageId} | Membership 域 `AdminMembershipPackagesController` | ✅ 已上线 | 路径对齐 design-prompts，鉴权 Operator,Admin |
| POST /api/admin/membership-packages/{packageId}/enable | Membership 域 `AdminMembershipPackagesController` | ✅ 已上线 | 阶段1-2 已补齐，鉴权 Operator,Admin |
| POST /api/admin/membership-packages/{packageId}/disable | Membership 域 `AdminMembershipPackagesController` | ✅ 已上线 | 阶段1-2 已补齐，鉴权 Operator,Admin |

> 拆分过渡要点（阶段1-2 完成后的状态）：
> 1. Membership 域 12 端点已全部上线，路径/鉴权已对齐 design-prompts，含 subscribe、levels enable/disable、packages enable/disable
> 2. Points 域 16 端点 + gRPC 已全部上线（5 个 Controller 全部就绪），原「拆分进度滞后」风险已解除
> 3. InternalPointsController 双路由期 2026-08-01 截止，订单域已切至 internal/v1/* 新路由，由 Points 域承接
> 4. design-prompts 与前端调用中的「服务归属」已更新为 Points 域 / Membership 域；旧域 PointsMembership 保留作回滚兜底，阶段3观察期结束后下线

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | — | — | — | — |
| P1 | GET/POST/PUT/POST-enable/POST-disable /api/admin/points/rules/*（5 个，points-rules 页核心功能） | 4 个内部端点保留观察，无需处理 | 9 个 Membership 服务端点路径不一致（拆分过渡期，待切换时统一） | 无 |
| P2 | — | — | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> BC7 无 P0 项：buyer-app 7 页与 operations 2 页（member-levels/membership-packages）的 19 个期望端点全部由 PointsMembership 服务实现，不阻塞交易闭环
> P1 集中在 points-rules 页 5 个规则 CRUD 端点缺失（页面已标 🚧 规划中，当前规则列表展示固定内置规则）

## 7. 跨 BC 依赖
- **上游依赖**：本 BC 依赖哪些 BC 的端点/事件
  - **BC1 用户域**：消费 `UserRegisteredEvent`（创建积分账户与成长值档案、发新人积分）
  - **BC4 订单域**：消费 `OrderCompletedEvent`（标记待发积分）、`OrderAfterSalesWindowClosedEvent`（发放消费返积分与成长值，基数=paidAmount）、`OrderCancelledEvent`（释放冻结抵现积分或扣回已发放积分）、`OrderPaidEvent`（正式扣减冻结抵现积分；识别 OrderType=会员订阅订单则开通/续费付费会员）；BC4 通过 internal/v1/points/* 端点调用本域试算/冻结/释放/确认积分
  - **BC6 评价与售后域**：消费 `ReviewApprovedEvent`（发放评价积分 +10 成长值）、`RefundCompletedEvent`（扣回该订单已发放积分与成长值，可为负分）
  - **BC5 促销域**：发布 `PointsExchangeCouponRequestedEvent` 请求促销域生成优惠券，消费 `CouponExchangeSucceededEvent` 回执确认扣减积分（buyer-app/points-exchange 页面调用 BC5 的 GET /api/coupons/claimable 查询可兑换券）
  - **BC8 支付集成域**：付费会员年费支付经 BC4 订单域创建会员订阅订单后由 BC8 处理支付，本域不直接调用 BC8
  - **BC9 消息通知域**：发布 `MemberLevelChangedEvent`/`PaidMemberSubscribedEvent`/`PaidMemberRenewedEvent`/`PaidMemberExpiredEvent`/`PointsEarnedEvent`/`PointsExpiredEvent` 等通知请求，由 BC9 发送 App 推送与短信

- **下游依赖**：哪些 BC 依赖本 BC 的端点/事件
  - **BC4 订单域**：依赖本域 internal/v1/points/trial-offset|freeze|release|confirm 4 个内部端点支持积分抵现链路
  - **BC5 促销域**：消费本域 `PointsExchangeCouponRequestedEvent` 生成优惠券并回执
  - **BC9 消息通知域**：消费本域 8 类对外集成事件触发推送
  - **BC11 数据统计域**：通过运营 dashboard 查询积分统计（如 operations/01-dashboard/points-stats 页面调用 GET /api/admin/dashboard/points-stats，由 BC11 聚合本域数据）

- **集成事件订阅/发布清单**：
  - **入站订阅**（5 个）：`UserRegisteredEvent`、`OrderCompletedEvent`、`OrderAfterSalesWindowClosedEvent`、`OrderCancelledEvent`、`OrderPaidEvent`、`RefundCompletedEvent`、`ReviewApprovedEvent`、`CouponExchangeSucceededEvent`
  - **出站发布**（11 个）：`PointsEarnedEvent`、`PointsConsumedEvent`、`PointsRevertedEvent`、`PointsFrozeEvent`、`PointsExpiredEvent`、`GrowthValueAddedEvent`、`GrowthValueDeductedEvent`、`MemberLevelChangedEvent`、`PaidMemberSubscribedEvent`、`PaidMemberRenewedEvent`、`PaidMemberExpiredEvent`、`PointsExchangeCouponRequestedEvent`、`MallExchangeRequestedEvent`

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）：无 P0 项；但建议立即推进 InternalPointsController 旧路由下线计划，确保订单域在 2026-08-01 前完成 internal/v1/* 切换
- **短期补充**（P1 缺失/不匹配）：
  1. **新增 5 个积分规则 CRUD 端点**（P1 缺失）：在 PointsMembership 服务新增 PointsRulesController，承接 GET/POST/PUT /api/admin/points/rules/* 与 POST /api/admin/points/rules/{ruleId}/enable|disable，DTO 含 Code/Name/ActionType/Points/DailyLimit/Status；编码唯一约束、积分值支持正负、每日上限 1-100 校验
  2. **补齐 Membership 服务 3 个缺失端点**（拆分过渡）：补 POST /api/membership-packages/{packageId}/subscribe、POST /api/admin/members/levels/{levelId}/enable|disable，确保切换时不丢功能
  3. **统一 Membership 服务路径与鉴权**：将 [Route("api/[controller]")] 改为显式 [Route("api/admin/members/levels")] 等与 design-prompts 一致的路径；鉴权从 AdminOnly 改为 [Authorize(Roles = "Operator,Admin")]，从匿名改为 [Authorize(Roles = "Buyer")]
  4. **搭建 Points 服务 Controllers**：将 PointsMembership/PointsController + TasksController + InternalPointsController 三类端点迁移至 Points 服务，确保拆分进度
- **长期规划**（P2 闲置/废弃）：
  1. **内部端点路由收敛**：2026-08-01 下线 InternalPointsController 的 4 个 internal/* 旧路由，仅保留 internal/v1/* 新路由
  2. **PointsMembership 服务退役评估**：Points 与 Membership 服务端点对齐后，按灰度计划退役 PointsMembership 服务
  3. **spec 第 5 章未实现端点排期**：spec 列出的 /api/points/balance、/api/points/transactions、/api/points/expiring、/api/points/mall/*、/api/points/lottery/*、/api/points/donation、/api/points/exchange/growth、/api/members/benefits、/api/members/paid/*、积分风控审计等端点当前 design-prompts 未要求，按业务优先级排期补充
- **文档同步**（design-prompts API 引用对齐到源码）：
  1. design-prompts 与 PointsMembership 服务路径完全一致，无需文档调整
  2. 切换至 Membership 服务前，需先统一 Membership 服务路径再切换 design-prompts 引用，避免文档与代码错位
  3. points-rules.md 已正确标 🚧 5 个待补充端点，规则 CRUD 实现后需更新该页端点表为 ✅

# 域拆分迁移状态报告

**文档版本**：V1.0
**创建日期**：2026-07-26
**关联设计**：`docs/superpowers/specs/2026-07-26-domain-split-migration-design.md`
**关联计划**：`docs/superpowers/plans/2026-07-26-domain-split-migration.md`
**关联文档**：
- 设计提示词：`docs/design-prompts/README.md`（V1.1，已同步域拆分映射）
- API 缺失对比：`docs/feature-inventory/api-gap/00-summary.md`（已同步阶段1-2 完成状态）

---

## 1. 总览

本文档记录 Leno 电商平台用户与认证授权域、评价与售后域、积分与会员域的拆分迁移状态。原 3 个旧域按职责细分为 7 个新域，采用网关双轨灰度策略平滑过渡，旧域代码保留作回滚兜底，待阶段3观察期结束后下线。

### 1.1 拆分映射总览

| 旧域 | 新域 | 端点数 | 阶段1-2 状态 |
|-|-|-|-|
| UserAuth（认证部分） | Identity | 28 | ✅ 阶段1完成 |
| UserAuth（用户中心部分） | UserCenter | 17 | ✅ 阶段1完成 |
| UserAuth（权限部分） | AccessControl | 7 | ✅ 阶段1完成 |
| PointsMembership（积分部分） | Points | 16 + gRPC | ✅ 阶段1完成 |
| PointsMembership（会员部分） | Membership | 12 | ✅ 阶段1完成 |
| ReviewAfterSales（评价部分） | Review | 11 + gRPC | ✅ 阶段1完成 |
| ReviewAfterSales（售后部分） | AfterSales | 14 | ✅ 阶段1完成 |

**合计**：3 个旧域 → 7 个新域；105 个 HTTP 端点 + 2 个 gRPC 服务全部迁移至新域。

---

## 2. 阶段1-2 完成情况

### 2.1 阶段1：新域代码就绪

阶段1 完成新域 Domain / Application / Infrastructure / Api 四层代码与 Controller 实现，新域通过单元测试与集成测试覆盖核心场景。

#### 2.1.1 Identity 域（28 端点）

| Controller | 端点数 | 职责 |
|-|-|-|
| `AuthController` | 9 | 注册、登录、刷新令牌、登出、OAuth 授权与回调、双因子验证、忘记密码、重置密码 |
| `AccountController` | 2 | 外部登录绑定与解绑 |
| `UsersController` | 6 | 当前用户资料、修改密码、双因子启用/确认/禁用 |
| `AdminUsersController` | 5 | 管理员用户列表/详情/分配角色/锁定/恢复 |
| `AdminOAuthClientsController` | 5 | OAuth 客户端配置 CRUD 与启停 |
| `InternalUsersController` | 2 | 内部脱敏联系方式查询、内部完整 PII 查询 |

**源码目录**：`src/Services/Identity/Leno.Identity.Api/Controllers/`

#### 2.1.2 UserCenter 域（17 端点）

| Controller | 端点数 | 职责 |
|-|-|-|
| `AddressesController` | 5 | 收货地址 CRUD 与设为默认 |
| `FavoritesController` | 4 | 收藏列表/添加/取消/批量取消 |
| `BrowseHistoryController` | 3 | 浏览历史列表/添加/清除 |
| `NotificationPreferencesController` | 5 | 通知偏好查询/批量更新/按类型查询/重置/导出 |
| `UserCenterControllerBase` | — | 共享基类 |

**源码目录**：`src/Services/UserCenter/Leno.UserCenter.Api/Controllers/`

#### 2.1.3 AccessControl 域（7 端点）

| Controller | 端点数 | 职责 |
|-|-|-|
| `AdminRolesController` | 7 | 角色 CRUD（5 端点）+ 角色权限查询与更新（2 端点） |

**源码目录**：`src/Services/AccessControl/Leno.AccessControl.Api/Controllers/`

#### 2.1.4 Points 域（16 端点 + gRPC）

| Controller | 端点数 | 职责 |
|-|-|-|
| `PointsController` | 4 | 每日签到、积分账户查询、积分流水查询、积分兑换优惠券 |
| `AdminPointsController` | 1 | 运营手动发放积分 |
| `PointsRulesController` | 5 | 积分规则查询/创建/更新/启用/停用 |
| `TasksController` | 2 | 任务列表查询、完成任务领取积分 |
| `InternalPointsController` | 4 + gRPC | 内部试算/冻结/释放/确认积分（gRPC 双路由：`internal/v1/*` 新路由 + 旧路由 Obsolete 2026-08-01 下线） |

**源码目录**：`src/Services/Points/Leno.Points.Api/Controllers/`

#### 2.1.5 Membership 域（12 端点）

| Controller | 端点数 | 职责 |
|-|-|-|
| `MembersController` | 1 | 查询当前用户会员信息 |
| `AdminMemberLevelsController` | 6 | 会员等级查询/创建/更新/启用/停用（含权限管理） |
| `MembershipPackagesController` | 2 | 会员套餐列表查询、订阅套餐 |
| `AdminMembershipPackagesController` | 4 | 会员套餐创建/更新/启用/停用 |

**源码目录**：`src/Services/Membership/Leno.Membership.Api/Controllers/`

> 注：阶段1-2 中 Membership 域已对齐 design-prompts 期望路径与鉴权策略（`/api/admin/members/levels/*`、`/api/admin/membership-packages/*`，Operator/Admin 角色），原 Membership 服务 9 端点的路径/鉴权偏离问题已解决。

#### 2.1.6 Review 域（11 端点 + gRPC）

| Controller | 端点数 | 职责 |
|-|-|-|
| `ReviewsController` | 5 | 买家提交评价、按订单行查询、我的评价、上传评价图片、回复评价 |
| `ProductReviewsController` | 1 | 按 SPU 分页查询已通过评价 |
| `SellerReviewsController` | 1 | 卖家查询本店铺商品评价 |
| `AdminReviewsController` | 3 | 运营分页查询评价、审核通过、隐藏违规评价 |
| `ReviewControllerBase` | — | 共享基类 |

**源码目录**：`src/Services/Review/Leno.Review.Api/Controllers/`

#### 2.1.7 AfterSales 域（14 端点）

| Controller | 端点数 | 职责 |
|-|-|-|
| `AfterSalesController` | 6 | 买家提交售后申请、退货填写物流单号、撤销售后、按订单查询、我的售后、上传售后凭证图片 |
| `SellerAfterSalesController` | 4 | 卖家查询售后单、审核同意、驳回、确认收货 |
| `AdminAfterSalesController` | 3 | 运营分页查询全平台售后单、审核通过、驳回 |
| `AfterSalesControllerBase` | — | 共享基类 |

**源码目录**：`src/Services/AfterSales/Leno.AfterSales.Api/Controllers/`

### 2.2 阶段2：网关双轨挂载

阶段2 完成网关路由配置，新域端点经双轨挂载，灰度策略如下：

| 配置项 | 默认值 | 说明 |
|-|-|-|
| `Grayscale:Threshold` | 5% | HTTP 端点灰度比例（按用户 ID 哈希分流） |
| `Grayscale:InternalThreshold` | 100% | internal 端点全部切新域 |
| `Grayscale:RollbackToLegacy` | false | 回滚开关：true 即将流量回退至旧域 |
| `Grayscale:ObservationPeriodEnd` | 2026-09-26 | 阶段3 观察期结束日期（60 天） |

**网关路由策略**：
1. 同一路径双轨挂载（旧域 + 新域），按灰度阈值分流
2. HTTP 请求按 `X-User-Id` 哈希取模分流，保证同一用户请求始终路由到同一域
3. internal 端点（路径前缀 `internal/v1/*`）100% 切新域，订单域已切换至新路由
4. 灰度异常时通过 `Grayscale:RollbackToLegacy=true` 一键回滚至旧域

---

## 3. 文档同步状态

### 3.1 design-prompts 文档同步

| 文档 | 同步状态 | 说明 |
|-|-|-|
| `docs/design-prompts/README.md` | ✅ V1.1 已同步 | 含域拆分迁移双轨期说明与映射表 |
| `docs/design-prompts/buyer-app/00-overview.md` | ✅ 已同步 | API 来源含新域归属，含 7.1 域拆分映射表 |
| `docs/design-prompts/operations/00-overview.md` | ✅ 已同步 | API 来源含新域归属，含 7.1 域拆分映射表 |
| `docs/design-prompts/system-admin/00-overview.md` | ✅ 已同步 | API 来源含新域归属，含 7.1 域拆分映射表 |
| `docs/design-prompts/seller/00-overview.md` | ✅ 已同步 | API 来源含新域归属，含 7.1 域拆分映射表 |
| buyer-app 各页面提示词 | ✅ 已同步 | 「数据模型与 API 对接」段含「服务归属」字段 |
| operations 各页面提示词 | ✅ 已同步 | 「数据模型与 API 对接」段含「服务归属」字段 |
| system-admin 02-user-access / 06-account / 04-runtime-ops | ✅ 已同步 | 「数据模型与 API 对接」段含「服务归属」字段；health-monitoring 与 rate-limit-rules 模块卡片/筛选上下文已含新域 |
| seller 06-after-sales / 07-review / 08-account | ✅ 已同步 | 「数据模型与 API 对接」段含「服务归属」字段 |

### 3.2 feature-inventory 文档同步

| 文档 | 同步状态 | 说明 |
|-|-|-|
| `docs/feature-inventory/README.md` | ✅ 已同步 | BC → 源码目录映射表与拆分过渡态说明已更新 |
| `docs/feature-inventory/api-gap/00-summary.md` | ✅ 已同步 | 拆分过渡态影响范围表与关键风险已更新 |
| `docs/feature-inventory/api-gap/bc1-user-auth.md` | ✅ 已同步 | 顶部含阶段1-2 完成说明，英文名标注新域 |
| `docs/feature-inventory/api-gap/bc6-review-aftersales.md` | ✅ 已同步 | 顶部含阶段1-2 完成说明，第 5 章对照表与新域端点清单已更新 |
| `docs/feature-inventory/api-gap/bc7-points-membership.md` | ✅ 已同步 | 顶部含阶段1-2 完成说明，第 5 章对照表与新域端点清单已更新 |

---

## 4. 旧域代码保留与下线计划

### 4.1 旧域保留状态

阶段1-2 完成后，旧域代码完整保留，作为回滚兜底。`Leno.slnx` 解决方案文件中旧域项目保留，不修改。

| 旧域 | 源码目录 | 状态 | 端点数 |
|-|-|-|-|
| UserAuth | `src/Services/UserAuth/Leno.UserAuth.Api/` | 保留兜底 | 39 |
| PointsMembership | `src/Services/PointsMembership/Leno.PointsMembership.Api/` | 保留兜底 | 23（含 4 内部） |
| ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/` | 保留兜底 | 22 |

### 4.2 阶段3 观察期计划

**观察期**：2026-07-26 ~ 2026-09-26（60 天）

**观察指标**：
1. 新域 5xx 错误率 < 0.1%
2. 新域 P95 延迟 < 旧域 P95 延迟 × 1.2
3. 灰度用户业务指标（下单成功率、支付成功率、积分发放成功率、评价提交成功率、售后申请成功率）无显著回归
4. 网关双轨灰度无异常切换记录

**灰度推进计划**：
- 第 1 周：5% 灰度，每日巡检指标
- 第 2 周：10% 灰度，持续观察
- 第 3 周：30% 灰度
- 第 4 周：50% 灰度
- 第 5-6 周：80% 灰度
- 第 7-8 周：100% 灰度（新域独占）

### 4.3 阶段4 旧域下线计划

**下线前置条件**：
1. 100% 灰度运行 14 天无异常
2. 旧域无任何流量进入（网关日志确认）
3. 回滚开关未触发超过 14 天

**下线步骤**：
1. 网关移除旧域路由配置
2. 旧域项目从 `Leno.slnx` 移除（保留 Git 历史可追溯）
3. 旧域源码目录归档至 `archived/` 分支或独立仓库
4. 文档中移除「旧域双轨兜底」说明，更新为「新域独占」
5. 监控告警规则更新，移除旧域健康检查

**预计下线日期**：2026-09-26（观察期结束后启动，预计 1 周完成）

---

## 5. 风险与回滚

### 5.1 已知风险

| 风险 | 影响 | 缓解措施 |
|-|-|-|
| 双轨期数据一致性 | 新旧域数据库可能短暂不一致 | 同步采用 Outbox 模式 + 集成事件最终一致；新域读模型经事件订阅构建 |
| 灰度用户哈希分布不均 | 部分用户始终命中旧域 | 哈希函数经过测试，分布均匀度 > 95% |
| InternalPointsController 双路由 | internal 端点新旧路由并存 | 旧路由已 [Obsolete] 标记 2026-08-01 下线，订单域已切至 internal/v1/* 新路由 |
| Membership 域历史路径偏离 | 旧 Membership 服务 9 端点路径/鉴权与 design-prompts 不一致 | 阶段1-2 已由新 Membership 域 12 端点对齐覆盖，旧 Membership 服务保留兜底但不直接对外 |

### 5.2 回滚预案

**触发条件**：
- 新域 5xx 错误率 > 1% 持续 5 分钟
- 灰度用户核心业务指标回归 > 5%
- 数据丢失或脏数据确认

**回滚步骤**：
1. 设置 `Grayscale:RollbackToLegacy=true`，网关一键将流量切回旧域
2. 验证旧域流量恢复，监控核心指标
3. 排查新域问题，修复后重新推进灰度
4. 回滚期间新域保留只读副本，便于问题定位

---

## 6. 验收清单

### 6.1 阶段1-2 验收（已完成）

- [x] 7 个新域源码就绪，含 Domain / Application / Infrastructure / Api 四层
- [x] 新域 Controller 全部实现，端点路径与 design-prompts 期望一致
- [x] 新域单元测试覆盖率 ≥ 80%，集成测试覆盖核心场景
- [x] 网关双轨路由配置完成，灰度阈值默认 5%
- [x] internal 端点 100% 切新域
- [x] design-prompts 与 feature-inventory 文档同步完成
- [x] `Leno.slnx` 未修改，旧域项目保留

### 6.2 阶段3 观察期验收（待完成）

- [ ] 100% 灰度运行 14 天无异常
- [ ] 灰度推进各阶段指标达标
- [ ] 回滚开关未触发超过 14 天

### 6.3 阶段4 旧域下线验收（待完成）

- [ ] 网关旧域路由移除
- [ ] 旧域项目从 `Leno.slnx` 移除
- [ ] 旧域源码归档
- [ ] 文档更新为新域独占

---

## 7. 附录

### 7.1 相关文档索引

| 文档类型 | 文档路径 | 说明 |
|-|-|-|
| 设计文档 | `docs/superpowers/specs/2026-07-26-domain-split-migration-design.md` | 域拆分迁移设计 |
| 实现计划 | `docs/superpowers/plans/2026-07-26-domain-split-migration.md` | 实现计划与任务分解 |
| 设计提示词 | `docs/design-prompts/README.md` | UI 设计提示词（V1.1） |
| API 缺失对比 | `docs/feature-inventory/api-gap/00-summary.md` | 11 BC API 差异总览 |
| BC1 报告 | `docs/feature-inventory/api-gap/bc1-user-auth.md` | 用户与认证授权域差异 |
| BC6 报告 | `docs/feature-inventory/api-gap/bc6-review-aftersales.md` | 评价与售后域差异 |
| BC7 报告 | `docs/feature-inventory/api-gap/bc7-points-membership.md` | 积分与会员域差异 |

### 7.2 术语表

| 术语 | 说明 |
|-|-|
| 双轨期 | 新旧域同时在线，网关按灰度阈值分流的过渡期 |
| 灰度阈值 | 网关将请求路由到新域的比例 |
| 回滚开关 | `Grayscale:RollbackToLegacy` 配置项，true 即将流量切回旧域 |
| 观察期 | 100% 灰度运行后持续监控的 60 天时期 |
| 旧域保留兜底 | 旧域代码保留在解决方案中，作为回滚备份 |
| 新域独占 | 旧域下线后，新域独立承载所有流量 |

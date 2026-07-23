# 架构升级方案分阶段实施计划设计

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](./00-architecture-upgrade-plan.md) 第六章实施步骤
**目标**：为架构升级方案 4 个阶段（41 项任务）产出可执行级实施计划，每阶段独立文件，可直接驱动 subagent 执行
**方法**：方案 A（全阶段统一可执行级），严格前置依赖，本地构建验证

---

## 1. 范围与约束

### 1.1 实施范围

母方案第六章定义的 4 阶段 40 项任务（阶段二跳过 1 项已在阶段一完成的 P1-5）：

| 阶段 | 任务数 | 周期 | 健康度变化 |
|------|--------|------|-----------|
| 阶段一 P0 阻塞修复 | 4 项 P0（+ 3 项部分修复遗留合并处理） | 1 周 | 8.3 → 8.5 |
| 阶段二 速赢优化 | 14 项（母方案 15 步骤，跳过 1 项已在阶段一完成） | 2-3 周 | 8.5 → 8.8 |
| 阶段三 中期演进 | 12 项 | 1-2 月 | 8.8 → 9.3 |
| 阶段四 长期架构 | 10 项 | 3-6 月 | 9.3 → 9.6 |
| **合计** | **40 项** | — | **8.3 → 9.6** |

### 1.2 关键约束

- **每阶段独立文件**：4 个文件各自自包含完整实施计划，可独立审阅和执行
- **可执行级详细度**：包含 subagent 编排、波次划分、每任务修改范围+验收标准+依赖关系，可直接驱动 subagent 执行
- **严格前置依赖**：每阶段开篇设"前置依赖核验清单"，后阶段任务依赖前阶段产出，重叠任务在前阶段完成后跳过
- **本地构建验证**：subagent 写代码后 `dotnet build` / `dotnet test` 验证，失败则修复后提交（非 `[unverified]` 模式）
- **代码完整性**：遵循用户代码完整性强制契约——禁止占位符、TODO、空实现、截断输出，每函数完整实现

### 1.3 跨阶段依赖管理

严格前置依赖模型，重叠任务处理：

| 阶段 | 前置阶段 | 重叠任务处理 |
|------|---------|------------|
| 阶段一 | 无 | — |
| 阶段二 | 阶段一完成 | P1-5（MarkAllAsReadAsync）在阶段一已随 NEW-P0-4 完成，阶段二跳过该项 |
| 阶段三 | 阶段二完成 | 库存独立 BC 短期修复（OperationType）在阶段一已完成，阶段三仅做中期迁移 |
| 阶段四 | 阶段三完成 | 积分会员 BC 拆分在阶段三已启动，阶段四延续 |

### 1.4 验证策略统一标准

所有阶段采用本地构建验证：

- **编译验证**：`dotnet build` 零错误（W0 零警告目标）
- **单元测试**：`dotnet test` 全绿，新增/修改代码覆盖率 ≥ 80%
- **集成测试**：Testcontainers + MassTransit TestHarness（阶段三起强制）
- **commit 规范**：每任务独立 commit，message 格式 `[phase{N}][{BC}] {task-id}: {description}`

---

## 2. 文档结构设计

### 2.1 文件布局

4 个独立文件位于 `docs/superpowers/specs/2026-07-23-architecture-upgrade-plan/` 目录下，与母方案 `00-architecture-upgrade-plan.md` 并列：

```
docs/superpowers/specs/2026-07-23-architecture-upgrade-plan/
├── 00-architecture-upgrade-plan.md          # 已有母方案（不修改）
├── implementation-plans-design.md           # 本文件（设计文档）
├── 01-phase1-p0-fixes.md                    # 阶段一：P0 阻塞修复（4 任务）
├── 02-phase2-quick-wins.md                  # 阶段二：速赢优化（14 任务）
├── 03-phase3-mid-term.md                    # 阶段三：中期演进（12 任务）
└── 04-phase4-long-term.md                   # 阶段四：长期架构（10 任务）
```

### 2.2 统一文档模板

每个阶段文件采用统一结构（与 `2026-07-22-fix-execution-design.md` 风格一致，增强依赖与验证部分）：

```markdown
# {阶段名称} 实施计划

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](母方案) 第 X 章
**前置依赖**：{前一阶段文件或"无"}
**目标**：{阶段目标 + 健康度变化}

## 1. 范围与约束
  1.1 实施范围（任务总数/分类表）
  1.2 关键约束（环境/并行度/冲突避免/代码完整性）
  1.3 前置依赖核验清单（逐项确认前置阶段产出已就绪）

## 2. 总体架构
  - 波次编排图（ASCII）
  - subagent 总数与分批策略
  - BC 目录互斥矩阵

## 3-N. 每波次详细编排
  - P0/P1 项清单表（问题 ID / 修改范围 / 影响 BC / 验收标准）
  - subagent 指令要点
  - 并行/串行决策

## N+1. 验证策略
  - 本地构建验证流程（dotnet build / dotnet test）
  - 每任务验收标准汇总
  - 回归测试范围

## N+2. 风险与回滚
  - 高风险项识别
  - 回滚预案（每高风险项）
  - 双轨期策略（如适用）

## N+3. 决策门（仅阶段三/四）
  - 执行前需重新评估的前置条件
  - 环境假设清单
  - 修订触发条件
```

---

## 3. 阶段一实施计划设计（P0 阻塞修复）

### 3.1 范围

**目标**：解除部署阻塞，恢复全域一致性。健康度 8.3 → 8.5。
**任务数**：4 项 P0 + 3 项部分修复遗留（合并处理）
**前置依赖**：无
**预估周期**：1 周

### 3.2 波次编排

4 项 P0 修改范围互斥（Order / Cart / Order / Notification），可完全并行。但 NEW-P0-1 与 NEW-P0-3 均涉及 Order BC 数据库迁移，需串行以避免迁移冲突。

```
Wave 1a（3 并行）          Wave 1b（1 串行，依赖 1a 完成）
┌──────────┬──────────┬──────────┐  ┌──────────┐
│Task 1    │Task 2    │Task 4    │  │Task 3    │
│NEW-P0-1  │NEW-P0-2  │NEW-P0-4  │  │NEW-P0-3  │
│Order     │Cart      │Notificat │  │Order     │
│Config+迁移│UnitOfWork│Repository│  │补偿表+迁移│
└──────────┴──────────┴──────────┘  └──────────┘
   ↓ git commit              ↓ git commit
   ─────────────────────────→ 1b 启动
```

**冲突规避**：Task 1（NEW-P0-1）与 Task 3（NEW-P0-3）均修改 Order BC 数据库迁移。为避免迁移文件冲突，Task 3 在 Task 1 提交后启动（Wave 1b 串行等待，实际为 3 并行 + 1 串行）。Task 2（Cart）、Task 4（Notification）与其他任务无冲突，在 Wave 1a 并行。

### 3.3 任务清单（4 项）

| Task | 问题 ID | 修改范围 | 影响 BC | 验收标准 | 波次 |
|------|---------|---------|---------|---------|------|
| 1 | NEW-P0-1 | `OrderConfiguration.cs` 删除显式 `row_version` 配置或 `BaseDbContext` 删除 shadow `version` 列；新增迁移 DropColumn | Order | SQL Server 迁移成功，单 rowversion 列，`dotnet build` 通过 | 1a |
| 2 | NEW-P0-2 | `CartUnitOfWork.cs` 改委托 `SaveChangesWithOutboxAsync`，移除 `SaveChangesAsync` 旁路 | Cart | Cart 领域事件经 Outbox 投递，集成测试验证事件到达 | 1a |
| 3 | NEW-P0-3 | `StockReservationCompensation` 实体增 `OperationType` 枚举字段；`StockReservationCompensationBackgroundService` 按类型分发 `ReleaseAsync`/`ReturnDeductedAsync`；新增迁移 AddColumn | Order | 补偿按类型调用正确方法，ForceCancel 已支付订单的 ReturnDeducted 成功 | 1b（依赖 Task 1） |
| 4 | NEW-P0-4 | `EfCoreNotificationRecordRepository.MarkAllAsReadAsync` 改为加载聚合根逐个标记 + `SaveEntitiesAsync` | Notification | 触发 `NotificationReadDomainEvent`，写审计字段（UpdatedAt/UpdatedBy） | 1a |

### 3.4 部分修复遗留项处理

母方案 2.2.2 节标注的 3 项"部分修复"中：
- **Cart 聚合缺乐观锁**（shadow property 隐式生效）：阶段一不处理，阶段二 P1-1 统一处理
- **Order Saga 补偿失败**：即 NEW-P0-3，Task 3 已覆盖
- **各 BC Outbox 旁路**：即 NEW-P0-2，Task 2 已覆盖（Cart 是最后一个旁路的 BC）

### 3.5 验证策略

- 每任务独立 commit，`[phase1][{BC}] {task-id}: {description}`
- `dotnet build` 零错误
- 单元测试：每项新增/修改方法覆盖率 ≥ 80%
- 集成测试：Task 2/3/4 需验证领域事件经 Outbox 投递
- DBA 评审：Task 1/3 迁移脚本（Down 方法必须配套）

### 3.6 风险与回滚

| 风险 | 缓解 |
|------|------|
| Task 1 DropColumn `version` 误删业务数据 | 迁移前备份；Down 方法 AddColumn 恢复 |
| Task 3 迁移与 Task 1 冲突 | Task 3 串行等待 Task 1 完成后启动（Wave 1b） |
| Task 2 Outbox 引入性能开销 | 压测对比；保留旁路代码注释以便快速回退 |

---

## 4. 阶段二实施计划设计（速赢优化）

### 4.1 范围

**目标**：清理重复实现，配置化抽取，索引补充，修复 P1 高优先级问题。健康度 8.5 → 8.8。
**任务数**：15 项（母方案 6.2 节步骤 1-15，其中步骤 10 P1-5 已在阶段一完成，实际 14 项）
**前置依赖**：阶段一全部完成（NEW-P0-4 即 P1-5 已完成）
**预估周期**：2-3 周

### 4.2 任务分类与互斥分析

15 项任务按修改区域分为 4 组，组内可并行，组间无冲突：

| 组别 | 任务 | 修改区域 | 互斥性 |
|------|------|---------|--------|
| **G1 共享层去重** | 步骤 1（RateLimiter）、2（TraceIdEnricher）、3（AuditableInterceptor） | `BuildingBlocks/Leno.Infrastructure/` + `ApiGateway/` | 组内串行（同目录） |
| **G2 性能优化** | 步骤 4（BloomFilter Lua）、5（NotificationDispatcher SaveChanges） | `Leno.Infrastructure/Caching/` + `Notification.Infrastructure/` | 并行（不同 BC） |
| **G3 数据库索引** | 步骤 6（reviews.seller_id）、7（notification_records 复合） | `ReviewAfterSales.Infrastructure/` + `Notification.Infrastructure/` Configuration + 迁移 | 并行 |
| **G4 P1 修复** | 步骤 8（P1-1 Cart）、9（P1-4 Payment）、11（P1-7 配置化）、12（P1-9 JwtTTL）、13（P1-10 Promotion）、14（死代码清理）、15（Outbox 归档） | Cart/Payment/Notification/UserAuth/Promotion/SellerShop | 并行（不同 BC） |

**跳过项**：步骤 10（P1-5 MarkAllAsReadAsync）已在阶段一 Task 4（NEW-P0-4）完成。

### 4.3 波次编排

```
Wave 1（4 并行 subagent）           Wave 2（4 并行 subagent）
┌──────────┬──────────┬──────────┬──────────┐  ┌──────────┬──────────┬──────────┬──────────┐
│G1 共享层 │G3 索引   │G4-P1 Cart│G4-P1 Pay │  │G2 性能优 │G4-P1 配置│G4-P1 死代│G4-P1 Out │
│去重串行  │review+not│P1-1 匿名 │P1-4 支付 │  │化 BloomF │化 P1-7/  │码清理    │box 归档  │
│3 任务    │ification │购物车    │单卡Pending│  │+Dispatc  │P1-9/P1-10│14项      │15项      │
│Infra+GW  │_records  │Cart BC   │Payment BC│  │her       │3 BC      │多 BC     │SystemAdm │
└──────────┴──────────┴──────────┴──────────┘  └──────────┴──────────┴──────────┴──────────┘
   ↓ 4 commits                            ↓ 4 commits
   ─────────────────────────────────────────→ Wave 2 启动
```

**波次划分依据**：
- Wave 1：修改基础设施层 + 独立 BC，不依赖其他任务产出
- Wave 2：G2 依赖 G1 去重后的共享层；G4 配置化依赖 G1 的 `IOptionsMonitor` 模式确立

**G1 串行说明**：G1 共享层去重的 3 个任务（2.1.1/2.1.2/2.1.3）修改同一目录 `BuildingBlocks/Leno.Infrastructure/`，在同一 subagent 内串行执行（先去重 RateLimiter，再合并 TraceIdEnricher，最后清理 AuditableInterceptor），对外表现为 1 个并行槽位。

**subagent 总数**：8 个（2 波 × 4 并行）

### 4.4 任务清单（14 项，跳过步骤 10）

| Task | 步骤 | 问题 ID | 修改范围 | 验收标准 |
|------|------|---------|---------|---------|
| 2.1.1 | 1 | — | 删除 `ApiGateway/Services/RedisSlidingWindowRateLimiter.cs`，引用 `Leno.Infrastructure` 实现 | 双份消除，ApiGateway 测试通过 |
| 2.1.2 | 2 | — | 合并 `TraceIdEnricher` 与 `OpenTelemetryTraceIdEnricher` 为单一实现 | 日志 TraceId 一致，OTel 关联不变 |
| 2.1.3 | 3 | — | 删除 `BaseDbContext.FillAuditableFields`，保留 `AuditableEntityInterceptor` | 审计字段正常写入，测试覆盖 |
| 2.2.1 | 4 | — | `RedisBloomFilter` 改用 Lua 脚本一次 `EVAL` 替代 7 次串行 `StringGetBitAsync` | 单次网络往返，单元测试验证位图一致性 |
| 2.2.2 | 5 | — | `NotificationDispatcher` 先创建所有渠道记录并 Add，单次 `SaveChanges` | SaveChanges 次数从 2N 降至 1，集成测试验证记录完整 |
| 2.3.1 | 6 | P1-6 | `ReviewConfiguration` 增加 `ix_reviews_seller_id` 索引 + 迁移 | 索引存在，查询计划走索引 |
| 2.3.2 | 7 | — | `NotificationRecordConfiguration` 增加 `ix_notification_records_user_isread_channel` 复合索引 + 迁移 | 复合索引存在，查询计划验证 |
| 2.4.1 | 8 | P1-1 | `RedisAnonymousCartRepository.SaveAsync` 改用 Lua 脚本原子更新（CAS 模式） | 并发更新无覆盖写，集成测试模拟并发 |
| 2.4.2 | 9 | P1-4 | `PaymentRequestedEventConsumer` 支付单 Pending 时允许重新发起（状态检查 + 幂等） | 用户可重新发起支付，不产生重复支付单 |
| 2.4.3 | 11 | P1-7 | 抽取 `RetryPolicyOptions` / `RateLimitOptions` 走 `IOptionsMonitor<>`，按 templateCode 维度配置 | 配置缺省值与 const 对齐，零行为变更 |
| 2.4.4 | 12 | P1-9 | `JwtRevocationService.UserBlacklistTtl` 与 JWT 实际有效期联动 | TTL 动态计算，测试验证 |
| 2.4.5 | 13 | P1-10 | `PromotionCalculateAppService.GetByUserAsync` 下推 `ExpiredAt > now` 到 SQL | 内存过滤消除，查询计划验证 |
| 2.4.6 | 14 | — | 删除 `NotificationEventConsumer` `[Obsolete]` + `SellerDashboardAppService` 双轨下线 | 确认无测试引用后删除，编译通过 |
| 2.4.7 | 15 | — | Outbox 表 7 天归档策略（定时任务清理已处理记录） | 归档任务运行，表行数下降 |

### 4.5 验证策略

- 每任务独立 commit：`[phase2][{BC}] {step-id}: {description}`
- `dotnet build` 零错误零警告
- **去重任务（G1）**：回归测试覆盖 ApiGateway 全路由 + 审计字段写入
- **配置化任务（2.4.3/2.4.4）**：缺省值对齐测试（与现有 const 行为一致）
- **索引任务（G3）**：DBA 评审迁移脚本 + 查询计划验证
- **并发任务（2.4.1）**：集成测试模拟并发场景（Testcontainers Redis）

### 4.6 风险与回滚

| 风险 | 缓解 |
|------|------|
| G1 去重破坏 ApiGateway 限流 | 保留旧实现注释 1 个版本周期；灰度发布 |
| G3 索引迁移锁表 | 在线创建索引 `WITH (ONLINE = ON)`；低峰执行 |
| 2.4.1 Cart Lua 脚本复杂度高 | 先单元测试 Lua 逻辑，再集成测试；保留旧 SaveAsync 作为 fallback |
| 2.4.3 配置化改变缺省行为 | 缺省值与 const 完全对齐测试；零行为变更门禁 |

---

## 5. 阶段三实施计划设计（中期演进）

### 5.1 范围

**目标**：BC 拆分、Saga 状态机、规则引擎、安全技术栈升级。健康度 8.8 → 9.3。
**任务数**：12 项（母方案 6.3 节步骤 1-12）
**前置依赖**：阶段二全部完成（P1 清零、重复实现清零、配置化覆盖 90%）
**预估周期**：1-2 月

### 5.2 任务依赖图

```
步骤1 库存独立BC ───┐ (短期修复已在阶段一完成，此处仅中期迁移)
                    ▼
步骤2 MassTransit Saga状态机 ────┐ (依赖库存BC接口稳定)
                                 ▼
步骤3 Process Manager模式 ────────┘ (依赖Saga状态机基础设施)

步骤4 促销规则引擎 ──────────────── (独立)
步骤5 评价与售后BC拆分 ───────────── (独立)
步骤6 AuthN/AuthZ BC拆分 ──────────┐
                                   ▼
步骤7 OAuth/SSO通用化 ─────────────┘ (依赖步骤6，AuthN拆分后)
步骤8 支付渠道插件化 ──────────────── (独立)
步骤9 通知中心渠道注册表 ──────────── (独立)
步骤10 安全技术栈升级 ─────────────── (独立，但需全BC协调)
步骤11 Cart SKU快照本地化 ─────────── (独立)
步骤12 CQRS读模型snapshot+replay ──── (独立)
```

### 5.3 波次编排（4 波）

```
Wave 1（4 并行）              Wave 2（3 并行）              Wave 3（3 并行）              Wave 4（2 并行）
┌──────────┬──────────┐      ┌──────────┬──────────┐      ┌──────────┬──────────┐      ┌──────────┬──────────┐
│步骤1     │步骤5     │      │步骤2     │步骤6     │      │步骤3     │步骤7     │      │步骤10    │步骤12    │
│库存BC    │评价售后  │ ───► │Saga状态  │AuthN/    │ ───► │Process   │OAuth/SSO │ ───► │安全技术  │CQRS读模  │
│迁移      │BC拆分    │      │机        │AuthZ拆分 │      │Manager   │通用化    │      │栈升级    │型replay │
├──────────┤          │      ├──────────┤          │      ├──────────┤          │      └──────────┴──────────┘
│步骤4     │          │      │步骤8     │          │      │步骤9     │          │
│促销规则  │          │      │支付渠道  │          │      │通知渠道  │          │
│引擎      │          │      │插件化    │          │      │注册表    │          │
├──────────┤          │      └──────────┴──────────┘      └──────────┴──────────┘
│步骤11    │          │
│Cart SKU  │          │
│快照      │          │
└──────────┴──────────┘
```

**波次划分依据**（严格遵循依赖图）：
- Wave 1（4 并行）：步骤1（库存BC）、步骤4（促销规则）、步骤5（评价售后）、步骤11（Cart快照）—— 全独立无依赖
- Wave 2（3 并行）：步骤2（Saga，依赖步骤1库存BC接口）、步骤6（AuthN/AuthZ拆分）、步骤8（支付插件，独立）
- Wave 3（3 并行）：步骤3（Process Manager，依赖步骤2 Saga）、步骤7（OAuth/SSO，依赖步骤6 AuthN拆分）、步骤9（通知渠道，独立）
- Wave 4（2 并行）：步骤10（安全技术栈，需全BC协调放最后）、步骤12（CQRS读模型，独立）

**波次依赖**：
- Wave 1 → Wave 2：步骤2 依赖步骤1 库存 BC 接口
- Wave 2 → Wave 3：步骤3 依赖步骤2 Saga 基础设施；步骤7 依赖步骤6 AuthN 拆分
- Wave 3 → Wave 4：步骤10 需全 BC 协调，放最后避免冲突

**subagent 总数**：12 个（4 波，每波 2-4 并行）

### 5.4 任务清单（12 项）

| Task | 步骤 | 任务 | 周期 | 兼容性风险 | 验收标准 |
|------|------|------|------|-----------|---------|
| 3.1 | 1 | 库存独立 BC 中期迁移：`StockReservation` 聚合迁移至 `Leno.Inventory.Domain`，Order BC 通过集成事件调用 | 6 周 | 高 | 库存真源单一化，三 BC 库存数据迁移完成，Redis 双写对账 SLA 监控上线 |
| 3.2 | 2 | MassTransit Saga 状态机：`SagaStateMachine<OrderSagaState>`，状态持久化到 `order_saga_states` 表 | 6 周 | 高 | 崩溃恢复 100%，状态流转 `Pending→StockReserved→PointsFrozen→OrderCreated→Completed/Compensating` 验证 |
| 3.3 | 3 | Process Manager 模式：`OrderPaymentProcessManager` 编排 `MarkOrderPaid`/`ConfirmStock`/`ConfirmPoints` | 4 周 | 中 | 跨进程操作全局协调，中间态可观测，失败自动补偿 |
| 3.4 | 4 | 促销规则引擎：`IPromotionRule` + `PromotionRuleContext`，规则配置 JSON 化 | 4 周 | 中 | 新规则零侵入扩展，旧规则迁移到新引擎，A/B 测试支持 |
| 3.5 | 5 | 评价与售后 BC 拆分：`Review` BC + `AfterSales` BC，`eligibilityChecker` 拆为两套 | 4 周 | 中 | 评价读模型独立 ES 索引重建，售后状态机独立演进 |
| 3.6 | 6 | AuthN/AuthZ BC 拆分：`Identity` BC（认证）+ `AccessControl` BC（授权），`CheckPermission` RPC | 6 周 | 中 | 认证授权独立部署，`User.Roles` 迁移到 AccessControl，JWT claim `role` 向后兼容 |
| 3.7 | 7 | OAuth/SSO 通用化：`IOAuth2ProviderAdapter` 通用 OIDC 适配器，配置驱动 | 3 周 | 低 | 新 provider 零代码接入，OIDC claim 映射标准化 |
| 3.8 | 8 | 支付渠道插件化：`IEnumerable<IPaymentChannelAdapter>` 注入 + `Assembly.Load` 加载 | 3 周 | 低 | 新增银联/Apple Pay 从"改3处代码+发版"降为"实现适配器+注册DI+配置启用" |
| 3.9 | 9 | 通知中心渠道注册表：`INotificationChannelRegistry` + 渠道元数据 + 能力声明 | 3 周 | 中 | 新增渠道（Push/IM/Webhook）零侵入核心调度，偏好存储迁移完成 |
| 3.10 | 10 | 安全技术栈升级：Argon2id + PEPPER / RS256 非对称签名 / KMS 托管 + KeyId 版本化 | 4 周 | 中 | 密码哈希迁移 Argon2id，JWT 双签名过渡，KMS 集成验证 |
| 3.11 | 11 | Cart SKU 快照本地化：Cart 聚合存储 SKU 快照，减少实时跨进程调用 | 3 周 | 中 | `CartPriceService` 实时跨进程调用消除，快照一致性验证 |
| 3.12 | 12 | CQRS 读模型 snapshot + incremental replay：ES 投影支持快照重建 | 4 周 | 中 | 读模型重建速度提升，增量回放验证 |

### 5.5 双轨期策略（高风险项）

| 场景 | 双轨期 | 切换机制 |
|------|--------|---------|
| Saga 状态机 vs 进程内编排 | 4 周 | feature flag 按 OrderId 哈希切流 |
| BC 拆分（库存/评价售后/AuthN-AuthZ） | 8 周 | 事件类型双写 + 灰度按 BC 切流 |
| HS256 → RS256 | 4 周 | 双签名过渡 + 公钥分发 |
| Process Manager vs 三消费者 | 4 周 | feature flag 按 OrderId 切流，双轨期保证幂等 |

### 5.6 验证策略

- 每任务独立 commit：`[phase3][{BC}] {task-id}: {description}`
- `dotnet build` 零错误零警告
- **BC 拆分任务**：跨 BC 集成测试（Testcontainers + MassTransit TestHarness）强制
- **Saga 状态机**：混沌工程测试（故障注入验证崩溃恢复）
- **安全升级**：密码哈希迁移测试（旧 bcrypt → Argon2id 平滑过渡）+ JWT 双签名验证
- **性能基准**：每任务配套性能基准测试，对比基线确保不退化

### 5.7 决策门（阶段三特有）

执行前需重新评估的前置条件：

| 决策门 | 评估内容 | 修订触发条件 |
|--------|---------|------------|
| DG-1 | 库存 BC 迁移：阶段一 NEW-P0-3 修复是否已生产验证 | 补偿表 OperationType 在生产环境运行 ≥ 2 周无异常 |
| DG-2 | Saga 状态机：阶段二 Outbox 归档策略是否生效 | Outbox 表行数稳定，无膨胀 |
| DG-3 | BC 拆分：团队是否有足够人力（5 后端 + 1 架构师） | 资源不足时推迟或分批 |
| DG-4 | 安全升级：KMS 基础设施是否就绪 | Azure Key Vault / AWS KMS 可用 |
| DG-5 | 评价售后拆分：ES 集群是否扩容完成 | ES 容量满足双 BC 读模型需求 |

### 5.8 风险与回滚

| 风险 | 缓解 |
|------|------|
| BC 拆分数据迁移失败 | 双轨期并行 + 灰度按事件类型切流 + 回滚预案 |
| Saga 状态机迁移在途订单丢失 | 先持久化 Saga 状态（不引入状态机）+ 状态对账脚本 |
| 库存独立 BC 迁移期间超卖 | 双写过渡 + Redis 库存对账 SLA 监控 |
| 安全升级破坏现有认证 | HS256/RS256 双签名过渡 4 周 + KMS 失败回退 appsettings.json |
| Process Manager 与现有消费者双轨冲突 | feature flag 切流 + 幂等保证 + 双轨期监控 |

---

## 6. 阶段四实施计划设计（长期架构）

### 6.1 范围

**目标**：Infrastructure 拆包、多级缓存、多租户/国际化预留、持续优化。健康度 9.3 → 9.6（L5 持续优化达成）。
**任务数**：10 项（母方案 6.4 节步骤 1-10）
**前置依赖**：阶段三全部完成（BC 拆分完成、Saga 状态机上线、安全升级完成）
**预估周期**：3-6 月

### 6.2 任务依赖图

```
步骤1 Infrastructure模块化拆包 ──────────────────────────┐
   (9 子包 + 元包门面，为后续任务提供基础)                │
                                                         ▼
步骤2 ACL防腐层可插拔策略链 ──────┐    步骤3 BFF聚合层DAG编排引擎 ────┐
   (依赖拆包后的 AntiCorruption │       (依赖拆包后的 ApiGateway)     │
    子包)                       │                                    │
                                 ▼                                    ▼
步骤4 Outbox分片发布器 ────────────────────────────────────────────────┤
   (独立，依赖拆包后的 Persistence 子包)                                │
                                                                       ▼
步骤5 多级缓存L1+L2 ──────────────────────────────────────────────────┤
   (依赖拆包后的 Caching 子包)                                          │
                                                                       ▼
步骤6 Consul配置Schema版本化 ──────────────────────────────────────────┤
   (独立)                                                               │
                                                                       ▼
步骤7 多租户预留 ──────────┐  步骤8 国际化预留 ──────────┐  步骤9 积分会员BC拆分 ──┐
   (高风险，领域模型扩展位) │     (高风险，模板多语言)     │     (阶段三延续，独立)  │
                          ▼                             ▼                        ▼
                          步骤10 跨BC契约评审机制 + Pact契约测试（持续，贯穿全阶段）
```

### 6.3 波次编排（3 波 + 1 持续）

```
Wave 1（1 串行，基础设施）  Wave 2（3 并行）          Wave 3（3 并行）
┌──────────────────┐       ┌──────────┬──────────┬──────────┐  ┌──────────┬──────────┬──────────┐
│步骤1             │       │步骤2     │步骤3     │步骤4     │  │步骤7     │步骤8     │步骤9     │
│Infrastructure    │ ────► │ACL策略链 │BFF DAG   │Outbox分片│  │多租户预留│国际化预留│积分会员BC│
│模块化拆包        │       │引擎      │编排      │发布器    │  │          │          │拆分      │
│9子包+元包门面    │       │4周       │6周       │4周       │  │4周       │4周       │6周       │
│6周               │       └──────────┴──────────┴──────────┘  └──────────┴──────────┴──────────┘
└──────────────────┘                ↓                              ↓
   ↓ git commit                 ──────────────────────────────────→ Wave 3 启动
                              ┌──────────┐
                              │步骤5     │  (依赖 Wave 2 Caching 子包)
                              │多级缓存  │  Wave 2 完成后启动，与 Wave 3 并行
                              │L1+L2     │
                              │4周       │
                              └──────────┘
                              ┌──────────────────────────────────────────┐
                              │步骤6 Consul配置Schema版本化（独立，任意时机）│
                              │3周                                        │
                              └──────────────────────────────────────────┘
                              ┌──────────────────────────────────────────┐
                              │步骤10 跨BC契约评审 + Pact契约测试（持续）  │
                              │贯穿全阶段，从 Wave 1 开始                  │
                              └──────────────────────────────────────────┘
```

**波次依赖**：
- Wave 1 → Wave 2：步骤 2/3/4 依赖拆包后的子包
- Wave 2 → 步骤 5：多级缓存依赖 Caching 子包
- Wave 3：步骤 7/8/9 相互独立，可与步骤 5/6 并行

**subagent 总数**：10 个（Wave 1 串行 1 个 + Wave 2 并行 3 个 + Wave 3 并行 3 个 + 步骤 5/6/10 各 1 个穿插）

### 6.4 任务清单（10 项）

| Task | 步骤 | 任务 | 周期 | 兼容性风险 | 验收标准 |
|------|------|------|------|-----------|---------|
| 4.1 | 1 | Infrastructure 模块化拆包：9 子包（Abstractions/Caching/EventBus/AntiCorruption/Persistence/Telemetry/RateLimiting/Auth/ReadModel）+ 元包门面 | 6 周 | 低 | 按需引用，启动加速 30%+，元包向后兼容门面可用 |
| 4.2 | 2 | ACL 防腐层可插拔策略链：`IAclChannel` 接口 + 有序 channel 列表 + 优先级熔断选择 | 4 周 | 中 | 新协议接入零侵入，gRPC/HTTP 双轨迁移完成 |
| 4.3 | 3 | BFF 聚合层 DAG 编排引擎：声明式 `AggregateBuilder` + 拓扑排序 + 并行调度 + 超时级联 | 6 周 | 低 | 复杂聚合场景声明式表达，`Parallel.ForEachAsync` 作为特例保留 |
| 4.4 | 4 | Outbox 分片发布器：按聚合根 ID 哈希分片 + `SELECT ... FOR UPDATE SKIP LOCKED` | 4 周 | 中 | 发布吞吐随实例数线性扩展，无损水平扩展 |
| 4.5 | 5 | 多级缓存 L1 Local + L2 Redis：`IMemoryCache` L1（短 TTL）+ Redis L2 + Pub/Sub 跨实例失效 | 4 周 | 中 | 热点 Key Redis QPS 下降 80%+，L1 跨实例失效验证 |
| 4.6 | 6 | Consul 配置 Schema 版本化与灰度发布 | 3 周 | 中 | 配置变更版本化，灰度发布机制可用 |
| 4.7 | 7 | 多租户预留：聚合根 + EF Configuration 增 `tenant_id` + 全局查询过滤器（仅预留扩展位，不实际落地） | 4 周 | 高 | 领域模型扩展位就绪，业务驱动时可直接落地 |
| 4.8 | 8 | 国际化预留：通知模板多语言变体 + 错误码本地化资源 + `IStringLocalizer`（仅预留，不实际落地） | 4 周 | 高 | 国际化扩展位就绪，模板 `Culture` 维度支持 |
| 4.9 | 9 | 积分会员 BC 拆分：`Points` BC + `Membership` BC，经 `PointsEarnedEvent`/`MemberLevelChangedEvent` 协作 | 6 周 | 高 | 独立伸缩（积分高频写、会员低频评估），故障隔离，数据库拆分完成 |
| 4.10 | 10 | 跨 BC 契约评审机制 + Pact 契约测试（持续，贯穿全阶段） | 持续 | 中 | Pact Broker 上线，跨 BC 调用契约锁定，breaking 变更自动检测 |

### 6.5 双轨期策略

| 场景 | 双轨期 | 切换机制 |
|------|--------|---------|
| Infrastructure 拆包 | 12 周 | 元包门面始终可用 + 子包按需迁移 |
| gRPC int64 → string（遗留） | 12 周 | deprecated 标注 + 客户端逐步升级 |
| 积分会员 BC 拆分 | 8 周 | 事件类型双写 + 灰度按 BC 切流 |
| 多级缓存 L1+L2 | 4 周 | feature flag 按 Key 前缀切流，L1 失效回源 L2 |
| ACL 策略链 vs 双轨调度 | 4 周 | feature flag 按 BC 切流 |

### 6.6 验证策略

- 每任务独立 commit：`[phase4][{Module/BC}] {task-id}: {description}`
- `dotnet build` 零错误零警告
- **Infrastructure 拆包**：依赖图分析工具验证无循环依赖；元包门面全 BC 编译通过
- **多级缓存**：性能基准测试（热点 Key QPS 对比基线下降 80%+）；L1 跨实例失效集成测试
- **Outbox 分片**：多实例发布吞吐压测（线性扩展验证）；`SKIP LOCKED` 正确性测试
- **多租户/国际化预留**：扩展位单元测试（验证 `tenant_id` 列存在、`Culture` 维度支持），不实际落地
- **Pact 契约测试**：跨 BC 契约覆盖率 ≥ 90%，CI 集成 breaking 变更检测

### 6.7 决策门（阶段四特有）

| 决策门 | 评估内容 | 修订触发条件 |
|--------|---------|------------|
| DG-6 | Infrastructure 拆包：阶段三 BC 拆分是否完全稳定 | BC 拆分后生产运行 ≥ 1 月无 P0/P1 问题 |
| DG-7 | 多租户预留：业务是否有 SaaS 多租户需求确认 | 业务方确认需求后再实际落地，否则仅保留扩展位 |
| DG-8 | 国际化预留：业务是否有海外扩展计划 | 业务方确认海外计划后再实际落地 |
| DG-9 | 积分会员 BC 拆分：阶段三 AuthN/AuthZ 拆分经验复盘 | 复盘拆分模式，优化迁移策略 |
| DG-10 | Pact 契约测试：团队是否完成 Pact 培训 | 培训完成后再强制 CI 集成 |

### 6.8 风险与回滚

| 风险 | 缓解 |
|------|------|
| Infrastructure 拆包引发依赖循环 | 分批迁移 + 依赖图分析工具 + 元包门面兜底 |
| 多租户/国际化预留过度设计 | 业务驱动原则，仅预留扩展位不实际落地 |
| 多级缓存 L1 跨实例失效失败 | Pub/Sub 通道监控 + L1 TTL 短（5s）兜底 |
| 积分会员 BC 拆分数据迁移失败 | 双轨期并行 + 灰度按事件类型切流 + 回滚预案 |
| Outbox 分片 `SKIP LOCKED` 兼容性 | SQL Server 2016+ 验证；回退单实例发布器 |
| Pact 契约测试增加 CI 耗时 | 按需运行（PR 触发）+ 契约缓存 |

### 6.9 持续优化（L5 达成）

阶段四完成后进入 L5 持续优化模式：

- **健康度监控**：每季度复评 BC 健康度矩阵，目标维持 ≥ 9.6
- **技术债看板**：SystemAdmin 审计日志跟踪架构决策，新增技术债入看板
- **契约稳定性**：Pact 契约测试持续运行，breaking 变更需架构评审
- **性能基线**：每季度性能基准对比，退化超 5% 触发优化任务

---

## 7. 实施总结

### 7.1 subagent 编排总览

| 阶段 | 波次数 | subagent 总数 | 每波并行度 | 总周期 |
|------|--------|--------------|-----------|--------|
| 阶段一 | 2 波（1a 并行 + 1b 串行） | 4 | 3 + 1 | 1 周 |
| 阶段二 | 2 波 | 8 | 4 + 4 | 2-3 周 |
| 阶段三 | 4 波 | 12 | 4 + 3 + 3 + 2 | 1-2 月 |
| 阶段四 | 3 波 + 持续 | 10 | 1 + 3 + 3 + 穿插 3 | 3-6 月 |
| **合计** | **11 波 + 持续** | **34** | — | **~6-9 月** |

### 7.2 决策门汇总

| 阶段 | 决策门数 | 关键决策门 |
|------|---------|-----------|
| 阶段三 | 5 | DG-1（库存 BC 生产验证）、DG-3（人力）、DG-4（KMS 就绪） |
| 阶段四 | 5 | DG-6（BC 拆分稳定）、DG-7/DG-8（业务需求确认）、DG-10（Pact 培训） |

### 7.3 量化预期

| 指标 | 阶段一后 | 阶段二后 | 阶段三后 | 阶段四后 |
|------|---------|---------|---------|---------|
| 健康度 | 8.5 | 8.8 | 9.3 | 9.6 |
| P0 阻塞 | 0 | 0 | 0 | 0 |
| P1 问题 | 10 | 0 | 0 | 0 |
| 重复实现 | 4 | 0 | 0 | 0 |
| 配置化覆盖率 | 60% | 90% | 95% | 98% |
| Saga 崩溃恢复 | 0% | 0% | 100% | 100% |
| L5 达成 | ❌ | ❌ | ❌ | ✅ |

---

**设计文档完成**

本设计为架构升级方案 4 阶段 40 项任务定义了统一可执行级实施计划框架，每阶段独立文件，严格前置依赖，本地构建验证，配套波次编排、决策门、风险回滚机制。

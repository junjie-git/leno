# 双轨下线批次 1+2 收口与交易闭环补齐 —— 设计文档

> 日期：2026-10-16 · 状态：**已获用户批准，待实施**
> 前置文档：`docs/双轨下线-最优收尾方案.md`（2026-09-23，无约束版收尾设计）、`docs/双轨下线-实施方案.md` §7（权威状态）、`docs/库存目标架构-无约束最优方案.md` §8（库存收尾，已完成）
> 本文档回答：批次 1（机械收敛）+ 批次 2（四开关切流）+ 交易闭环 7 项业务任务怎么收。

---

## 0. 决策记录（用户已确认）

| # | 决策点 | 结论 |
|---|--------|------|
| D-1 | 主方向 | **D：技术债收尾 + 业务功能补齐混合推进** |
| D-2 | 双轨下线范围 | **A2：批次 1 + 批次 2**；W4 三 BC 退役另开窗口，本次不触碰 |
| D-3 | 业务任务范围 | **B1：交易闭环组 7 项**（PRD-06/07、CART-05、ORD-09、RAS-06、PAY-05/06） |
| D-4 | 验证基线 | **V2**：单测 + 契约测试兜底；仅 D1/D2 起 compose 端到端回放 |
| D-5 | C2 删除时机 | **1a：立即删除**，零命中日志仅作佐证，不设观测期 |
| D-6 | D3 差分裁决 | **2a：授权按 `docs/spec/05-促销域.md` 裁决**；文档无据的个案暂停向用户报告 |
| D-7 | 执行编排 | **方案甲：风险递增串行**，四阶段，每阶段闸门 = CI 全绿 + 验收清单 + 用户确认 |

## 0.1 范围外（明确不做）

- W4 三 BC 原子退役（UserAuth / PointsMembership / ReviewAfterSales）与 RS256 阶段 2/3 —— 需维护窗口，单独立项
- W5 其余澄清项：E2 SellerShop 读模型、E3 读模型权威终局、G1 Auth:Mode 目标形态
- 库存重构遗留第 3/4 项：台账 SellerId 语义契约变更、gRPC 通道加密
- 全部 19 个 P2 任务中交易闭环组之外的任务（通知可靠性组、账号安全组、平台基建组）

---

## 1. 总体结构

```
阶段 0  批次 1 机械收敛     A5 清单单一来源 → C2 → B3 → C3 → F5     （CI 绿灯即验收）
阶段 1  低风险开关收口     D4 SKU 快照迁移 → D3 规则引擎差分裁决    （属性差分测试 + 迁移断言）
阶段 2  高风险开关切流     D1 Saga → D2 支付后 PM                  （compose 端到端回放）
阶段 3  业务闭环补齐       PRD-07 → PRD-06 → CART-05 → ORD-09 → RAS-06 → PAY-06 → PAY-05（依赖序）
```

设计原则（承收尾方案五原则）：

1. **开关必须删除，而不是打开** —— 收尾不是 `flag=true`，是删 flag 与输掉的轨。
2. **输掉的轨道连同代码一起删** —— 不保留回退路径。
3. **验证用确定性差分，不用生产灰度**。
4. **一个数据只有一个权威，一个机制只有一种实现**。
5. **部署清单只维护一份**。

每阶段闸门：`dotnet build` 全绿 + 全量单测/契约测试绿 + 阶段专项验收清单 + 用户确认后进下一阶段。每阶段一批 commit；`docs/双轨下线-实施方案.md` §7 与 `docs/todo/tasks/progress.md` 随每阶段完成即回写（保持 SSOT）。

## 2. 阶段 0 —— 批次 1 机械收敛（5 项，按依赖顺序）

### 2.1 A5 端口清单单一来源（先行）

- 新建 `deploy/services.manifest.json`，字段：`name` / `httpPort` / `grpcPort` / `image`，覆盖当前全部 19 个服务（含待退役的 UserAuth / PointsMembership / ReviewAfterSales——W4 退役时随删同步缩减至 16），作为端口唯一权威来源。
- 修正现存互换：Helm `PaymentApi=5158` 改为 5156，`review-aftersales` 归位 5158（以 compose/KV 语义为准绳，manifest 记录正确值）。
- 新增 CI job：manifest × docker-compose × Helm chart（`services` 块）× KV `ServiceUrls__*` 四方一致性校验，任何漂移使 CI 变红。
- 先行的理由：后续所有阶段都会触碰部署物料，先立单一来源避免边改边漂移。

### 2.2 C2 旧路由删除

- 删除 5 个 BC 的旧前缀端点（`/internal/*` 中不含 `/v1/` 的全部端点）。
- 删除 `LegacyInternalRouteDetectorMiddleware` 及其 `WebApplicationExtensions` 中的注册、其约 40 个单测。
- 存量调用方一律改走 `internal/v1/*`（若 grep 发现残留调用）。
- 按 D-5：立即删除；实施后本地冒烟一轮，`LEGACY-INTERNAL-ROUTE` 日志零命中仅作佐证记录进收口报告，不设观测期。

### 2.3 B3 延迟消息单轨化

- `OrderAppService`（约 :319）的 `_bus.CreateMessageScheduler()` 改为构造注入 `IMessageScheduler`（Quartz 持久化调度器已在 `Leno.Infrastructure` DI 注册）。
- 删除直发路径与相关 try/catch 兜底注释；延迟消息只走 Quartz。
- 验证：既有超时取消集成测试（下单 → 到期 → Saga 收到超时消息 → 自动取消）保持绿。

### 2.4 C3 gRPC 契约单形态

- 除 `inventory.proto` 外，其余 proto 删除 int64 形态字段，只留 string；同步重新生成契约代码。
- 删除 Review 侧"只发 int64 则抛错"的防御分支；删除 SellerShop 的兼容分支。

### 2.5 F5 乐观锁列名统一

- 全部收敛为 `rowversion` / `RowVersion`；删除 `Revision` / `Version` 别名与影子列（确认 Order 的 `DropOrderVersionShadowColumn` 迁移无回潮）；EF 映射统一。
- CI 增加守护规则：grep 检查 `Revision`/`Version` 别名列映射，禁止回潮。

### 阶段 0 验收清单

- [ ] `dotnet build` 全解决方案零错误
- [ ] 全量单测 + 契约测试绿
- [ ] CI 新增 manifest 四方校验 job 生效（人工破坏一次验证其会红，随后恢复）
- [ ] 全仓 grep：`CreateMessageScheduler` 零命中、旧路由端点零命中、`Revision`/`Version` 别名映射零命中
- [ ] §7 状态回写：C2/B3/C3/F5/A5 → ✅

## 3. 阶段 1 —— 低风险开关收口（D4 → D3）

### 3.1 D4 购物车 SKU 快照单轨化

- `cart-sku-snapshot-backfill.sql` 转为**幂等 EF 迁移**（`UPDATE ... WHERE SkuSnapshot IS NULL` 形态），随后追加列约束 `NOT NULL`。
- 删除读路径旧格式回退分支（无快照时解析旧 JSON 的代码）；删除 `Cart:UseSkuSnapshot` 开关及分支。
- 验证：迁移幂等性集成测试（连跑两次结果一致）；迁移后全表快照非空断言；读路径单测改写为只测快照格式（删除旧格式用例）。

### 3.2 D3 规则引擎成为唯一折扣实现

**顺序强制：先建验证，后做删除。**

1. **差分 harness**：`Promotion.Tests` 新增属性差分测试。
   - 语料生成器维度：购物车形态（单品 / 多品 / 含失效项 / 超量）× 规则组合（满减 / 折扣 / 阶梯 / 叠加 / 互斥）× 边界（临界金额 / 0 元 / 取整 / 精度）。
   - 对同一输入分别执行旧算法与规则引擎，**逐字段断言**：订单总价、各品分摊金额、优惠明细结构。
2. **裁决（D-6）**：不等价案例按 `docs/spec/05-促销域.md` 裁决；文档无据可依的个案暂停报告用户；每次裁决结果以「期望值固化用例」进语料，防回潮。
3. **删除**：语料全绿后，删除旧折扣算法类与其单测、`Promotion:UseRuleEngine` 开关及所有分支。

### 阶段 1 验收清单

- [ ] 迁移幂等测试通过、快照全表非空断言通过
- [ ] 差分语料 ≥ 200 案例，全部等价或裁决固化
- [ ] 全仓 grep：`UseSkuSnapshot` / `UseRuleEngine` 零命中
- [ ] 单测 + 契约测试绿；§7 状态回写 D4/D3 → ✅

## 4. 阶段 2 —— 高风险开关切流（D1 → D2，compose 端到端）

> 按 D-4，仅本阶段起 compose 全链路（consul / sqlserver / redis / rabbitmq / elasticsearch + 相关 API），沿用库存重构 2026-09-24 验证的物料与脚本模式。

### 4.1 D1 Saga 成为"下单到支付决策"唯一生命周期拥有者

- **回放先行**：六链路 staging 回放脚本——创建 / 支付 / 超时取消 / 主动取消 / 退款 / 强制取消；断言 `order_saga_states` 终态与订单表状态逐单吻合。
- **删除**：`OrderSagaOrchestrator` 进程内编排、`Order:UseSagaStateMachine` 开关、shadow 并行转发路径。
- **保留**：创建路径为同步事务（校验 → gRPC 预占 → 落库 → Outbox `OrderCreated`），返回值由创建事务直接构造——Saga 只接管创建之后的生命周期。
- **复跑**：删除后同脚本再跑一遍，终态与删除前逐单一致才放行。

### 4.2 D2 支付后 Process Manager 唯一协调

- **回放先行**：支付成功事件 → 断言 `order_payment_processes` 三子任务（库存确认 / 积分确认 / 订单已支付）全部完成；补偿路径以人为注入单子任务失败验证可收敛、可重放（DLQ）。
- **删除**：`Order:UsePaymentProcessManager` 开关 + shadow 转发（`StockConfirmConsumer` 等处 flag 分支）。
- **边界守则（D1/D2 零交叉）**：Saga 管"下单到支付决策"（预占、积分冻结、支付窗口、超时取消）；PM 管"支付成功后"（确认扣减、积分入账、订单状态、失败补偿）；两张状态表、职责不交叉。

### 阶段 2 验收清单

- [ ] 六链路回放（删除前）终态记录归档；删除后复跑终态逐单一致
- [ ] PM 三子任务完成断言 + 补偿收敛断言通过
- [ ] 全仓 grep：`UseSagaStateMachine` / `UsePaymentProcessManager` 零命中
- [ ] 单测 + 契约测试绿；§7 状态回写 D1/D2 → ✅

## 5. 阶段 3 —— 交易闭环 7 项（依赖序，在单轨代码上开发）

| 序 | 任务 | 关键设计决策 | 前端落点 |
|----|------|-------------|---------|
| 3.1 | **PRD-07** SKU 编码全局唯一校验 | 领域接口 `IProductUniquenessChecker` + 基础设施实现（查库校验）；编辑场景 `excludeProductId`；SKU 码加 DB 唯一索引兜底（应用校验 + 约束双层）；重复错误信息明确（"SKU 编码已被使用" / "商品标题已存在"） | 无（纯后端） |
| 3.2 | **PRD-06** 运营全量商品管理列表 | `GET /api/admin/products/all`；筛选：状态（草稿/待审核/已上架/已下架/已驳回/店铺暂停）、卖家 ID、分类 ID、关键词；分页返回含审核状态；与卖家端隔离；权限 Admin/Operator | operations |
| 3.3 | **CART-05** 全选/取消全选 | `Cart` 聚合 `ToggleAllSelection(bool)`，仅操作 `IsValid=true` 项，失效项保持未选中；`PATCH /api/cart/selection`；空购物车幂等无副作用 | buyer-app |
| 3.4 | **ORD-09** 运营强制取消异常订单 | 复用阶段 2 收口后的单轨 Order 代码完善 `POST /api/admin/orders/{id}/force-cancel`；已支付 → 发 `RefundRequestedIntegrationEvent` 走既有退款链路；待支付 → 直接取消并释放库存/积分/优惠券；操作日志 + 审计日志；经 BC10 通知买卖双方；仅 Admin 角色 | operations（订单管理页加按钮） |
| 3.5 | **RAS-06** 售后凭证与评价图片上传 | 走既有 `IFileStorageService` 抽象（SK-02 MinIO 适配器，不直接依赖具体实现）；凭证 ≤5 张、评价 ≤9 张、单张 ≤5MB、JPG/PNG/WebP；校验在应用层 | buyer-app |
| 3.6 | **PAY-06** 支付渠道配置管理 | `PaymentChannelConfig` 实体（Channel/AppId/MchId/PrivateKey/PublicKey/Enabled）；密钥 AES-256 加密存储（密钥来自配置/KV，**严禁硬编码**，符合硬约束 #12）；列表脱敏返回；启/停用端点；变更发布集成事件通知支付域刷新配置；启停不影响已发起支付 | system-admin |
| 3.7 | **PAY-05** 对账文件下载 | `ReconciliationService`（`BackgroundService` + Quartz 持久化调度，与 B4 机制一致）每日 T+1：微信 `GET /v3/bill/tradebill`、支付宝 `alipay.data.dataservice.bill.downloadurl.query` → 下载 → CSV/TXT 解析 → 按交易号与本系统支付单比对 → 差异入 `ReconciliationDiff` → `GET /api/admin/reconciliation/diffs` 查询；渠道客户端以接口适配器封装，测试用 Mock（规则 10.4：Mock 仅测试用） | system-admin（对账差异页） |

依赖与顺序：PRD-07 无依赖最先；ORD-09 必须在阶段 2 完成后（单轨 Order 代码上开发）；PAY-06 先于 PAY-05（对账依赖渠道配置）；其余并行安全。

### 阶段 3 验收清单

- [ ] 7 项各自验收标准（对应 `docs/todo/tasks/*.md`）全部勾选
- [ ] 每项 TDD：先失败测试后实现；单测 + 集成测试绿
- [ ] 前端页面 + vitest spec 绿；四端公共逻辑进 `@leno/shared`
- [ ] `progress.md` 状态回写：7 项 → ✅，整体完成率 78.2% → 86.2%（75/87；其余 12 项属范围外组，见 §0.1）

## 6. 测试与文档策略（全程贯穿）

- **TDD（规则 10.5）**：每项业务任务先写失败测试再实现；单测 + 集成测试双层。
- **守护 CI（防回潮）**：阶段 0-2 新增三类守护进入 `ci.yml` 永久生效——manifest 四方校验 job、乐观锁别名 grep 守护、四个开关名零命中检查。
- **前端**：每个新页面/组件配 vitest spec，与既有 `*.spec.ts` 模式一致。
- **错误处理**：统一走既有全局异常中间件与一致错误响应格式（规则 8.3 / 7.7），不新造机制。
- **文档回写**：每阶段完成即回写 `双轨下线-实施方案.md` §7 与 `progress.md`；最终产出一份收口记录（四阶段执行证据 + 验收结果）。
- **遗留移交**：W4 三 BC 退役、W5 澄清项（E2/E3/G1）、库存遗留第 3/4 项，单列"遗留清单"写入收口记录，移交下次立项。

## 7. 最终验收总表

| 阶段 | 交付 | 证据 |
|------|------|------|
| 0 | 6 特性开关中的 0 个删除，5 项机械收敛完成 | CI 绿 + grep 零命中记录 |
| 1 | `UseSkuSnapshot` / `UseRuleEngine` 删除 | 迁移断言 + 差分语料全绿 |
| 2 | `UseSagaStateMachine` / `UsePaymentProcessManager` 删除 | compose 六链路回放前后终态一致 |
| 3 | 7 项业务任务完成 | 各任务验收标准勾选 + 测试绿 |
| 收尾 | 特性开关 6 → 2（`Auth:Mode` 随 W4 退役删除；`AntiCorruption:UseGrpc` 终局随后续澄清项处置，源文档未绑定 W4） | 收口记录 |

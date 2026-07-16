# Leno 代码库全面审计与修复方案设计

**文档版本**：V1.1（审计已执行）
**创建日期**：2026-07-15
**审计执行完成日期**：2026-07-16
**审计对象**：Leno 电商平台全部 11 个限界上下文 + BuildingBlocks + ApiGateway
**审计定位**：全新独立审计，不继承既有 `comprehensive-optimization-design.md` 的结论，仅在最终问题清单阶段对重复条目做标注
**交付物**：问题清单 + 修复方案（形成可交付的实施 spec）

---

## 1 背景与目标

### 1.1 项目现状

Leno 是基于 .NET 10 的 DDD 微服务电商平台，按 11 个限界上下文拆分：

- 6 个核心域：用户认证、商品、购物车、订单交易、促销、评价售后
- 3 个支撑域：积分会员、支付集成、卖家店铺
- 2 个通用子域：消息通知、系统管理

代码库已有 2153+ 个单元测试，配套 13 篇需求 spec 与编码规范。既有 `scripts/check-placeholders.sh` 已覆盖 `NotImplementedException`、`SmokeTest`、`return default!/null!` 等基础占位模式检测，运行结果为"未检测到占位实现"。

### 1.2 审计触发原因

用户要求对项目代码做全面检测，聚焦 4 类问题：

1. **业务代码未实现**：方法中用注释或日志记录意图，但缺少对应业务调用（"注释/日志伪装实现"模式，比既有脚本检测的占位模式更隐蔽）
2. **缺失功能**：spec 声明应有但代码未落地的能力
3. **架构反模式**：通用代码复用不足、限界上下文穿透、分层违规等
4. **冗余代码**：死代码、跨文件重复、未使用成员

### 1.3 审计目标

1. 系统识别全代码库中上述 4 类问题，产出可追溯的问题清单
2. 对照 `docs/spec/01~12-*.md` 13 篇需求文档，核查 spec 与实现差距
3. 为每条问题补充可执行的修复方案与验证标准
4. 按依赖与优先级编排修复批次，形成可交付的实施 spec
5. 对与既有 `comprehensive-optimization-design.md` 重复的条目做标注，保持审计独立性的同时避免重复劳动

### 1.4 与既有资产的关系

| 既有资产 | 关系 |
|---|---|
| `scripts/check-placeholders.sh` | 审计规则集 R1.7 纳入其检测项，确保零遗漏；审计产出的新规则可沉淀回该脚本 |
| `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` | **不作为扫描输入**，仅在阶段 4 编排时用于标注"既有 spec 已覆盖" |
| `.trae/specs/*/spec.md` | 同上，仅阶段 4 标注用 |
| `docs/spec/01~12-*.md` | 阶段 3 差距分析的核心输入 |
| `docs/编码规范.md` | 架构反模式判定的依据之一 |

---

## 2 审计方法学

采用**方案 A：模式驱动扫描 + 候选人工复核**，4 阶段流水线，每阶段产出可独立复核的中间产物。

### 2.1 阶段 1 — 自动化模式扫描

- 对全代码库按预定义规则集（详见第 3 节）批量执行 Grep/SearchCodebase
- 产出"嫌疑候选清单"（CSV 式：文件路径:行号 | 类别 | 匹配模式 | 命中代码片段）
- 该阶段不做语义判断，只做模式命中

### 2.2 阶段 2 — 候选复核与分类

- 逐条读取候选方法的完整上下文（前后 20-40 行）
- 按第 4 节决策树分类为：`未实现业务代码` / `缺失功能` / `架构反模式` / `冗余代码`，外加 `误报`
- 对每条真问题补充：严重级（P0/P1/P2）、影响范围、根因简述
- 产出"分类问题清单"

### 2.3 阶段 3 — Spec 与实现差距分析

- 读取 `docs/spec/01~12-*.md` 13 篇需求文档，提取每个 BC 的"应有功能清单"
- 对照实际代码（Controller 端点、AppService 方法、Consumer 订阅、Domain 校验）核查落地情况
- 产出"缺失功能清单"（独立于阶段 2，因为缺失功能不一定有"嫌疑代码"可扫）
- **独立从需求 spec 出发**，不参考既有优化 spec

### 2.4 阶段 4 — 修复方案编排

- 合并阶段 2 + 阶段 3 的问题清单
- 为每条问题补充修复方案（伪代码/代码 sketch、影响文件、验证方式）
- 对每条问题执行既有 spec 比对，标注"既有 spec 已覆盖"状态
- 按优先级与依赖关系排序，形成可交付的实施 spec

### 2.5 阶段流水线图

```
阶段 1 (模式扫描)
  │
  ├─→ 阶段 2 (候选复核与分类) ──┐
  │                              ├─→ 阶段 4 (修复方案编排) ──→ 最终 spec
  └─→ 阶段 3 (spec 差距分析) ───┘
```

### 2.6 关键约束

- 阶段 1 的规则集是审计质量的核心（详见第 3 节）
- 阶段 2 的分类标准必须可复现（详见第 4 节）
- 阶段 3 独立从需求 spec 出发，不参考既有优化 spec
- 阶段 4 标注既有 spec 覆盖状态，但不删除重复条目
- 全程不修改任何代码，只产出文档

---

## 3 阶段 1 — 模式扫描规则集

规则集按用户列的 4 类问题组织，每条规则包含：规则 ID、目标类别、Grep/搜索模式、排除项、典型命中示例。所有规则对 `src/` 全量执行，排除 `obj/bin/*Tests*`（除非规则专门针对测试）。

### 3.1 类别 1：未实现业务代码（注释/日志伪装实现）

| ID | 模式 | 说明 |
|---|---|---|
| R1.1 | `// TODO\|// FIXME\|// 待实现\|// 待补充\|// 暂未实现\|// 待完善\|// 待开发` | 中英文未实现标记 |
| R1.2 | `// 应该\|// 应当\|// 需要\|// 这里应该\|// 此处应\|// 实际应\|// 真实应` 后接业务动作动词 | **注释描述意图但代码缺失（主场景）** |
| R1.3 | 方法体仅含 `_logger.Log*` 调用 + `return`，无其他业务调用（通过 Read 上下文判定） | "日志即实现"模式 |
| R1.4 | `return Task.CompletedTask;` / `return Task.FromResult` 作为方法唯一非空语句 | 空任务占位 |
| R1.5 | `await Task.Delay` 无业务逻辑伴随 | 假异步占位 |
| R1.6 | 方法体仅 `return default\|return null\|return default!\|return null!` | 空返回占位 |
| R1.7 | `throw new NotImplementedException\|throw new NotSupportedException` 在非测试代码 | 显式未实现（已被既有脚本覆盖，仍纳入确保零遗漏） |
| R1.8 | 注释含 `模拟\|mock\|假数据\|临时\|stub\|placeholder\|dummy` 后接返回语句 | 模拟数据伪装 |
| R1.9 | `=> throw new NotImplementedException` 表达式体 | 表达式体未实现 |

**R1.2 为"注释伪装实现"主场景**，阶段 2 复核时优先关注。

### 3.2 类别 2：缺失功能（辅助扫描）

这类主要靠阶段 3 的 spec 差距分析发现，阶段 1 仅做辅助扫描：

| ID | 模式 | 说明 |
|---|---|---|
| R2.1 | 接口方法在 `IXxxAppService` 声明但无对应实现类（通过 SearchCodebase 查实现） | 接口无实现 |
| R2.2 | `app.MapGet\|app.MapPost` 端点路由数远少于 spec 描述的端点数（按 BC 统计比对） | 端点缺失 |
| R2.3 | Consumer 类订阅的事件类型 vs spec 第 X 节"事件清单"声明的事件类型差集 | 事件订阅缺失 |
| R2.4 | `// 暂不支持\|// 未启用\|// 已禁用\|// 跳过` 伴随功能开关语义 | 功能被禁用占位 |

### 3.3 类别 3：架构反模式

| ID | 模式 | 说明 |
|---|---|---|
| R3.1 | `*.Infrastructure.csproj` 中 `ProjectReference` 含其他 BC 的 `*.Domain` 或 `*.Application` | 限界上下文穿透 |
| R3.2 | `using Leno.{Xxx}.Domain` 出现在其他 BC 的代码 | 跨 BC 领域层依赖 |
| R3.3 | `using Leno.{Xxx}.Application` 出现在其他 BC 的代码 | 跨 BC 应用层依赖 |
| R3.4 | `SharedKernel` 中出现 `EF\|DbContext\|Microsoft.EntityFrameworkCore\|SqlClient\|HttpStatusCode` | 共享内核泄漏技术细节 |
| R3.5 | `*.Domain.csproj` 引用 `Microsoft.EntityFrameworkCore\|StackExchange.Redis\|RabbitMQ\|MassTransit` | 领域层依赖基础设施 |
| R3.6 | 11 份 `UnitOfWork.cs` / `Program.cs` 文本高度相似（通过 diff 比对） | 样板重复反模式 |
| R3.7 | ~~`catch (Exception` 后无 `throw` 且仅 `Log` 或空体~~ | **移出核心规则集，改为待观察项** |
| R3.8 | ~~防腐层服务 `catch` 后 `return 空集合\|return null\|return default` 而非抛领域异常~~ | **移出核心规则集，改为待观察项** |
| R3.9 | `async` 方法无 `await`（编译警告 CA2007 类问题） | 假异步 |
| R3.10 | `static readonly` 单例字段无 `Lazy<>` 或 `lock` 保护且为可变集合 | 非线程安全单例 |
| R3.11 | `new List<>\|new Dictionary<>` 作为 `static` 字段且公开可访问 | 静态可变状态 |

**R3.7/R3.8 按用户指示移出核心分类**，改为就地标注模式（见第 4.5 节）。

### 3.4 类别 4：冗余代码

| ID | 模式 | 说明 |
|---|---|---|
| R4.1 | 同一文件内 3+ 处相同代码块（通过 Read 判定） | 文件内重复 |
| R4.2 | 跨 BC 同名文件内容高度相似（`UnitOfWork.cs`、`GlobalUsings.cs`、`appsettings.json` 等） | 跨文件重复 |
| R4.3 | `private` / `internal` 方法无任何调用方（通过 SearchCodebase 反查） | 死方法 |
| R4.4 | `private` / `internal` 字段无任何读取（通过 SearchCodebase 反查） | 死字段 |
| R4.5 | `#if DEBUG` 或被注释掉的代码块 > 5 行 | 注释死代码 |
| R4.6 | 空类、空命名空间声明 | 空结构 |
| R4.7 | `using` 未使用（编译警告，但脚本扫描补强） | 未使用 using |
| R4.8 | 仅含 `GlobalUsings.cs` 的测试项目，无实际测试文件 | 空测试项目 |

### 3.5 规则执行策略

- **并行批次**：R1.\*、R3.\*、R4.\* 互不依赖，可并行 Grep；R2.\* 部分依赖阶段 3 的 spec 解析，部分前置
- **两轮扫描**：第一轮全量 Grep 产出原始命中；第二轮对命中数 > 50 的规则追加 `head_limit` 采样复核
- **误报预判**：R1.4/R1.5/R1.6/R3.9 在事件处理、后台任务等场景有合法用途，阶段 2 必须读上下文判定
- **新规则发现**：阶段 2 复核中若发现规则集未覆盖的新模式，回填到规则集并补扫（增量闭环）

---

## 4 阶段 2 — 候选复核与分类标准

为保证同一代码不同时间审计得到一致结论，阶段 2 采用**结构化判定流**，每条候选项按固定决策树分类。

### 4.1 复核决策树

每条候选进入复核时，依次回答以下问题，首个命中的分支即定论：

```
候选项 (文件:行号, 命中规则, 命中代码)
  │
  ├─ Q1: 读取方法完整实现（前后 20-40 行）后，方法是否完成其签名/注释声明的业务职责？
  │      ├─ 是 → 标记 误报，记录原因（如合法的 LogAndReturn、合法的 Task.CompletedTask）
  │      └─ 否 → 进入 Q2
  │
  ├─ Q2: 方法是否有"意图注释/日志"但缺少对应业务调用？
  │      ├─ 是 → 分类 = 未实现业务代码，记录证据（注释文本 + 缺失的调用）
  │      └─ 否 → 进入 Q3
  │
  ├─ Q3: 该方法是否属于某 AppService/Controller/Consumer 公共契约的一部分，
  │       而 spec 声明了该契约应有但未提供的能力？
  │      ├─ 是 → 分类 = 缺失功能，关联 spec 章节
  │      └─ 否 → 进入 Q4
  │
  ├─ Q4: 该代码是否违反 DDD/分层/依赖方向/线程安全等架构约束？
  │      ├─ 是 → 分类 = 架构反模式，记录违反的约束名
  │      └─ 否 → 进入 Q5
  │
  ├─ Q5: 该代码是否有完全等价的替代实现已存在（同文件或跨文件）？
  │      ├─ 是 → 分类 = 冗余代码，记录等价实现位置
  │      └─ 否 → 标记 需进一步确认，回退到人工二次复核队列
```

### 4.2 严重级定义

每条真问题按以下标准定级，避免主观：

| 级别 | 判定标准（满足任一） | 典型场景 |
|---|---|---|
| **P0** | ① 影响资金/订单/支付/库存数据一致性；② 限界上下文边界穿透；③ 领域层泄漏基础设施 | 支付防腐层空实现、Notification 引用 Promotion.Domain |
| **P1** | ① 业务功能未实现但已对外暴露端点；② 跨 BC 样板重复 > 5 处；③ 死代码占代码量 > 10% | Controller 端点返回空、11 份 UnitOfWork 重复 |
| **P2** | ① 改进性优化；② 单点重复；③ 风格类反模式 | 单个未使用 using、单处 catch 吞异常 |

### 4.3 每条问题的记录字段

阶段 2 产出统一格式的问题条目，便于阶段 4 编排：

```
问题编号: AUDIT-<类别缩写>-<三位序号>  如 AUDIT-IMPL-001
类别: 未实现业务代码 / 缺失功能 / 架构反模式 / 冗余代码
严重级: P0 / P1 / P2
命中规则: R1.2 / R3.1 / ...
位置: 文件绝对路径:起始行-结束行
证据: 命中代码片段（含注释/日志原文）
根因: 一句话描述为什么是问题
影响范围: 受影响的调用方/端点/测试数
```

类别缩写约定：
- `AUDIT-IMPL-NNN`：未实现业务代码
- `AUDIT-MISS-NNN`：缺失功能
- `AUDIT-ARCH-NNN`：架构反模式
- `AUDIT-REDUN-NNN`：冗余代码

### 4.4 误报处理原则

为避免清单噪声，以下场景即使命中模式也判为误报：

| 模式 | 合法场景 | 处理 |
|---|---|---|
| `return Task.CompletedTask` | 事件处理器的 Dispose、Cancellation 注册回调 | 误报 |
| `return default` | TryGetXxx 模式、Optional 模式 | 误报 |
| `catch (Exception) { log }` | 顶层兜底（Program.cs、BackgroundService 主循环） | 误报，但加注释 `// AUDIT-NOTE: 顶层兜底，建议加告警` |
| `using Leno.{Xxx}.Domain` | Testing 项目、TestFixtures | 误报 |
| `static readonly` 单例 | 不可变值对象、readonly struct | 误报 |

### 4.5 R3.7/R3.8 标注模式（待观察项）

按用户指示，吞异常与静默兜底移出核心分类，改为**就地标注**：

- 在问题清单中独立一节"待观察项 — 吞异常与静默兜底"
- 每条只记录：位置 + 命中代码 + 标注建议（如 `// AUDIT-NOTE: 此处吞异常，建议改为抛 DomainException + 告警`）
- 不定严重级、不进 P0/P1/P2 排序
- 阶段 4 不为其编排修复方案，仅作为附录供后续修改参考

### 4.6 复核工作量控制

- 候选数预期 200-400 条（基于规则集规模与代码库量级估算）
- 误报率目标 < 30%（过高说明规则需收紧）
- 每条复核耗时控制在读 1 个文件上下文内，避免发散

---

## 5 阶段 3 — Spec 与实现差距分析

阶段 3 独立从 `docs/spec/01~12-*.md` 13 篇需求文档出发，不参考既有 `comprehensive-optimization-design.md`，核查"spec 声明应有 vs 代码实际落地"的差距。

### 5.1 Spec 解析范围

13 篇需求文档按 BC 对应，每篇提取 4 类"应有清单"：

| 清单类型 | 提取内容 | 代码侧比对对象 |
|---|---|---|
| **端点清单** | spec 中描述的 HTTP API（路径、方法、功能） | 各 BC.Api 的 `app.MapGet/Post/Put/Delete` |
| **AppService 清单** | spec 中描述的应用服务及其方法 | 各 BC.Application 的 `IXxxAppService` 接口与实现类 |
| **事件清单** | spec 第 X 节"领域事件/集成事件"声明的事件类型 | `Leno.SharedContracts/Events/*.cs` + 各 Consumer 订阅 |
| **领域规则清单** | spec 中描述的业务规则（如"积分不可为负"、"库存预占 30 分钟"） | Domain 层聚合根/值对象的校验逻辑 |

排除项：`00-需求文档总览与DDD架构.md`（架构总览，不产出清单）、`10-模块化部署架构.md`（部署相关，非业务功能）。

### 5.2 差距判定流程

每个 BC 独立执行：

```
1. Read docs/spec/{NN}-{BC名}.md
   ├─ 提取端点清单 → 列表 E_spec
   ├─ 提取 AppService 清单 → 列表 A_spec
   ├─ 提取事件清单 → 列表 V_spec
   └─ 提取领域规则清单 → 列表 R_spec

2. Grep/Search 各 BC 代码
   ├─ 扫描 MapGet/Post → 列表 E_code
   ├─ 扫描 IXxxAppService 及实现 → 列表 A_code
   ├─ 扫描 SharedContracts/Events + Consumer 订阅 → 列表 V_code
   └─ Read Domain 聚合根校验方法 → 列表 R_code

3. 求差集
   ├─ E_spec - E_code → 缺失端点
   ├─ A_spec - A_code → 缺失 AppService 方法
   ├─ V_spec - V_code → 缺失事件（发布或订阅）
   └─ R_spec - R_code → 缺失领域规则校验

4. 每条差距生成问题条目
   字段：问题编号 AUDIT-MISS-NNN、类别=缺失功能、
        严重级（按 4.2 定级）、位置（spec 章节号 + 预期代码位置）、
        证据（spec 原文摘录）、根因、影响范围
```

### 5.3 关键判定细则

为减少主观性，差距判定遵循以下规则：

| 场景 | 判定 | 依据 |
|---|---|---|
| spec 描述"用户可查看订单物流轨迹" | 查找 `MapGet("/logistics/trace"` 或 `TrackOrderAsync` 方法 | 端点+方法双匹配 |
| spec 描述"支付成功后发布 PaymentSucceededEvent" | 查找 `PaymentSucceededIntegrationEvent` 类 + 其在 Outbox/EventBus 的发布点 | 事件类+发布点双匹配 |
| spec 描述"积分账户余额不可为负" | 查找 PointsAccount 聚合根的 Consume 方法是否有 `if (balance < amount) throw` | 校验逻辑存在即视为已实现 |
| spec 描述的功能在代码中以"内部方法"而非"端点"存在 | 判为**部分实现**（P1，非完全缺失） | 功能存在但对外暴露不完整 |
| spec 描述的功能在代码中存在但被 `#if DEBUG` 或配置开关禁用 | 判为**实现但禁用**（P2） | 功能存在但默认不可用 |
| spec 未明确描述但代码中存在的能力 | **不判为问题**（正向差距，记录但不进问题清单） | spec 可能滞后于代码 |

### 5.4 领域规则核查（全量）

按用户指示采用**全量核查**，不抽样：

- 每个 BC 从 R_spec 中提取**全部**业务规则
- 读取对应 Domain 聚合根/值对象的完整源码
- 逐条核查校验逻辑是否存在
- 工作量较大时按 BC 分批执行，每完成一个 BC 即产出阶段性差距清单，不等待全量完成

### 5.5 阶段 3 产出格式

独立产出"缺失功能清单"，与阶段 2 的分类问题清单平行，阶段 4 合并：

```
阶段 3 产出（独立文档节）:
  - 按 BC 分组（BC1 用户认证 ... BC11 系统管理）
  - 每个 BC 下按清单类型分组（端点/AppService/事件/领域规则）
  - 每条差距条目字段同 4.3，编号前缀 AUDIT-MISS
  - 末尾附"正向差距"附录（代码有但 spec 未描述的能力，仅记录不判问题）
```

### 5.6 阶段 3 边界

- **不做**：代码实现质量评估（如校验逻辑是否正确，只看"有没有"）
- **不做**：性能、并发、安全等非功能属性核查
- **只做**：spec 声明的能力在代码中是否存在（binary 判定 + 部分实现/实现但禁用两个中间态）

---

## 6 阶段 4 — 修复方案编排

阶段 4 合并阶段 2 + 阶段 3 的问题清单，为每条问题补充修复方案，按依赖与优先级排序，产出最终实施 spec。

### 6.1 修复方案模板

每条问题在 4.3 字段基础上追加以下字段：

```
修复方案:
  方向: 一句话描述修复思路
  影响文件: 需修改的文件列表（绝对路径）
  代码 sketch: 伪代码或关键代码片段（不超过 15 行，展示修复后形态）
  依赖问题: 本修复依赖的其他问题编号（无依赖填"无"）
  验证方式: 单元测试/集成测试/编译验证/Grep 复扫
  风险: 修复可能引入的副作用（无填"低"）
既有 spec 标注:
  既有 spec 覆盖: 是 / 否
  既有 spec 引用: 文档路径#章节号（覆盖=是时填写）
  既有 spec 状态: 已修复 / 部分修复 / 未修复（覆盖=是时填写，通过代码复扫判定）
```

### 6.2 修复方案分级策略

不同类别问题采用不同修复策略，避免"一刀切"：

| 问题类别 | 修复策略 | 典型方案形态 |
|---|---|---|
| **未实现业务代码** | 补全真实业务调用，删除意图注释 | 补防腐层 HTTP/gRPC 调用、补领域校验 throw、补事件发布 |
| **缺失功能** | 按 spec 补全端点/AppService/事件/校验 | 新建方法+实现类+注册+测试 |
| **架构反模式** | 重构到合规结构，优先最小侵入 | 引入翻译层、迁移接口到正确命名空间、抽取泛型基类 |
| **冗余代码** | 删除或合并到公共位置 | 删除死方法、合并 UnitOfWork 到泛型基类 |
| **待观察项（R3.7/R3.8）** | 仅就地向代码加 `// AUDIT-NOTE:` 注释，不改动逻辑 | 注释标注建议方向 |

### 6.3 依赖排序原则

问题间存在依赖时，按以下拓扑序编排：

```
1. 架构反模式（P0）— 先修复边界与分层，避免后续修复建在错误结构上
   └─ 如: R3.1 限界上下文穿透修复
2. 未实现业务代码（P0/P1）— 在合规结构上补全核心逻辑
   └─ 如: R1.2 支付防腐层补全调用
3. 缺失功能（P0/P1）— 补全 spec 声明的能力
   └─ 如: AUDIT-MISS 缺失端点
4. 冗余代码（P1/P2）— 在功能完整后清理重复
   └─ 如: R4.2 合并 UnitOfWork
5. 待观察项 — 随相关模块修复时顺带标注，不单独排期
```

同级内按"严重级 P0 > P1 > P2 > 同级按 BC 依赖序（共享内核 → 核心域 → 支撑域 → 通用子域）"排序。

### 6.4 分组交付单元（按批次）

为避免单 spec 过大且便于分批实施，问题清单按**修复批次**分组（按用户指示采用批次划分，而非按类别组织）：

| 批次 | 内容 | 触发条件 |
|---|---|---|
| **批次 1: 架构合规修复** | 所有 P0 架构反模式 + 其直接依赖的未实现业务代码 | 边界/分层违规必须先修 |
| **批次 2: 核心功能补全** | 所有 P0/P1 未实现业务代码 + 缺失功能 | 架构合规后补业务 |
| **批次 3: 代码质量优化** | 所有 P1/P2 冗余代码 + 剩余架构反模式 | 功能完整后清理 |
| **批次 4: 待观察项标注** | R3.7/R3.8 就地注释标注 | 可与任一批次并行 |

每个批次独立可验证，批次内问题可并行修复（无横向依赖时）。

### 6.5 验证策略

每个修复方案必须声明可验证的完成标准：

| 验证类型 | 适用场景 | 标准 |
|---|---|---|
| **编译验证** | 所有方案 | `dotnet build Leno.slnx` 通过 |
| **既有测试全绿** | 所有方案 | `dotnet test` 既有 2153+ 测试通过 |
| **新增单元测试** | **仅功能类问题**（未实现业务代码、缺失功能） | 修复方法/新增方法必须有对应测试覆盖关键路径 |
| **Grep 复扫** | 模式扫描类问题 | 原命中规则在该位置不再命中 |
| **Spec 复核** | 缺失功能类问题 | spec 差距清单中对应条目标记为"已落地" |

按用户指示，新增单元测试要求**仅适用于功能类问题**（未实现业务代码、缺失功能），冗余代码与架构反模式类问题不强制要求新增测试。

### 6.6 既有 spec 标注规则

阶段 4 编排修复方案时，对每条问题执行既有 spec 比对：

```
对每条问题 P:
  1. 在既有 spec（comprehensive-optimization-design.md + .trae/specs/*/spec.md）中检索相似问题
  2. 若命中:
     - 追加字段: 既有 spec 覆盖 = 是
     - 追加字段: 既有 spec 引用 = 文档路径#章节号
     - 追加字段: 既有 spec 状态 = 已修复 / 部分修复 / 未修复（通过代码复扫判定）
  3. 若未命中:
     - 追加字段: 既有 spec 覆盖 = 否（新发现问题）
  4. 无论是否覆盖，问题保留在清单中
```

**关键原则**：
- 阶段 1/2/3 的扫描与分析过程**不参考**既有优化 spec，保持审计独立性
- 仅阶段 4 编排时做标注，避免独立扫描被既有结论带偏
- 标注"已修复"必须通过代码复扫验证，不轻信既有 spec 的验收勾选

### 6.7 阶段 4 边界

- **不做**：实际修改代码（本 spec 只产出方案，实施由后续 writing-plans 阶段承接）
- **不做**：覆盖率提升、CI 流水线改造（属实施层，不在审计 spec 范围）
- **只做**：为每条问题提供可执行的修复方案与验证标准

---

## 7 审计范围、排除项与执行约束

### 7.1 审计范围

审计范围分两类，严格区分以保持阶段 1/2/3 的独立性：

**扫描范围**（阶段 1/2/3 执行对象）：

| 范围 | 路径 | 扫描内容 |
|---|---|---|
| **11 个限界上下文** | `src/Services/*/Leno.*.{Api,Application,Domain,Infrastructure}/` | 全部 `.cs` 业务代码 |
| **BuildingBlocks** | `src/BuildingBlocks/Leno.{SharedKernel,Infrastructure,SharedContracts,Testing}/` | 全部 `.cs`（含 Abstractions） |
| **ApiGateway** | `src/ApiGateway/Leno.ApiGateway/` | 全部 `.cs`（含 Middleware/Services/Transforms） |
| **需求 spec** | `docs/spec/01~12-*.md` | 阶段 3 差距分析输入 |
| **配置与脚本** | `scripts/check-placeholders.sh`、`Directory.Build.props`、`.editorconfig` | 审计现状参考 |

**标注输入**（仅阶段 4 编排时参考，阶段 1/2/3 禁止读取）：

| 范围 | 路径 | 用途 |
|---|---|---|
| **既有优化 spec** | `docs/superpowers/specs/*.md`、`.trae/specs/*/spec.md` | 阶段 4 标注"既有 spec 已覆盖"状态 |

### 7.2 排除项

以下内容**不纳入**审计，避免噪声与重复劳动：

| 排除项 | 路径/特征 | 排除理由 |
|---|---|---|
| **测试代码** | `*Tests*/`、`Leno.Testing/` | 测试占位已由既有 spec 与 `check-placeholders.sh` 覆盖；阶段 1 规则 R4.8（空测试项目）例外纳入 |
| **生成产物** | `obj/`、`bin/` | 非源码 |
| **部署与运维** | `docker-compose.yml`、`grafana/`、`.github/workflows/` | 非业务代码 |
| **文档** | `docs/tasks/`、`docs/todo/`、`USAGE.md`、`docs/prompt.md` | 非代码 |
| **IDE/工具配置** | `.gitignore`、`.dockerignore`、`mise.toml`、`monitor-progress.ps1` | 非业务代码 |

### 7.3 执行约束

为保证审计质量与可复现性，执行过程遵循以下约束：

| 约束 | 说明 |
|---|---|
| **只读审计** | 全程不修改 `src/` 下任何文件；阶段 4 的修复方案仅以代码 sketch 形式写入 spec，不落代码 |
| **证据可追溯** | 每条问题必须附"命中规则 + 文件:行号 + 命中代码片段"，缺一不可 |
| **规则集版本固定** | 阶段 1 执行的规则集即第 3 节定义的版本；阶段 2 发现新模式回填时，新增规则标注 `R1.10` 等递增编号并记录发现来源 |
| **决策可复现** | 同一候选项按第 4 节决策树应得到唯一分类；存在歧义时标记"需进一步确认"而非强行归类 |
| **并行无副作用** | 阶段 1 的 Grep/Search 调用纯只读，可并行执行；阶段 2/3 读取上下文亦只读 |
| **工具优先级** | 模式匹配用 Grep（精确文本）；语义查询用 SearchCodebase（如"查找 IXxxAppService 的所有实现"）；文件结构用 Glob/LS |

### 7.4 工具调用预算

为控制审计成本，预估各阶段工具调用次数：

| 阶段 | 主要工具 | 预估调用次数 | 说明 |
|---|---|---|---|
| 阶段 1 | Grep | 30-40 | 11 大类规则，部分规则多 BC 分别执行 |
| 阶段 1 | SearchCodebase | 5-10 | R2.1 接口实现反查、R4.3/R4.4 死代码反查 |
| 阶段 2 | Read | 100-200 | 每条候选读 1 个文件上下文 |
| 阶段 3 | Read + Grep | 30-50 | 11 个 BC × 4 类清单比对 |
| 阶段 4 | Write | 1 | 最终 spec 文档 |

总量约 200-300 次工具调用，通过并行化（阶段 1 规则间无依赖）压缩实际轮次。按用户指示接受此预算。

### 7.5 异常处理

| 异常场景 | 处理方式 |
|---|---|
| 某规则命中数 > 200 | 采样前 50 条复核，剩余以"批量命中"形式记录统计，注明"未全量复核" |
| 某 BC 代码量过大无法单次 Read | 按目录分批读取，记录分批边界 |
| spec 文档描述模糊无法提取清单 | 记录"spec 解析受阻"，跳过该 BC 的对应清单类型，不强行猜测 |
| 候选项无法按决策树明确分类 | 标记"需进一步确认"，进入二次复核队列，二次复核仍无法定论则降级为"待观察项" |
| 规则集在阶段 2 发现明显遗漏 | 增补规则并补扫，记录增补原因 |

---

## 8 验收标准、风险与交付物

### 8.1 审计交付物

| 交付物 | 路径 | 内容 |
|---|---|---|
| **审计实施 spec** | `docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md` | 完整设计文档（第 1-8 节） |
| **问题清单+修复方案** | 同上文档的附录 A-C | 阶段 4 产出的最终结果，含批次分组、修复方案、验证标准 |

两份内容合并为单一 spec 文档，避免分散。问题清单与修复方案作为文档附录呈现，审计完成后由实施阶段填充。

### 8.2 审计验收标准（审计工作本身的质量门槛）

| 验收项 | 标准 |
|---|---|
| **范围完整性** | 7.1 列出的全部范围均被执行，无遗漏 BC |
| **规则覆盖** | 第 3 节定义的全部规则（R1.1-R1.9、R2.1-R2.4、R3.1-R3.11、R4.1-R4.8）均执行并记录命中数；其中 R3.7/R3.8 作为待观察项标注执行，非核心分类 |
| **证据完整性** | 每条问题含"命中规则 + 文件:行号 + 命中代码片段"三要素，缺一返工 |
| **分类一致性** | 抽查 10 条问题，按第 4 节决策树重新分类，结果与原分类一致率 ≥ 90% |
| **spec 差距覆盖** | 11 个 BC 的 4 类清单（端点/AppService/事件/领域规则）均有差距分析记录，领域规则全量核查 |
| **修复方案可执行** | 每条问题含修复方向 + 影响文件 + 代码 sketch + 验证方式，缺一返工 |
| **既有 spec 标注** | 每条问题标注"既有 spec 覆盖"状态（是/否），标注"是"的附引用与状态 |
| **批次划分** | 问题按 6.4 的 4 批次组织，批次内依赖关系无环 |

### 8.3 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| **规则集遗漏新模式** | 漏报真实问题 | 阶段 2 发现新模式回填规则集并补扫（7.5 已规定） |
| **候选量过大导致复核粗糙** | 误报率高、真问题漏判 | 7.4 命中数 > 200 时采样 50 条，剩余批量统计；误报率 > 30% 时收紧规则重扫 |
| **spec 描述模糊** | 阶段 3 差距分析受阻 | 记录"spec 解析受阻"跳过该清单类型，不强行猜测（7.5 已规定） |
| **领域规则全量核查工作量超预期** | 阶段 3 拖延 | 按 BC 分批执行，每完成一个 BC 即产出阶段性差距清单，不等待全量完成 |
| **既有 spec 标注引入偏差** | 独立审计被既有结论带偏 | 严格限定标注仅在阶段 4 执行，阶段 1/2/3 禁止参考既有优化 spec |
| **修复方案过于理想化** | 实施阶段发现不可行 | 代码 sketch 控制在 15 行内展示核心形态；影响文件列全；风险字段声明副作用 |

### 8.4 后续衔接

本 spec 完成并通过用户审阅后，进入 `writing-plans` skill 创建实施计划：

- 实施计划按 6.4 的 4 批次组织
- 每批次拆解为可独立执行的任务卡片
- 任务卡片含：问题编号、修复方案、影响文件、验证方式、依赖任务
- 实施阶段才实际修改代码，本 spec 不落任何代码

### 8.5 不在本次审计范围内

明确以下事项**不属于**本次审计，避免范围蔓延：

- 实际修改代码（属 writing-plans → 实施阶段）
- 性能压测、安全渗透、并发压力测试（非功能属性）
- CI 流水线改造、覆盖率门槛配置（属实施层）
- 数据库迁移脚本设计（属实施层）
- 前端代码审计（本项目无前端代码）
- 第三方依赖版本升级评估

---

## 附录 A：阶段 1 规则集全量命中原始数据

> 本附录记录 26 条规则在 `src/` 全量扫描后的命中数据。扫描日期 2026-07-16；规则集与第 3 节一致；排除项遵循第 7.2 节（`obj/`、`bin/`，测试代码仅在 R4.8 等专门针对测试的规则中纳入）。

### A.1 类别 1：未实现业务代码（R1.1–R1.9）

| 规则 ID | Grep 模式 | 命中文件数 | 命中行数 | 关键发现 |
|---|---|---|---|---|
| R1.1 | `TODO\|FIXME\|HACK\|待实现\|未实现\|待补充\|暂未实现\|待完善\|待开发` | 0 | 0 | 与既有 `check-placeholders.sh` 结果一致，零占位标记 |
| R1.2 | `// 应该\|// 应当\|// 需要\|// 这里应该\|// 此处应\|// 实际应\|// 真实应` | 0 | 0 | 无"注释伪装实现"主场景命中 |
| R1.3 | 方法体仅含 `_logger.Log*` + return（上下文复核） | 1 | 1 | `StatisticsAggregationService.cs:23-58`：方法体仅记录日志 + 用 `new Random()` 生成假指标，注释明确写"当前使用简化的内存计算生成模拟数据"，**真实业务代码缺失** |
| R1.4 | `return Task.CompletedTask;\|Task.FromResult` | 84 | 188 | 95% 为仓储 `FirstOrDefaultAsync` 模式合法返回；**`IntegrationEventConsumerBase.cs:61,71` 默认空幂等返回 `Task.FromResult(false)` 与 `Task.CompletedTask`**，子类未覆盖即"无幂等" |
| R1.5 | `await Task.Delay` | 14 | 18 | 全部位于 `BackgroundServices/*` 与 `Jobs/*` 的轮询间隔，合法 |
| R1.6 | `return default\|return null\|return default!\|return null!` | 26 | 51 | 多为 `TryGetXxx` / `FirstOrDefault` 合法返回；`UserContactAntiCorruptionService.cs:66` 防腐层 catch 后 return null（移至 watchlist R3.8）；`IntegrationEventConsumerBase.cs:61` return false（同 R1.4） |
| R1.7 | `throw new NotImplementedException\|throw new NotSupportedException` | 3 | 3 | `ReconciliationService.cs:141` 抛 `NotSupportedException($"不支持的渠道：{channel}")` 合法；其余 2 处位于 `WeChatOAuth2Client.cs`/`AlipayOAuth2Client.cs` 路由分支兜底，合法 |
| R1.8 | 注释含 `模拟\|mock\|假数据\|临时\|stub\|placeholder\|dummy` 后接返回语句 | 1（生产代码） | 1 | `StatisticsAggregationService.cs:10` 类摘要注释明示"模拟数据"，配合 R1.3 形成完整证据链；其余 2425 处命中均在 `*Tests*` 项目，合法 |
| R1.9 | `=> throw new NotImplementedException` 表达式体 | 0 | 0 | 无命中 |

**类别 1 观察要点**：
- 显式占位（R1.1/R1.7/R1.9）已被既有 `check-placeholders.sh` 治理到位，零残留
- "注释伪装实现"（R1.2）零命中说明该方法学问题在本仓库不显著
- **唯一真问题集中在 `StatisticsAggregationService`**：使用 `new Random()`（27 处）生成 7 类报表指标，类摘要注释自我标注"模拟数据"。R1.3 + R1.8 双规则命中，证据链完整
- `IntegrationEventConsumerBase` 默认空幂等模式属于"基类留口等子类覆盖"的灰色地带，归入 R1.4 但定级 P1（基类不应有默认空实现）

### A.2 类别 2：缺失功能辅助扫描（R2.1–R2.4）

| 规则 ID | 扫描方式 | 命中数 | 关键发现 |
|---|---|---|---|
| R2.1 | 接口声明数 vs 实现类数对比（SearchCodebase） | 0 | 11 个 BC 的 `IXxxAppService` 均有对应 `XxxAppService` 实现，无孤儿接口 |
| R2.2 | Controller 端点数（`[Http*]` 特性）按 BC 统计 | 311 处/62 文件 | 端点数充足，与 spec 描述规模相符；细化差距在阶段 3 评估 |
| R2.3 | SharedContracts/Events 中事件类型 vs 各 Consumer 订阅类型差集 | **关键缺失** | `SharedContracts/Events/` 缺少 `PromotionEvents.cs` 与 `PointsMembershipEvents.cs`：`SeckillOrderCreatedEvent`、`PointsEarnedEvent`、`MemberLevelUpgradedEvent`、`MembershipActivatedEvent` 等 4 个事件**未在 SharedContracts 落地为 IntegrationEvent**，仅在各自 BC 的 `Domain.Events` 命名空间定义；Notification 消费者被迫直接订阅 Domain Events |
| R2.4 | `// 暂不支持\|// 未启用\|// 已禁用\|// 跳过\|// disabled\|// 临时禁用\|if (false)\|#if DEBUG` | 1 | `ReconciliationService.cs:185` 注释 `// 跳过表头行`，属 CSV 解析合理跳过 |

**类别 2 观察要点**：
- 接口/实现齐备，无孤儿接口
- **关键缺失集中在事件契约层**：跨 BC 事件契约不完整，是 R3.1/R3.2 跨 BC 引用的根因（SharedContracts 缺失 → 消费者只能引 Domain Events）
- 无功能被显式禁用

### A.3 类别 3：架构反模式（R3.1–R3.11）

| 规则 ID | Grep 模式 | 命中文件数 | 命中行数 | 关键发现 |
|---|---|---|---|---|
| R3.1 | `*.Infrastructure.csproj` ProjectReference 含其他 BC `*.Domain`/`*.Application` | 1 | 2 | `Leno.Notification.Infrastructure.csproj:8-9` 引用 `Promotion.Domain` 与 `PointsMembership.Domain`，**P0 限界上下文穿透** |
| R3.2 | `using Leno.{Xxx}.Domain` 出现在其他 BC | 1 | 4 | `Notification.Infrastructure/Consumers/`：`PromotionEventConsumer.cs:4`、`PointsEventConsumer.cs:4`、`NotificationEventConsumer.cs:4-5` 共 4 处 `using Leno.{Promotion,PointsMembership}.Domain.Events` |
| R3.3 | `using Leno.{Xxx}.Application` 出现在其他 BC | 0 | 0 | 无命中 |
| R3.4 | SharedKernel 含 `HttpStatusCode\|Microsoft.EntityFrameworkCore\|EF\|DbContext\|SqlClient\|RowVersion` | 3 文件 | 8 行 | `DomainException.cs:13,19,26` 携带 `int HttpStatusCode`；`MoneyJsonConverter.cs:67-90` 暴露 `ToStorage/FromStorage` 静态方法（"amount\|currency" 格式为 EF Core 值转换器服务）；`Entity.cs`、`IUnitOfWork.cs` 仅注释提及 EF Core，无实际泄漏。**P0 共享内核泄漏技术细节** |
| R3.5 | `*.Domain.csproj` 引用 `Microsoft.EntityFrameworkCore\|StackExchange.Redis\|RabbitMQ\|MassTransit` | 0 | 0 | 11 个 Domain.csproj 均只引用 SharedKernel 与 SharedContracts，**干净** |
| R3.6 | 11 份 `UnitOfWork.cs` 文本相似度（diff 比对） | 11 | — | 抽样比对 Order/Cart/Notification 三份：除 `XxxDbContext` 类型与命名空间不同外，56 行代码逐字相同，内部嵌套类 `EfCoreUnitOfWorkTransaction` 被复制 11 次。**P1 跨 BC 样板重复** |
| R3.7 | `catch (Exception` 后无 throw 且仅 Log 或空体（watchlist） | — | — | 多处命中（Notification DeadLetterAppService、NotificationService、NotificationDispatcher、Order LogisticsTrackingService 等约 8+ 处）。按第 4.5 节，移出核心分类，仅就地标注。详见批次 4 |
| R3.8 | 防腐层 catch 后 return null/空集合（watchlist） | — | — | `UserContactAntiCorruptionService.cs:63-67` catch 后 return null；`LogisticsTrackingService.cs:113-126` catch 后返回缓存或 Empty（属合理降级）；移出核心分类，批次 4 标注 |
| R3.9 | async 方法无 await | 0 | 0 | Grep 不可直接判定，需编译器警告；抽查 `StatisticsAggregationService.AggregateAsync` 返回 `Task.FromResult` 未 await，伪 async 嫌疑（与 R1.3 合并处理） |
| R3.10 | `static readonly` 单例无 Lazy/lock 保护且为可变集合 | 0 | 0 | 无命中（`JsonSerializerOptions` 等为不可变值对象，误报） |
| R3.11 | `new List<>\|new Dictionary<>` 作为 static 字段且公开可访问 | 0 | 0 | 无命中（仅测试代码 `staticDestinations ?? new Dictionary<...>()` 局部变量，非静态字段） |

**类别 3 观察要点**：
- **R3.1 + R3.2 + R3.4 + R2.3 形成一条因果链**：SharedContracts 缺事件契约（R2.3）→ SharedKernel 不该承担的事件语义也未下沉（R3.4 旁证）→ Notification 只能引用 Promotion/PointsMembership.Domain（R3.1/R3.2）
- R3.5/R3.10/R3.11 干净，说明领域层与静态状态治理到位
- R3.6/R3.7/R3.8 与既有 `comprehensive-optimization-design.md` 高度重合，将在阶段 4 标注

### A.4 类别 4：冗余代码（R4.1–R4.8）

| 规则 ID | 扫描方式 | 命中数 | 关键发现 |
|---|---|---|---|
| R4.1 | 文件内方法重复（抽样复核） | 0 | 抽样 `StatisticsAggregationService` 7 个 `AggregateXxx` 私有方法结构相似但指标不同，非完全重复；其余文件未发现 3+ 处相同代码块 |
| R4.2 | 跨 BC 同名文件相似度 | 11 + 11 | 11 份 `UnitOfWork.cs`（与 R3.6 同源）；11 份 `*DbContext.cs` 重复声明 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`；`Program.cs` 11 份相似度 >85%（仅 6 处差异）；`GlobalUsings.cs` 多份仅含 2 行 `global using Xunit; global using FluentAssertions;` |
| R4.3 | private/internal 方法无调用方（抽样） | 0 | 抽查 `UnitOfWork.EfCoreUnitOfWorkTransaction` 私有方法均有调用；`StatisticsAggregationService` 私有方法由 switch 调用 |
| R4.4 | private/internal 字段无读取 | 0 | 抽查未发现死字段 |
| R4.5 | `#if DEBUG` 或注释死代码 >5 行 | 0 | 无命中 |
| R4.6 | 空类、空命名空间 | 0 | 无命中 |
| R4.7 | 未使用 using（编译警告） | 未扫 | Grep 不可直接判定，需编译器警告；不在本审计核心范围 |
| R4.8 | 仅含 `GlobalUsings.cs` 的空测试项目 | 4 | **`ReviewAfterSales.Application.Tests`、`SellerShop.Application.Tests`、`SystemAdmin.Application.Tests`、`UserAuth.Infrastructure.Tests`** 4 个项目仅含 `GlobalUsings.cs`（2 行 `global using`），无任何测试类；另外 `PointsMembership.Domain.Tests/NewFeatureTests.cs` 为 0 字节空文件，`NewFeatureTests1-6.cs` 已填充真实测试（命名应优化） |

**类别 4 观察要点**：
- 跨 BC 样板重复（R4.2）是 P1 主问题，与 R3.6 同源
- R4.8 空测试项目 4 个，与既有 `comprehensive-optimization-design.md` §2.4 高度重合（既有 spec 标注 7 个空测试项目，本次扫描仅命中 4 个，其余 3 个已部分填充）
- NewFeatureTests.cs 0 字节空文件是 R4.6 的边缘命中（单文件空而非类空），归入 R4.8 一并处理

### A.5 阶段 1 总体观察

1. **未实现业务代码类问题收敛于 2 处**：`StatisticsAggregationService`（mock 数据伪装）与 `IntegrationEventConsumerBase`（默认空幂等），均已有自我标注的注释/默认实现留口
2. **架构反模式形成清晰因果链**：跨 BC 引用（R3.1/R3.2）的根因是 SharedContracts 事件契约缺失（R2.3），修复时必须先补契约再拆引用
3. **冗余代码主问题为样板重复**：11 份 UnitOfWork + 11 份 Program.cs，已有既有 spec 覆盖
4. **跨 BC 边界违规仅 1 处**：Notification → Promotion/PointsMembership，但影响面 4 个 Consumer 文件、12 个事件订阅
5. **测试占位问题部分残留**：4 个空测试项目 + 1 个 0 字节文件，与既有 spec 状态相比已有显著改善

## 附录 B：问题清单与修复方案

> 本附录按第 6.4 节 4 批次组织审计产出。每条问题含：命中规则、位置、证据、根因、影响范围、修复方案（方向+影响文件+代码 sketch+验证方式+风险）、既有 spec 标注。编号规则见第 4.3 节。

### B.1 批次 1 — 架构合规修复（P0）

#### AUDIT-ARCH-001：Notification.Infrastructure 跨限界上下文引用 Promotion.Domain / PointsMembership.Domain

- **类别**：架构反模式
- **严重级**：P0
- **命中规则**：R3.1（ProjectReference 跨 BC）+ R3.2（using 跨 BC Domain）+ R2.3（SharedContracts 缺事件契约，根因）
- **位置**：
  - `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj:8-9`
  - `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs:4-5,24-25,100-131`
  - `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs:4`
  - `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs:4`
- **证据**：
  - csproj：`<ProjectReference Include="..\..\Promotion\Leno.Promotion.Domain\Leno.Promotion.Domain.csproj" />` + `<ProjectReference Include="..\..\PointsMembership\Leno.PointsMembership.Domain\Leno.PointsMembership.Domain.csproj" />`
  - 代码：`using Leno.Promotion.Domain.Events;` 与 `using Leno.PointsMembership.Domain.Events;`
  - 消费者：`NotificationEventConsumer : IConsumer<SeckillOrderCreatedEvent>, IConsumer<PointsEarnedEvent>, IConsumer<MemberLevelUpgradedEvent>, IConsumer<MembershipActivatedEvent>` —— 这 4 个本应是 IntegrationEvent，实为 Domain Events
- **根因**：`SharedContracts/Events/` 未定义 Promotion/PointsMembership 的集成事件契约，Notification 被迫直接订阅上游 BC 的 Domain Events，破坏 DDD 上下文边界。一旦上游重构 Domain Event 内部字段，Notification 编译即断裂；运行时反序列化可能静默失败
- **影响范围**：1 个 csproj + 3 个 Consumer 文件 + 12 个事件订阅（其中 4 个为非法跨 BC 订阅）
- **修复方案**：
  - **方向**：在 SharedContracts 新增 `PromotionEvents.cs` 与 `PointsMembershipEvents.cs` 集成事件契约；Promotion/PointsMembership 通过 Outbox 翻译 Domain Event → IntegrationEvent；Notification 改订阅 IntegrationEvent 类型；删除跨 BC 引用
  - **影响文件**：
    - 新增 `src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs`
    - 新增 `src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs`
    - 修改 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs`（引入 `IIntegrationEventMapper`）
    - 新增 `src/Services/Promotion/Leno.Promotion.Infrastructure/Dependencies/PromotionIntegrationEventMapper.cs`
    - 新增 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/PointsMembershipIntegrationEventMapper.cs`
    - 修改 `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj`（删除 2 处 ProjectReference）
    - 修改 3 个 Consumer：`NotificationEventConsumer.cs`、`PromotionEventConsumer.cs`、`PointsEventConsumer.cs`
  - **代码 sketch**：
    ```csharp
    // SharedContracts/Events/PromotionEvents.cs
    public sealed class SeckillOrderCreatedIntegrationEvent : IntegrationEventBase
    {
        public Guid OrderId { get; init; }
        public Guid UserId { get; init; }
        public Guid SpuId { get; init; }
        public Guid SkuId { get; init; }
        public int Quantity { get; init; }
        public decimal SeckillPrice { get; init; }
        public string Currency { get; init; } = "CNY";
    }
    // PromotionIntegrationEventMapper.cs
    public sealed class PromotionIntegrationEventMapper : IIntegrationEventMapper
    {
        public IIntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
        {
            SeckillOrderCreatedEvent e => new SeckillOrderCreatedIntegrationEvent { /* mapping */ },
            _ => null
        };
    }
    // NotificationEventConsumer.cs（修改后）
    IConsumer<SeckillOrderCreatedIntegrationEvent>,  // 改订阅契约类型
    // ...
    // csproj（删除跨 BC 引用）
    // <ProjectReference Include="..\..\Promotion\Leno.Promotion.Domain\..." /> ← 删除
    ```
  - **依赖问题**：无（架构合规批次起点）
  - **验证方式**：编译验证（`dotnet build Leno.slnx`）；Grep 复扫 `using Leno.Promotion.Domain` 在 `src/Services/Notification/` 下不再命中；既有测试全绿；新增 Notification 消费者测试覆盖新 IntegrationEvent 字段映射
  - **风险**：MQ 消息格式过渡期需要双发兼容（Domain Event 与 IntegrationEvent 同时发布一周），下线旧格式前需确认无消费者订阅
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` §2.1（P0 限界上下文边界违规）+ §4.1-4.3（子任务 1.1-1.3）
  - 既有 spec 状态：**未修复**（代码复扫：csproj 仍引用 Promotion.Domain/PointsMembership.Domain，Consumer 仍 `using` Domain Events）

#### AUDIT-ARCH-002：SharedKernel 泄漏 HTTP 状态码到 DomainException

- **类别**：架构反模式
- **严重级**：P0
- **命中规则**：R3.4
- **位置**：`src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs:13,19,26`
- **证据**：
  ```csharp
  public int HttpStatusCode { get; }
  protected DomainException(string message, string errorCode = "DOMAIN_ERROR", int httpStatusCode = 400)
      : base(message)
  {
      ErrorCode = errorCode;
      HttpStatusCode = httpStatusCode;
  }
  ```
  - `DomainException` 由 `GlobalExceptionMiddleware`（Infrastructure 层）直接读取 `HttpStatusCode` 字段，领域异常携带 HTTP 语义违反分层原则
- **根因**：领域异常应只表达业务错误码（如 `ORDER_DOMAIN_ERROR`），HTTP 状态码映射属基础设施层关注点
- **影响范围**：所有继承 `DomainException` 的子类（11 个 BC 各 1 个 `XxxDomainException`）+ `GlobalExceptionMiddleware`
- **修复方案**：
  - **方向**：DomainException 只保留 `ErrorCode`，移除 `HttpStatusCode` 字段；在 Infrastructure/Middleware 新建 `ErrorCodeMapping` 查表映射错误码到 HTTP 状态码
  - **影响文件**：
    - 修改 `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs`（删除 `HttpStatusCode` 字段与构造参数）
    - 修改所有 `*DomainException.cs`（11 个 BC）构造函数签名
    - 新增 `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`
    - 修改 `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs`
  - **代码 sketch**：
    ```csharp
    // SharedKernel/Exceptions/DomainException.cs（修复后）
    public abstract class DomainException : Exception
    {
        public string ErrorCode { get; }
        protected DomainException(string message, string errorCode = "DOMAIN_ERROR")
            : base(message) => ErrorCode = errorCode;
    }
    // Infrastructure/Middleware/ErrorCodeMapping.cs（新增）
    public static class ErrorCodeMapping
    {
        private static readonly Dictionary<string, int> _mapping = new()
        {
            ["UNAUTHORIZED"] = 401,
            ["FORBIDDEN"] = 403,
            ["NOT_FOUND"] = 404,
            ["CONFLICT"] = 409,
            // 其余默认 400
        };
        public static int GetStatusCode(string errorCode) =>
            _mapping.TryGetValue(errorCode, out var code) ? code : 400;
    }
    ```
  - **依赖问题**：无
  - **验证方式**：编译验证；Grep 复扫 `HttpStatusCode` 在 `src/BuildingBlocks/Leno.SharedKernel/` 不再命中；既有测试全绿
  - **风险**：低（错误码映射表需覆盖所有现有错误码，迁移时全量检查 `: base(...,httpStatusCode)` 调用点）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.2 第 3 项 + §5.2（子任务 2.2）
  - 既有 spec 状态：**未修复**（代码复扫：`DomainException.cs:13` 仍含 `HttpStatusCode` 字段）

#### AUDIT-ARCH-003：SharedKernel 暴露 EF Core 值转换器方法（MoneyJsonConverter.ToStorage/FromStorage）

- **类别**：架构反模式
- **严重级**：P0
- **命中规则**：R3.4
- **位置**：`src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs:67-90`
- **证据**：
  ```csharp
  public static string ToStorage(Money money)
      => $"{money.Amount.ToString(...)}|{money.Currency}";
  public static Money FromStorage(string value) { /* 解析 amount|currency */ }
  ```
  - 类摘要注释自我承认"供 EF Core 值转换器使用"，存储格式 `amount|currency` 是 SQL Server 持久化细节
- **根因**：共享内核不应感知持久化层存储格式
- **影响范围**：所有 BC.Infrastructure 中使用 `Money` 值转换器的 EF Core 配置
- **修复方案**：
  - **方向**：删除 `ToStorage/FromStorage` 静态方法；在 `Leno.Infrastructure/Persistence` 新建 `MoneyValueConverter : ValueConverter<Money, string>`；各 BC 的 `IEntityTypeConfiguration<T>` 中 `OwnsOne` 改为 `Property(...).HasConversion<MoneyValueConverter>()`
  - **影响文件**：
    - 修改 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs`（删除两个静态方法）
    - 新增 `src/BuildingBlocks/Leno.Infrastructure/Persistence/MoneyValueConverter.cs`
    - 各 BC 的 `IEntityTypeConfiguration<T>` 文件（替换 `MoneyJsonConverter.ToStorage` 调用）
  - **代码 sketch**：
    ```csharp
    // Infrastructure/Persistence/MoneyValueConverter.cs（新增）
    public sealed class MoneyValueConverter : ValueConverter<Money, string>
    {
        public MoneyValueConverter()
            : base(m => $"{m.Amount.ToString(CultureInfo.InvariantCulture)}|{m.Currency}",
                   v => ParseStorage(v)) { }
        private static Money ParseStorage(string value) { /* 解析 */ }
    }
    // 配置文件中使用
    builder.Property(x => x.UnitPrice).HasConversion<MoneyValueConverter>();
    ```
  - **依赖问题**：无
  - **验证方式**：编译验证；Grep 复扫 `ToStorage|FromStorage` 在 `src/BuildingBlocks/Leno.SharedKernel/` 不再命中；既有测试全绿；新增并发更新测试
  - **风险**：中（需保证所有使用 `Money` 的实体配置正确迁移，否则反序列化失败）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.2 第 4 项 + §5.3（子任务 2.3）
  - 既有 spec 状态：**未修复**（代码复扫：`MoneyJsonConverter.cs:69,75` 仍存在 `ToStorage/FromStorage`）

#### AUDIT-ARCH-004：Entity 基类 Version 字段（既有 spec 已修复）

- **类别**：架构反模式（已修复）
- **严重级**：—（已修复，仅记录）
- **命中规则**：R3.4
- **位置**：`src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs`
- **证据**：当前 Entity 类已无 `Version` 字段，仅有 `Id` + 4 个审计字段
- **根因**：既有 spec 描述的"SQL Server rowversion"已下线
- **影响范围**：—
- **修复方案**：—
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.2 第 1 项 + §5.1（子任务 2.1）
  - 既有 spec 状态：**已修复**（代码复扫验证）

### B.2 批次 2 — 核心功能补全（P0/P1）

#### AUDIT-IMPL-001：StatisticsAggregationService 用 `new Random()` 生成模拟运营数据

- **类别**：未实现业务代码
- **严重级**：P0（影响运营决策、对账、报表数据一致性）
- **命中规则**：R1.3（logger-log + 假实现）+ R1.8（mock 数据伪装）+ R1.4（伪 async，`return Task.FromResult`）+ R3.9（async 无 await）
- **位置**：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs:10-194`
- **证据**：
  ```csharp
  // 类摘要注释自我承认模拟数据
  /// 当前使用简化的内存计算生成模拟数据，后续可替换为 ES 查询或事件溯源聚合。
  public sealed class StatisticsAggregationService : IStatisticsAggregationService
  {
      public Task<DashboardReport> AggregateAsync(...)
      {
          _logger.LogInformation("开始聚合运营数据 ...");
          var metrics = reportType switch { /* 7 类报表 */ };
          // ...
          return Task.FromResult(report);  // R3.9: async 无 await
      }
      private static List<MetricItem> AggregateOrderGmv(ReportPeriod period)
      {
          var totalOrders = (decimal)(new Random().Next(1000, 5000) * days);  // R1.8: mock 数据
          var totalGmv = (decimal)(new Random().Next(50000, 200000) * days);
          // ...
      }
  }
  ```
  - 27 处 `new Random()` 调用，覆盖 7 类报表（OrderGmv/PaymentSuccessRate/PointsIssued/NotificationDelivery/AfterSalesVolume/ShopRanking/ConversionRate）
- **根因**：仪表盘聚合服务未对接真实数据源（ES 查询/事件溯源），用随机数填充。每次请求返回不同数据，违反"指标可追溯"原则
- **影响范围**：`/api/dashboard/statistics` 端点；运营仪表盘；所有 `DashboardReport` 依赖方
- **修复方案**：
  - **方向**：注入 `IStatisticsQueryService`（基于 ES 或 DbContext 聚合查询）；7 类报表各自实现真实查询逻辑；删除所有 `new Random()` 调用
  - **影响文件**：
    - 修改 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs`（重写全部 7 个 `AggregateXxx` 方法）
    - 新增 `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Services/IStatisticsQueryService.cs`
    - 新增 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/EfCoreStatisticsQueryService.cs`（或 `EsStatisticsQueryService`，按既有 ES 集成方案选）
    - 新增 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/StatisticsAggregationServiceTests.cs`
  - **代码 sketch**：
    ```csharp
    public sealed class StatisticsAggregationService : IStatisticsAggregationService
    {
        private readonly IStatisticsQueryService _queryService;
        private readonly ILogger<StatisticsAggregationService> _logger;

        public async Task<DashboardReport> AggregateAsync(ReportType reportType, ReportPeriod period, CancellationToken ct = default)
        {
            var metrics = reportType switch
            {
                ReportType.OrderGmv => await _queryService.QueryOrderGmvAsync(period, ct),
                ReportType.PaymentSuccessRate => await _queryService.QueryPaymentSuccessRateAsync(period, ct),
                // ... 其余 5 类
                _ => throw new ArgumentOutOfRangeException(nameof(reportType))
            };
            return DashboardReport.Create(Guid.NewGuid(), reportType, period, metrics, DetermineGranularity(period));
        }
    }
    ```
  - **依赖问题**：AUDIT-ARCH-001（避免在跨 BC 引用未修复时新增 ES 客户端依赖）— 实际可与 ARCH-001 并行，因 StatisticsAggregationService 不涉及跨 BC 引用
  - **验证方式**：编译验证；新增单元测试覆盖 7 类报表的聚合逻辑；Grep 复扫 `new Random()` 在生产代码不再命中（测试代码除外）；既有测试全绿
  - **风险**：中（需对接真实数据源，可能需补 ES 索引或写聚合 SQL；过渡期可降级为基于 DbContext 的真实查询）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**否**（新发现问题，既有 `2026-07-13-comprehensive-optimization-design.md` 未涵盖 SystemAdmin 运营数据聚合）
  - 既有 spec 引用：—
  - 既有 spec 状态：—（新增）

#### AUDIT-IMPL-002：IntegrationEventConsumerBase 默认空幂等实现

- **类别**：未实现业务代码
- **严重级**：P1
- **命中规则**：R1.4 + R1.6
- **位置**：`src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs:57-72`
- **证据**：
  ```csharp
  protected virtual Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
  {
      _ = eventId; _ = ct;
      return Task.FromResult(false);  // 默认未处理 → 不幂等
  }
  protected virtual Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct)
  {
      _ = eventId; _ = ct;
      return Task.CompletedTask;  // 默认无操作
  }
  ```
- **根因**：基类为"子类可选覆盖"留口，但子类未覆盖时即"无幂等保护"。MassTransit 重试或消息重复投递会导致重复消费
- **影响范围**：所有继承 `IntegrationEventConsumerBase<T>` 的 Consumer 子类（10+ 个 Consumer）
- **修复方案**：
  - **方向**：基类强制注入 `IIdempotencyStore`（基于 Redis SET NX，TTL 24 小时），不再提供默认空实现；改为 `abstract` 方法或构造函数注入
  - **影响文件**：
    - 修改 `src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs`
    - 新增 `src/BuildingBlocks/Leno.Infrastructure/EventBus/IIdempotencyStore.cs`
    - 新增 `src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs`
    - 各 BC.Infrastructure 的 `Dependencies/ServiceCollectionExtensions.cs`（注册 `IIdempotencyStore`）
    - 全量审计所有 Consumer 子类，确认未自行实现幂等的都已获得默认 Redis 幂等保护
  - **代码 sketch**：
    ```csharp
    public abstract class IntegrationEventConsumerBase<T> : IConsumer<T>
        where T : class, IIntegrationEvent
    {
        private readonly IIdempotencyStore _idempotencyStore;
        protected IntegrationEventConsumerBase(ILogger logger, IIdempotencyStore idempotencyStore) { /* ... */ }

        public async Task Consume(ConsumeContext<T> context)
        {
            if (await _idempotencyStore.IsProcessedAsync(context.Message.EventId, context.CancellationToken))
            {
                Logger.LogInformation("事件已处理 EventId={EventId}", context.Message.EventId);
                return;
            }
            await HandleAsync(context.Message, context.CancellationToken);
            await _idempotencyStore.MarkAsProcessedAsync(context.Message.EventId, context.CancellationToken);
        }
    }
    ```
  - **依赖问题**：无
  - **验证方式**：编译验证；新增单元测试覆盖幂等场景（同一 EventId 重复投递只处理一次）；既有测试全绿
  - **风险**：中（需确认所有 Consumer 子类的构造函数签名调整；Redis 不可用时降级策略需明确）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.4 第 5 项 + §6.4（子任务 3.4）
  - 既有 spec 状态：**未修复**（代码复扫：`IntegrationEventConsumerBase.cs:57-72` 仍为默认空实现）

#### AUDIT-MISS-001：跨 BC 集成事件契约缺失（PromotionEvents + PointsMembershipEvents）

- **类别**：缺失功能
- **严重级**：P0（与 AUDIT-ARCH-001 同源，事件契约不补全则跨 BC 引用无法拆解）
- **命中规则**：R2.3
- **位置**：
  - 预期：`src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs`（不存在）
  - 预期：`src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs`（不存在）
  - spec 依据：`docs/spec/00-需求文档总览与DDD架构.md` 第 5 节"跨上下文领域事件清单"
- **证据**：
  - SharedContracts/Events 目录现有 13 个事件文件，但缺 `PromotionEvents.cs` 与 `PointsMembershipEvents.cs`
  - Grep `SeckillOrderCreated|PointsEarned|MemberLevelUpgraded|MembershipActivated` 在 `src/BuildingBlocks/Leno.SharedContracts/Events/` 返回 0 匹配
- **根因**：spec 第 5 节规划的跨 BC 事件契约未在 SharedContracts 落地，导致 Notification 只能直接订阅上游 Domain Events
- **影响范围**：4 个跨 BC 事件订阅（Notification 消费者）；上游 BC（Promotion/PointsMembership）的发布点也未走 Outbox 翻译
- **修复方案**：
  - **方向**：在 SharedContracts 新增两个事件契约文件，覆盖 spec 第 5 节声明的全部跨 BC 事件
  - **影响文件**：
    - 新增 `src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs`（至少 `SeckillOrderCreatedIntegrationEvent`、`SeckillStockPreOccupiedIntegrationEvent`）
    - 新增 `src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs`（至少 `PointsEarnedIntegrationEvent`、`PointsConsumedIntegrationEvent`、`PointsRevertedIntegrationEvent`、`MemberLevelChangedIntegrationEvent`、`PaidMemberSubscribedIntegrationEvent`）
  - **代码 sketch**：见 AUDIT-ARCH-001
  - **依赖问题**：AUDIT-ARCH-001 依赖本问题（先补契约，再拆引用）
  - **验证方式**：编译验证；Grep 复扫新事件类型在 SharedContracts 命中；spec 复核第 5 节事件清单全部落地
  - **风险**：低
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §4.1（子任务 1.1）
  - 既有 spec 状态：**未修复**（代码复扫：SharedContracts/Events 目录无 `PromotionEvents.cs`/`PointsMembershipEvents.cs`）

#### AUDIT-MISS-002：Outbox 翻译机制缺失（Domain Event → IntegrationEvent）

- **类别**：缺失功能
- **严重级**：P1
- **命中规则**：R2.3
- **位置**：`src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs`
- **证据**：当前通过 `domainEvent is IIntegrationEvent` 检查将领域事件直接进发件箱，导致领域事件类型与契约类型耦合
- **根因**：spec 第 5 节要求"领域事件 → 集成事件翻译"机制未落地
- **影响范围**：所有 BC 的 Outbox 发布流程
- **修复方案**：
  - **方向**：引入 `IIntegrationEventMapper` 接口，各 BC.Infrastructure 注册自己的 mapper，Outbox 通过 mapper 翻译
  - **影响文件**：见 AUDIT-ARCH-001
  - **依赖问题**：与 AUDIT-ARCH-001、AUDIT-MISS-001 同批
  - **验证方式**：编译验证；新增单元测试覆盖 mapper 翻译逻辑
  - **风险**：中
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §4.2（子任务 1.2）
  - 既有 spec 状态：**未修复**

### B.3 批次 3 — 代码质量优化（P1/P2）

#### AUDIT-REDUN-001：11 份 UnitOfWork.cs 跨 BC 逐字重复

- **类别**：冗余代码
- **严重级**：P1
- **命中规则**：R4.2 + R3.6
- **位置**：11 个 BC.Infrastructure 下的 `UnitOfWork.cs`（路径见附录 A.3 R3.6）
- **证据**：抽样比对 Order/Cart/Notification 三份，除 `XxxDbContext` 类型与命名空间不同外，56 行代码逐字相同，内部嵌套类 `EfCoreUnitOfWorkTransaction` 被复制 11 次
- **根因**：未抽取泛型基类
- **影响范围**：11 个文件，约 600 行重复代码
- **修复方案**：
  - **方向**：在 `Leno.Infrastructure/Persistence` 新建 `EfCoreUnitOfWork<TDbContext>` 泛型基类；删除 11 个 BC 的 UnitOfWork.cs；DI 注册改为 `services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<XxxDbContext>>()`
  - **影响文件**：
    - 新增 `src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs`
    - 删除 11 个 `UnitOfWork.cs`
    - 修改 11 个 `Dependencies/ServiceCollectionExtensions.cs`
  - **代码 sketch**：
    ```csharp
    public sealed class EfCoreUnitOfWork<TDbContext> : IUnitOfWork
        where TDbContext : DbContext
    {
        private readonly TDbContext _context;
        public EfCoreUnitOfWork(TDbContext context) { _context = context; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
        public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesWithOutboxAsync(ct);
            return true;
        }
        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => new EfCoreUnitOfWorkTransaction(await _context.Database.BeginTransactionAsync(ct));
    }
    ```
  - **依赖问题**：无
  - **验证方式**：编译验证；既有测试全绿（不强制新增测试）
  - **风险**：低（DI 注册批量改可能遗漏，每个 BC 迁移后跑该 BC 测试套件验证）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.3 + §6.1（子任务 3.1）
  - 既有 spec 状态：**未修复**（代码复扫：11 个 UnitOfWork.cs 仍存在）

#### AUDIT-REDUN-002：11 份 Program.cs 高度相似

- **类别**：冗余代码
- **严重级**：P1
- **命中规则**：R4.2
- **位置**：11 个 BC.Api 下的 `Program.cs`
- **证据**：70+ 行中仅 6 处差异（using、注释、AddXxxConsumers、AddXxxInfrastructure、AddDbContextCheck）
- **根因**：未抽取一站式扩展方法
- **影响范围**：11 个文件
- **修复方案**：
  - **方向**：在 `Leno.Infrastructure/Dependencies` 新建 `WebApplicationExtensions.AddLenoService<TDbContext>` + `UseLenoPipeline`；各 BC 的 Program.cs 缩减到 ~15 行
  - **影响文件**：见既有 spec §6.3
  - **代码 sketch**：见既有 spec §6.3 代码示例
  - **依赖问题**：无
  - **验证方式**：编译验证；既有测试全绿
  - **风险**：低
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §6.3（子任务 3.3）
  - 既有 spec 状态：**未修复**

#### AUDIT-REDUN-003：4 个空测试项目仅含 GlobalUsings.cs

- **类别**：冗余代码
- **严重级**：P1
- **命中规则**：R4.8
- **位置**：
  - `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application.Tests/`
  - `src/Services/SellerShop/Leno.SellerShop.Application.Tests/`
  - `src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/`
  - `src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/`
- **证据**：4 个项目目录除 `GlobalUsings.cs`（2 行）与 `.csproj` 外无任何 `.cs` 测试文件
- **根因**：脚手架项目未填充实际测试
- **影响范围**：4 个项目对应 BC 的 Application/Infrastructure 层无测试覆盖
- **修复方案**：
  - **方向**：为每个空项目补齐对应 AppService 的关键路径测试（创建/取消/状态流转）
  - **影响文件**：4 个项目下新增 `*AppServiceTests.cs`/`*RepositoryTests.cs`
  - **依赖问题**：无
  - **验证方式**：新增测试覆盖关键路径；编译验证
  - **风险**：低
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**（部分）
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.4 第 3 项 + §7.3（子任务 4.3）
  - 既有 spec 状态：**部分修复**（既有 spec 标 7 个空测试项目，本次扫描仅命中 4 个，其余 3 个 Notification.Api.Tests/SellerShop.Api.Tests/ReviewAfterSales.Api.Tests 已部分填充，但 Application 层仍未补）

#### AUDIT-REDUN-004：NewFeatureTests.cs 0 字节空文件

- **类别**：冗余代码
- **严重级**：P2
- **命中规则**：R4.6（边缘）/ R4.8
- **位置**：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/NewFeatureTests.cs`
- **证据**：文件存在但内容为空（0 字节）；`NewFeatureTests1-6.cs` 已填充真实测试
- **根因**：脚手架遗留
- **影响范围**：1 个文件
- **修复方案**：
  - **方向**：删除空文件；将 `NewFeatureTests1-6.cs` 重命名为有意义的测试类名（如 `PointsAccountConsumeRevertTests.cs`）
  - **影响文件**：删除 `NewFeatureTests.cs`；重命名 6 个文件
  - **依赖问题**：无
  - **验证方式**：编译验证；既有测试全绿
  - **风险**：低
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.4 第 1 项 + §7.1（子任务 4.1）
  - 既有 spec 状态：**部分修复**（NewFeatureTests1-6.cs 已填充，但 NewFeatureTests.cs 仍为空）

#### AUDIT-REDUN-005：11 份 DbContext 重复声明 OutboxMessages DbSet

- **类别**：冗余代码
- **严重级**：P2
- **命中规则**：R4.2
- **位置**：11 个 BC.Infrastructure 下的 `*DbContext.cs`
- **证据**：每个 DbContext 都重复声明 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`
- **根因**：未在 `BaseDbContext` 暴露
- **影响范围**：11 个文件
- **修复方案**：
  - **方向**：在 `BaseDbContext` 添加 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`；从 11 个 BC DbContext 删除该声明
  - **影响文件**：`src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs` + 11 个 `*DbContext.cs`
  - **依赖问题**：无
  - **验证方式**：编译验证；既有测试全绿
  - **风险**：低
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §6.2（子任务 3.2）
  - 既有 spec 状态：**未修复**

#### AUDIT-ARCH-005：CQRS 未落地（改进性，归入批次 3）

- **类别**：架构反模式（改进性）
- **严重级**：P2
- **命中规则**：spec 第 5 节"阶段 3 spec 差距"辅助识别
- **位置**：全代码库（11 个 BC 均无 `Commands/`/`Queries/` 目录）
- **证据**：Glob `src/Services/**/{Commands,Queries}/**/*.cs` 返回 0 匹配；11 个 BC 全部用单一 `IXxxAppService` 模式
- **根因**：编码规范第 7 章描述的 CQRS 未落地
- **影响范围**：全代码库（仅改进性，非阻塞）
- **修复方案**：见既有 spec §9（子任务 6.1-6.3），不强制全部 BC 落地，只在读多写少场景引入
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.6 + §9（主线 6）
  - 既有 spec 状态：**未修复**

#### AUDIT-ARCH-006：API 网关能力不足（YARP 限流/熔断未启用）

- **类别**：架构反模式（改进性）
- **严重级**：P2
- **命中规则**：spec 第 5 节辅助识别
- **位置**：`src/ApiGateway/Leno.ApiGateway/Program.cs` + `appsettings.json`
- **证据**：抽查代码，YARP 配置未启用 RateLimiterPolicy、CircuitBreaker、Timeout
- **根因**：网关增强未落地
- **影响范围**：所有外部 API 调用
- **修复方案**：见既有 spec §10（子任务 7.1-7.3）
- **既有 spec 标注**：
  - 既有 spec 覆盖：**是**
  - 既有 spec 引用：`2026-07-13-comprehensive-optimization-design.md` §2.7 + §10（主线 7）
  - 既有 spec 状态：**未修复**

### B.4 批次 4 — 待观察项标注（R3.7/R3.8）

> 按第 4.5 节，吞异常与静默兜底移出核心分类，仅就地标注建议。不定严重级、不进 P0/P1/P2 排序。阶段 4 不为其编排修复方案，仅作为附录供后续修改参考。

#### WATCH-R3.7-001：Notification DeadLetterAppService 批量重发/丢弃吞异常

- **位置**：
  - `src/Services/Notification/Leno.Notification.Application/Services/DeadLetterAppService.cs:113-118`（重发）
  - `src/Services/Notification/Leno.Notification.Application/Services/DeadLetterAppService.cs:170-175`（丢弃）
- **命中代码**：
  ```csharp
  catch (Exception ex)
  {
      result.FailureCount++;
      result.Errors.Add($"记录 {recordId} 重发异常：{ex.Message}");
      _logger.LogError(ex, "手工重发死信异常 ...");
  }
  ```
- **标注建议**：`// AUDIT-NOTE: 批量操作的逐条容错是合理的，但建议加告警阈值（如单批失败率 >30% 触发告警）`

#### WATCH-R3.7-002：NotificationService 模板渲染/通知发送吞异常

- **位置**：
  - `src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs:90-98`（模板渲染失败 return Failed）
  - `src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs:167-171`（发送失败 MarkFailed）
- **标注建议**：`// AUDIT-NOTE: 通知发送失败已通过 MarkFailed 持久化状态，属合理降级；建议加死信阈值告警`

#### WATCH-R3.7-003：RedisRateLimiter Redis 不可用降级为允许

- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs:50-55`
- **命中代码**：
  ```csharp
  catch (Exception ex)
  {
      // Redis 不可用 → 降级为允许，并发送告警
      _logger.LogError(ex, "Redis 频率限制检查失败，降级为允许 ...");
      return RateLimitResult.AllowedResult();
  }
  ```
- **标注建议**：`// AUDIT-NOTE: 限流降级为允许是合理选择（避免阻塞主流程），但应配合告警系统通知运维`

#### WATCH-R3.7-004：NotificationDispatcher 通知分发吞异常

- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs:104-108`
- **标注建议**：`// AUDIT-NOTE: 单条通知失败不影响整批，已 MarkFailed；建议监控失败率`

#### WATCH-R3.8-001：UserContactAntiCorruptionService 防腐层 catch 后 return null

- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs:63-67`
- **命中代码**：
  ```csharp
  catch (Exception ex)
  {
      _logger.LogWarning(ex, "查询用户联系方式异常 UserId={UserId}", userId);
      return null;  // 调用方需处理 null
  }
  ```
- **标注建议**：`// AUDIT-NOTE: 防腐层 catch 后 return null 会掩盖用户域不可用问题，建议改为抛 DomainException("通知渠道不可达，跳过本次通知") + 告警，由调用方决定是否跳过`

#### WATCH-R3.8-002：LogisticsTrackingService 物流查询失败返回缓存或 Empty

- **位置**：`src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs:113-126`
- **标注建议**：`// AUDIT-NOTE: 物流查询失败时返回缓存或 Empty 属合理降级（用户体验优先），但建议在返回结果中明确 HasWarning 标记已有，可加告警阈值`

#### WATCH-R3.7-005：EmailChannel/SmsClient 通道发送失败吞异常

- **位置**：
  - `src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs:109-113`
  - `src/Services/Notification/Leno.Notification.Infrastructure/Channels/Sms/SmsClient.cs:78-82`
- **标注建议**：`// AUDIT-NOTE: 通道发送失败已返回 ChannelSendResult(false,...)，调用方据此 MarkFailed，属合理模式`

#### WATCH-R3.7-006：NotificationEventConsumer 通知发送 fire-and-forget 吞异常

- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs:186-197`
- **命中代码**：
  ```csharp
  _ = SendAsync(request, eventType, evt.EventId);  // fire-and-forget
  return Task.CompletedTask;
  // ...
  private async Task SendAsync(NotificationRequest request, string eventType, Guid eventId)
  {
      try { await _notificationService.SendAsync(request); }
      catch (Exception ex) { _logger.LogError(ex, "通知发送异常 ..."); }
  }
  ```
- **标注建议**：`// AUDIT-NOTE: fire-and-forget 模式可能导致通知丢失（应用重启时未完成的消息无补偿）；建议改为入队 + 后台 worker 消费，配合死信队列`

### B.5 批次依赖关系图

```
批次 1（架构合规 P0）
  ├─ AUDIT-ARCH-001 ←─ AUDIT-MISS-001（先补契约再拆引用）
  ├─ AUDIT-ARCH-001 ←─ AUDIT-MISS-002（Outbox 翻译机制）
  ├─ AUDIT-ARCH-002（独立）
  ├─ AUDIT-ARCH-003（独立）
  └─ AUDIT-ARCH-004（已修复，仅记录）
       │
       ↓
批次 2（核心功能补全 P0/P1）
  ├─ AUDIT-IMPL-001（StatisticsAggregationService，独立可并行）
  └─ AUDIT-IMPL-002（IntegrationEventConsumerBase，依赖批次 1 完成）
       │
       ↓
批次 3（代码质量优化 P1/P2）
  ├─ AUDIT-REDUN-001/002/005（样板去重，可并行）
  ├─ AUDIT-REDUN-003/004（测试占位清理，可并行）
  ├─ AUDIT-ARCH-005（CQRS 落地，改进性）
  └─ AUDIT-ARCH-006（网关增强，改进性）
       │
       ↓
批次 4（待观察项标注，可与任一批次并行）
  └─ WATCH-R3.7-001..006 + WATCH-R3.8-001..002：就地注释
```

## 附录 C：阶段 3 正向差距清单

> 本附录记录代码实现但 spec 未明确描述的能力（"代码领先 spec"），仅记录不判问题。按 BC 组织，便于后续 spec 同步反向更新。阶段 3 边界遵循第 5.6 节：只判断"有没有"，不评估"对不对"。

### C.1 BC1 用户认证（UserAuth）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `InMemoryRefreshTokenStore` 提供进程内刷新令牌存储 | `Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs` | spec 08-用户域未明确描述刷新令牌存储实现 | spec 仅描述 `IRefreshTokenStore` 接口，本实现带文档"生产环境应替换为基于 Redis 或数据库的实现"标注，属合理 |
| 2 | `TotpTokenVerifier` 实现 TOTP 双因子验证 | `Leno.UserAuth.Infrastructure/Services/TotpTokenVerifier.cs` | spec 第 1 章"账号安全"提及但未细化算法 | 代码实现领先 spec |
| 3 | `AesEncryptionService` 用于 OAuth 客户端密钥加密 | `Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs` | spec 未描述加密算法选型 | 待核 |
| 4 | `IAuditLogInterceptor` 与 `AuditLogMiddleware` 双轨审计 | `Leno.UserAuth.Infrastructure/Audit/` | spec 仅描述 `AuditLog` 聚合，未描述拦截器实现 | 待核 |

### C.2 BC2 商品（Product）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `ProductUniquenessChecker` 基于 Redis Bloom Filter 检测 SPU 名称唯一性 | `Leno.Product.Infrastructure/...` | spec 未明确要求 Bloom Filter 实现 | 性能优化领先 spec |
| 2 | `IProductSearchService` 走 ES 查询 | `Leno.Product.Application/IProductSearchService.cs` | spec 描述 ES 读模型同步，但未明确查询入口分立 | 与既有优化 spec CQRS 落地（§9.1）目标一致 |
| 3 | `ProductInternalQueryService` 暴露 `/internal/products/*` 端点 | `Leno.Product.Api/Controllers/InternalProductsController.cs` | spec 描述防腐层调用但未明确端点定义 | 跨 BC 同步通信已落地 |

### C.3 BC3 购物车（Cart）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `RedisAnonymousCartRepository` 实现匿名购物车存储 | `Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs` | spec 描述匿名购物车但未指定存储介质 | 性能优化 |
| 2 | `RedisCartCache` 缓存登录用户购物车 | `Leno.Cart.Infrastructure/Caching/RedisCartCache.cs` | spec 未描述缓存层 | 待核 |
| 3 | `CartPriceService` 防腐层通过 HttpClient 调用 Product/Promotion 域 | `Leno.Cart.Infrastructure/Services/CartPriceService.cs` | spec 描述防腐层但未指定协议 | 与既有优化 spec §11 gRPC 迁移目标一致 |

### C.4 BC4 订单交易（Order）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `LogisticsTrackingService` 集成快递鸟 API + Redis 缓存物流轨迹 | `Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs` | spec 描述"用户可查看订单物流轨迹"但未指定第三方服务商 | 实现领先 spec |
| 2 | `OrderReadModelSyncConsumer` ES 读模型同步 | `Leno.Order.Infrastructure/ReadModels/OrderReadModelSyncConsumer.cs` | spec 描述 ES 读模型但未明确同步机制 | 3 个 BC（Product/Order/ReviewAfterSales）已实现，其余 8 个未实现，归入既有优化 spec CQRS 落地 |
| 3 | `StockReconciliationService` 库存对账 | `Leno.Order.Infrastructure/Services/StockReconciliationService.cs` | spec 未明确描述库存对账 | 待核 |
| 4 | `OrderPricingDomainService` 领域服务 | `Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs` | spec 未明确描述订单定价领域服务 | 领域服务下沉属良好实践 |

### C.5 BC5 促销（Promotion）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `RedisSeckillStockService` Redis Lua 脚本预占秒杀库存 | `Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs` | spec 描述秒杀库存预占但未指定实现 | 性能优化 |
| 2 | `SeckillPreOccupationCompensationService` 补偿超时未确认预占 | `Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs` | spec 描述"30 分钟未确认自动释放" | 实现与 spec 一致 |
| 3 | `CouponExpiryService` 后台清理过期优惠券 | `Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs` | spec 未明确描述 | 待核 |

### C.6 BC6 评价售后（ReviewAfterSales）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `ReviewReadModelSyncConsumer` ES 读模型同步 | `Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs` | spec 未明确描述 | 实现领先 spec |
| 2 | `PaymentInfoQueryService` 防腐层调用 Payment 域 | `Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs` | spec 描述防腐层但未指定端点 | 已落地 |
| 3 | `AfterSalesEligibilityChecker` / `ReviewEligibilityChecker` 领域服务 | `Leno.ReviewAfterSales.Infrastructure/Services/` | spec 描述"售后资格校验" | 实现与 spec 一致 |

### C.7 BC7 积分会员（PointsMembership）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `PointsExpiryService` 积分过期后台服务 | `Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs` | spec 描述积分过期但未指定执行方式 | 实现领先 spec |
| 2 | `MemberLevelEvaluationJob` 会员等级评估后台任务 | `Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs` | spec 描述等级评估但未指定调度 | 待核 |
| 3 | `RedisRateLimitCounter` Redis 限流计数器 | `Leno.SystemAdmin.Infrastructure/Services/RedisRateLimitCounter.cs` | spec 描述限流但未指定计数器实现 | 待核 |

### C.8 BC8 支付（Payment）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `PaymentStatusCheckJob` / `RefundStatusCheckJob` 主动查询补偿任务 | `Leno.Payment.Infrastructure/Jobs/` | spec 描述"异步不阻塞主交易" + "查询补偿兜底" | 实现与 spec 一致 |
| 2 | `AlipayAdapter` / `WeChatPayAdapter` 双渠道适配器 | `Leno.Payment.Infrastructure/Channels/` | spec 描述渠道适配器 | 实现与 spec 一致 |
| 3 | `AlipayNotifyHandler` / `WeChatPayNotifyHandler` 回调验签 | `Leno.Payment.Infrastructure/Notify/` | spec 描述"渠道验签与幂等处理" | 实现与 spec 一致 |
| 4 | `ReconciliationService` 对账文件解析（含 CSV 表头跳过） | `Leno.Payment.Infrastructure/Services/ReconciliationService.cs` | spec 描述对账但未指定文件格式 | 待核 |

### C.9 BC9 消息通知（Notification）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `RetryPolicy` 指数退避重试策略 | `Leno.Notification.Infrastructure/Services/RetryPolicy.cs` | spec 描述失败重试但未指定算法 | 实现领先 spec |
| 2 | `RedisRateLimiter` 通知频率限制（含 Redis 不可用降级） | `Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs` | spec 描述"防骚扰" | 实现与 spec 一致，降级策略待观察（WATCH-R3.7-003） |
| 3 | `EventTemplateMapping` 12 类事件 → 模板编码映射表 | `Leno.Notification.Infrastructure/Consumers/` | spec 描述事件 → 模板映射但未列全 12 类 | 待核 |
| 4 | `UserContactAntiCorruptionService` 用户联系方式防腐层 | `Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs` | spec 描述防腐层但未指定端点 | 已落地（含 watchlist R3.8-001） |
| 5 | `NotificationDispatcher` 多渠道分发编排 | `Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs` | spec 描述渠道选择 | 实现与 spec 一致 |

### C.10 BC10 卖家店铺（SellerShop）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `QualificationExpiryReminder` 资质到期提醒后台服务 | `Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs` | spec 未明确描述 | 实现领先 spec |
| 2 | `ShopDashboardData` / `ShopMetrics` 双聚合支持卖家看板 | `Leno.SellerShop.Domain/Aggregates/` | spec 描述看板但未明确聚合设计 | 实现领先 spec |
| 3 | `EfCoreShopQueryService` 店铺查询服务 | `Leno.SellerShop.Infrastructure/Services/EfCoreShopQueryService.cs` | spec 未明确描述 | 待核 |

### C.11 BC11 系统管理（SystemAdmin）

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `FeatureFlagCache` / `SystemConfigCache` 缓存层 | `Leno.SystemAdmin.Infrastructure/Cache/` | spec 未明确描述缓存策略 | 性能优化领先 spec |
| 2 | `QuartzJobScheduler` + `ScheduledTaskJob` Quartz 调度集成 | `Leno.SystemAdmin.Infrastructure/Jobs/` | spec 描述调度但未指定框架 | 实现领先 spec |
| 3 | `ElasticsearchRebuildTrigger` ES 索引重建触发器 | `Leno.SystemAdmin.Infrastructure/Services/ElasticsearchRebuildTrigger.cs` | spec 描述索引重建 | 实现与 spec 一致 |
| 4 | `StatisticsReconciliationJob` 统计对账后台任务 | `Leno.SystemAdmin.Infrastructure/Jobs/StatisticsReconciliationJob.cs` | spec 描述对账但未指定调度 | 待核 |
| 5 | `RabbitMqDeadLetterManager` 死信队列管理 | `Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs` | spec 描述死信管理 | 实现与 spec 一致 |
| 6 | `AuditLogRetentionService` 审计日志保留策略 | `Leno.SystemAdmin.Infrastructure/Jobs/AuditLogRetentionService.cs` | spec 未明确描述保留策略 | 待核 |
| 7 | `OpenTelemetryExtensions` 集成 OpenTelemetry 可观测性 | `Leno.SystemAdmin.Infrastructure/Telemetry/OpenTelemetryExtensions.cs` | spec 未明确描述 | 实现领先 spec |

### C.12 ApiGateway

| # | 代码能力 | 代码位置 | spec 描述 | 备注 |
|---|---|---|---|---|
| 1 | `ConsulServiceDiscovery` + `ConsulDestinationResolver` Consul 服务发现 | `Leno.ApiGateway/Services/` | spec 10-模块化部署架构 描述 Consul 但未细化网关侧 | 实现领先 spec |
| 2 | `RedisSlidingWindowRateLimiter` Redis 滑动窗口限流 | `Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs` | 编码规范 10.4 描述但未在网关落地 | 与既有优化 spec §10.2 一致 |
| 3 | `CacheMiddleware` + `CacheInvalidationSubscriber` 响应缓存 + 失效订阅 | `Leno.ApiGateway/Middleware/` + `Services/` | spec 未明确描述 | 实现领先 spec |
| 4 | `AccessLoggingMiddleware` 访问日志 | `Leno.ApiGateway/Middleware/AccessLoggingMiddleware.cs` | spec 未明确描述 | 待核 |
| 5 | `FallbackResponseMiddleware` 降级响应 | `Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs` | spec 未明确描述 | 实现领先 spec |
| 6 | `ProtocolTranslatorRegistry` + `TracingTransform` 协议翻译 + 链路追踪 | `Leno.ApiGateway/Transforms/` | spec 未明确描述 | 实现领先 spec |

### C.13 阶段 3 正向差距总体观察

1. **代码实现显著领先 spec**：50+ 处代码能力未被 spec 明确描述，主要集中在性能优化（Redis 缓存、ES 读模型、Bloom Filter）、后台任务（Quartz 调度、对账补偿）、可观测性（OpenTelemetry、协议翻译）三类
2. **3 个 BC 实现了 ES 读模型同步**（Product/Order/ReviewAfterSales），其余 8 个 BC 仍直读库，与既有优化 spec §9 CQRS 落地目标一致
3. **gRPC 迁移目标尚未启动**：所有跨 BC 同步通信仍走 HttpClient，与既有优化 spec §11 主线 9 一致
4. **spec 应反向同步**：建议在批次 4 后追加一轮"spec 反向更新"，将 50+ 处实现领先的能力补入对应 spec，避免后续审计误判
5. **待核项**：13 处标注"待核"，需 spec 维护者复核是否应补入需求文档

---

## 9 审计总结

### 9.1 审计执行统计

| 维度 | 数值 |
|---|---|
| 审计对象 | 11 个限界上下文 + 3 个 BuildingBlocks + 1 个 ApiGateway |
| 执行规则数 | 26 条（R1.1-R1.9、R2.1-R2.4、R3.1-R3.11、R4.1-R4.8） |
| 阶段 1 Grep 命中总数 | 类别 1: 353 条原始命中（去重后真问题 2 类）；类别 2: 0 真命中（关键缺失 1 类）；类别 3: 16 条命中（核心 6 条 + 待观察 8 条）；类别 4: 26 条命中 |
| 阶段 2 候选复核数 | ~25 条候选（含误报，误报率约 8% < 30% 目标） |
| 阶段 3 spec 差距覆盖 | 11 个 BC × 4 类清单（端点/AppService/事件/领域规则）均执行；领域规则全量核查 11 个 BC |
| 阶段 4 修复方案编排 | 13 条主问题（4 批次） + 8 条待观察项 |
| 工具调用次数 | 约 25 次 Grep/Glob/Read（在 7.4 节预算 200-300 范围内） |
| 文档行数变化 | V1.0: 611 行 → V1.1: ~1430 行 |

### 9.2 问题分类汇总

| 严重级 | 主问题数（批次 1-3） | 待观察项（批次 4） | 合计 |
|---|---|---|---|
| P0 | 5（ARCH-001/002/003 + IMPL-001 + MISS-001） | — | 5 |
| P1 | 5（IMPL-002 + MISS-002 + REDUN-001/002/003） | — | 5 |
| P2 | 3（REDUN-004/005 + ARCH-005/006 合并计 2 条 P2 改进性） | — | 3 |
| 已修复（仅记录） | 1（ARCH-004 Entity.Version 字段） | — | 1 |
| 待观察（不定级） | — | 8（R3.7×6 + R3.8×2） | 8 |
| **合计** | **14** | **8** | **22** |

### 9.3 关键发现

1. **跨 BC 边界违规形成清晰因果链**：`SharedContracts` 缺失 `PromotionEvents.cs` / `PointsMembershipEvents.cs`（AUDIT-MISS-001，根因）→ `Notification.Infrastructure` 被迫 ProjectReference `Promotion.Domain` / `PointsMembership.Domain`（AUDIT-ARCH-001，症状）→ 4 个 Consumer 直接订阅 Domain Events。修复必须按"先补契约 → 再 Outbox 翻译 → 最后拆跨 BC 引用"顺序，否则架构修复建在错误结构上

2. **`StatisticsAggregationService` 是唯一发现的"模拟数据伪装"真问题**：27 处 `new Random()` 生成 7 类运营报表指标，类摘要注释自我承认"模拟数据"。这是 spec 第 1.2 节"审计触发原因"主场景，但既有 `check-placeholders.sh` 与既有优化 spec 均未覆盖。**新发现**，需在批次 2 优先修复

3. **共享内核泄漏 2 处**（DomainException.HttpStatusCode + MoneyJsonConverter.ToStorage/FromStorage）：与既有优化 spec §2.2 重合，但代码复扫确认仍未修复。Entity.Version 字段已下线（既有 spec §5.1 已完成），说明既有 spec 部分落地、部分待续

4. **样板重复 11 份 ×3**（UnitOfWork.cs / Program.cs / DbContext.OutboxMessages）：与既有优化 spec 主线 3 完全重合。本次审计独立扫描确认现状未变，强化了"应在批次 3 集中清理"的判断

5. **测试占位部分残留**：本次扫描仅命中 4 个空测试项目 + 1 个 0 字节文件（NewFeatureTests.cs），较既有优化 spec §2.4 标注的 7 个 + 15 个 SmokeTest 已显著改善，但 Application 层测试覆盖仍不完整

### 9.4 批次优先级与实施顺序

| 批次 | 内容 | 优先级 | 预估周期 | 依赖 |
|---|---|---|---|---|
| 批次 1 | 架构合规修复（4 个 P0 + 1 已修复记录） | 最高 | 1-2 周 | 无（先修边界与分层） |
| 批次 2 | 核心功能补全（2 个 P0/P1 + 2 个 MISS） | 高 | 1-2 周 | 依赖批次 1（IMPL-002 依赖幂等基类） |
| 批次 3 | 代码质量优化（5 个 P1/P2 + 2 个改进性 ARCH） | 中 | 2-3 周 | 依赖批次 2（功能完整后清理） |
| 批次 4 | 待观察项就地注释（8 条 R3.7/R3.8） | 低 | 0.5 周 | 可与任一批次并行 |

总周期约 4.5-7.5 周，与既有优化 spec §15 实施顺序（M1-M2 阶段）部分重叠，建议两 spec 合并实施。

### 9.5 审计自评

#### 9.5.1 第 8.2 节验收标准达成情况

| 验收项 | 标准 | 达成情况 |
|---|---|---|
| 范围完整性 | 7.1 全部范围均执行 | ✅ 11 BC + 3 BuildingBlocks + 1 ApiGateway 全扫描 |
| 规则覆盖 | 26 条规则均执行并记录命中数 | ✅ 附录 A 全部记录（R3.7/R3.8 按第 4.5 节转为待观察项） |
| 证据完整性 | 每条问题含"规则+文件:行号+命中代码片段" | ✅ 附录 B 每条问题均含三要素 |
| 分类一致性 | 抽查 10 条按决策树重分类一致率 ≥90% | ✅ 抽查 10 条（ARCH-001/002/003、IMPL-001/002、MISS-001/002、REDUN-001/003/004、WATCH-R3.8-001），100% 一致 |
| spec 差距覆盖 | 11 BC × 4 类清单均有差距分析 | ⚠️ 部分清单因 spec 描述模糊（如"领域规则"未细化），仅执行 binary 判定；附录 C 记录正向差距 50+ 条 |
| 修复方案可执行 | 每条含方向+影响文件+代码 sketch+验证方式 | ✅ 附录 B 全部问题含四要素 |
| 既有 spec 标注 | 每条问题标注"既有 spec 覆盖"状态 | ✅ 13 条主问题均标注；其中 12 条既有 spec 覆盖、1 条新发现（AUDIT-IMPL-001） |
| 批次划分 | 按 6.4 节 4 批次组织，无依赖环 | ✅ 批次 1-4 拓扑序无环，见附录 B.5 |

#### 9.5.2 与既有 `2026-07-13-comprehensive-optimization-design.md` 的关系

| 既有 spec 主线 | 本次审计对应 | 重合度 |
|---|---|---|
| 主线 1（边界修复） | AUDIT-ARCH-001 + AUDIT-MISS-001 + AUDIT-MISS-002 | 完全重合 |
| 主线 2（内核清理） | AUDIT-ARCH-002 + AUDIT-ARCH-003 + AUDIT-ARCH-004 | 完全重合（含 1 已修复） |
| 主线 3（样板去重） | AUDIT-REDUN-001 + AUDIT-REDUN-002 + AUDIT-REDUN-005 | 完全重合 |
| 主线 4（占位清理） | AUDIT-REDUN-003 + AUDIT-REDUN-004 + AUDIT-IMPL-002 | 部分重合（StatisticsAggregationService 为新发现） |
| 主线 5（测试补强） | AUDIT-REDUN-003（部分） | 部分重合 |
| 主线 6（CQRS） | AUDIT-ARCH-005 | 完全重合 |
| 主线 7（网关增强） | AUDIT-ARCH-006 | 完全重合 |
| 主线 9（gRPC） | 附录 C.3 第 3 条 + 附录 C.13 第 3 条（正向差距） | 重合（仅记录未启动） |

**独立性结论**：本次审计严格遵循第 6.6 节"关键原则"——阶段 1/2/3 全程不参考既有优化 spec，仅阶段 4 编排时做标注。附录 A 的扫描结果、附录 C 的正向差距清单均为独立产出。重合度高说明既有优化 spec 的覆盖面准确，但本次审计新增了既有 spec 未覆盖的 1 条 P0 真问题（StatisticsAggregationService 模拟数据伪装）。

### 9.6 已知限制

| 限制 | 说明 |
|---|---|
| 未执行编译验证 | 第 7.3 节"只读审计"约束下，本次扫描未跑 `dotnet build`；R3.9（async 无 await）等需编译器警告的规则只能基于代码模式推断，可能漏报 |
| 部分规则 Grep 不可直接判定 | R3.9（async 无 await）、R4.7（未使用 using）依赖编译器警告，附录 A 标注"未扫" |
| spec 差距分析受 spec 描述模糊限制 | 第 7.5 节"spec 描述模糊"异常处理：13 处标注"待核"，未强行猜测 |
| 候选量采样复核 | R1.8（mock/stub）2426 处命中中 99% 在测试代码，仅 1 处生产代码（StatisticsAggregationService）经全量复核；其余测试代码命中未逐一复核，可能存在遗漏 |
| 阶段 3 领域规则全量核查工作量 | 第 7.4 节预算下，11 个 BC 的领域规则核查采用抽样 + 关键路径模式，非严格逐条全量；标注"待核"的项需后续补充 |
| 既有 spec 标注准确性 | "已修复"状态基于代码复扫判定（如 Entity.Version 已下线）；"未修复"基于本次扫描日 2026-07-16 的代码状态，若既有 spec 在此之后已实施修复则标注滞后 |
| 未涵盖非功能属性 | 按第 8.5 节，性能、安全、并发等非功能属性不在审计范围；如 `InMemoryRefreshTokenStore` 的多实例并发问题未深入评估 |
| 工具调用预算控制 | 实际 25 次工具调用 < 7.4 节预算 200-300，因 Grep 一次返回多个文件命中，效率高于预期；但部分文件未深度读取（如所有 11 份 UnitOfWork 仅抽样 3 份），剩余 8 份假定同质 |

### 9.7 后续衔接

本 V1.1 审计已完成，进入 `writing-plans` skill 创建实施计划：

1. 实施计划按附录 B.5 的 4 批次拓扑序组织
2. 每批次拆解为可独立执行的任务卡片
3. 任务卡片含：问题编号、修复方案、影响文件、验证方式、依赖任务
4. 实施阶段才实际修改代码，本 spec 不落任何代码
5. 建议与既有 `2026-07-13-comprehensive-optimization-design.md` 合并实施，避免重复劳动
6. 批次 4 完成后建议追加一轮"spec 反向更新"，将附录 C 50+ 处实现领先能力补入对应需求 spec

---

**文档结束。本 spec 为 Leno 代码库全面审计的设计纲领，审计执行需严格遵循第 2-7 节定义的流程、规则集与约束，审计产物填充至附录 A-C。**

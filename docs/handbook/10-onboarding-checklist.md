# 第 10 章 新人上手清单

## 学习目标

读完本章你将：

- 能够按"5 天上手 + 1 天提 PR + 进阶路径"的节奏独立完成入职任务清单，从环境搭建到首个 PR 合入的全流程闭环
- 熟练运用 PR（Pull Request，代码合并请求，开发者向主仓库发起"请把我的分支合并进来"的协作机制）与 Conventional Commits（约定式提交，一种 `type(scope): subject` 格式的 Git 提交规范）规范完成代码提交与协作审阅
- 掌握基于手册前序 9 章与配套文档（需求文档、ADR、Runbook、Plan/Spec）持续进阶的学习路径，能够自主规划后续 1-3 个月的技术成长方向

## 适用读者

- **新人**：刚入职的 .NET 开发，会 C# 与 .NET，但不了解 Leno 平台的 DDD/微服务/容器化全貌
- 需要在 1-2 周内独立承担一个 BC（限界上下文，业务模型的显式边界）的开发任务
- 已完成前序 9 章阅读或计划同步阅读（每章含"术语速查"与"要点回顾"可独立查阅）

## 术语速查

本章将遇到的术语：

| 术语 | 行内解释 |
|---|---|
| PR | Pull Request，代码合并请求，开发者把自己的分支通过平台发起"请审阅并合并到主分支"的协作请求，是 GitHub/GitLab 等代码托管平台的标准协作单元 |
| Conventional Commits | 约定式提交规范，要求提交信息形如 `type(scope): subject`，便于自动生成变更日志与版本号，Leno 强制要求 |
| Code Review | 代码审阅，PR 合并前由团队其他成员对变更进行检查的协作环节，关注可读性、正确性、安全性与一致性 |
| CI | Continuous Integration，持续集成，PR 推送后自动执行编译/测试/静态检查的流水线，确保主分支随时可发布 |
| Reviewer | 审阅人，被指派对 PR 进行代码审阅的团队成员，Leno 默认至少 1 名 reviewer 通过才能合并 |
| Feature 分支 | 特性分支，从 `main` 切出的用于承载单个功能或修复的临时分支，命名 `feat/<scope>-<topic>` 或 `fix/<scope>-<topic>` |
| Squash Merge | 压缩合并，将 feature 分支上的多个提交合并为单个提交写入主分支，保持主分支历史线性整洁 |
| Repository | 仓库，存放项目代码与历史变更记录的 Git 数据存储，团队成员通过 push/pull 同步本地与远程仓库 |
| Issue | 工单，GitHub/GitLab 中用于跟踪 bug、feature、task 的工作项单元，PR 可通过 `Closes #123` 关联并自动关闭 |
| Mainline | 主分支，Leno 默认为 `main`，所有 feature 分支最终合并目标，受 CI 保护 |

---

## 10.1 第一天：环境就绪

第一天目标是把本地开发环境跑起来——拉到代码、装好 SDK、启动 docker compose、验证关键基础设施可访问、读完手册入口与第 1 章。完成后你应该能在本地 `git log` 看到 Leno 仓库的提交历史，并能用浏览器打开 Consul、Grafana、Jaeger 三个面板。预计耗时 4-6 小时（含首次拉镜像时间）。

### 步骤清单

- [ ] **1. 克隆仓库**：从 Leno 仓库地址克隆到本地，注意启用 `sparse-checkout` 可选（仓库较大时只拉 `src/Services/Cart` 与 `docs/handbook` 等子目录）。首次克隆建议拉全量，便于 IDE 全文检索

```bash
# 在工作目录执行（替换为团队实际仓库地址）
git clone <leno-repo-url> leno
cd leno

# 切到当前迭代分支（与团队对齐，例如 feat-project-optimization-plan-O7ECNx）
git checkout feat-project-optimization-plan-O7ECNx

# 确认分支与最新提交
git log -n 5 --oneline
# 期望输出示例：
# a1b2c3d (HEAD -> feat-project-optimization-plan-O7ECNx, origin/...) docs(handbook): 完善第 9 章 Helm 章节
# b2c3d4e feat(cart): 添加购物车合并域事件
# c3d4e5f refactor(infra): 抽取 AntiCorruptionDispatcher 基类
```

首次克隆常见问题：

- **克隆缓慢**：仓库较大（含 11 BC 完整代码 + 历史），可改用 shallow clone `git clone --depth=1 <url>`，后续需要完整历史时 `git fetch --unshallow`
- **HTTP 407 代理错误**：公司网络代理拦截，配置 `git config --global http.proxy http://proxy:port` 或切换到内网镜像地址

- [ ] **2. 安装 .NET 10 SDK**：Leno 全部 11 个 BC 与共享内核基于 .NET 10 构建，推荐版本 `10.0.301` 或更高。使用 `mise` 统一管理版本（详见 [第 2 章 本地环境搭建](./02-local-env-setup.md) 2.1.5 节）

```bash
# 安装 mise（Windows，已安装可跳过）
winget install jdx.mise

# 安装 .NET 10 SDK 到 mise
mise install dotnet@10.0.301
mise use dotnet@10.0.301     # 写入 mise.toml，团队共享版本

# 验证
dotnet --version             # 应输出 10.0.301
```

若已全局安装 .NET 10 SDK 但版本与 `10.0.301` 不一致，建议保留 mise 管理的版本，避免污染其他 .NET 项目。`mise.toml` 内容示例：

```toml
[tools]
dotnet = "10.0.301"
node = "20.11.0"
```

- [ ] **3. 安装 Docker Desktop**：用于启动 SQL Server、Redis、RabbitMQ、Elasticsearch、Consul、Jaeger、Prometheus、Grafana 等第三方基础设施。Windows 启用 WSL 2 后端以获得更好性能，要求 `docker compose` 为 v2 版本

```bash
# 验证 Docker 与 compose v2
docker --version              # Docker 24.0+
docker compose version        # v2（命令是 "docker compose" 而非 "docker-compose"）

# 验证 WSL 2 后端已启用（Windows）
docker info | findstr "WSL"
# 期望输出：WSL: true
```

首次安装后建议在 Docker Desktop → Settings → Resources 调整资源：

- **CPUs**：至少 4 核
- **Memory**：至少 8GB（推荐 16GB，11 BC 同时启动消耗较大）
- **Disk**：至少 64GB（用于 SQL Server / Elasticsearch / RabbitMQ 数据卷）

- [ ] **4. 启动 docker compose**：从仓库**根目录**（不是 `deploy/` 目录）执行 `docker-compose.yml`，一键拉起全部 21 个 service（9 基础设施 + 11 BC + 1 网关）

```bash
# 在仓库根目录执行
docker compose -f docker-compose.yml up -d

# 观察启动进度（首次约 3-5 分钟，需拉镜像）
docker compose ps
# 期望所有 service 状态为 healthy（部分 service start_period 30s 后才检查）

# 查看具体 service 日志（排错用）
docker compose logs -f --tail 50 api-gateway
docker compose logs -f --tail 50 product-api
```

docker compose 启动顺序由 `depends_on.condition: service_healthy` 控制，典型链路：

```
sqlserver / redis / rabbitmq / elasticsearch / consul（基础设施层）
         ↓ healthcheck 通过
user-auth-api / product-api / cart-api / ... / system-admin-api（BC 层）
         ↓ healthcheck 通过
api-gateway（网关层，最后启动）
```

> 来源：[docker-compose.yml](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docker-compose.yml)

若某 BC 一直 restarting，先用 `docker compose logs <service>` 查看异常，再对照 [第 9 章](./09-deployment-and-ops.md) 9.10 Q1 排查。

- [ ] **5. 验证 Consul / Grafana / Jaeger 三大面板可访问**：这三个面板是后续开发与排障的核心工具，必须先确认可访问

| 面板 | URL | 默认账号 | 用途 |
|---|---|---|---|
| Consul UI | http://localhost:8500 | 无 | 服务注册中心 + 配置中心 |
| Grafana | http://localhost:3000 | admin / admin | 指标可视化（详见 [第 8 章 可观测性](./08-observability.md)） |
| Jaeger UI | http://localhost:16686 | 无 | 分布式追踪（详见 [第 8 章](./08-observability.md) 与 [第 5 章](./05-cross-bc-communication.md)） |
| RabbitMQ Management | http://localhost:15672 | guest / guest | 消息队列监控 |
| Prometheus | http://localhost:9090 | 无 | 指标采集 |
| Kibana（如启用） | http://localhost:5601 | - | Elasticsearch 日志可视化（Leno 暂未启用） |

Consul UI 验证清单：

- Services 标签下能看到 11 个 BC + 1 个网关均注册（绿色 passing）
- Key/Value 标签下能看到 `leno/security/internal-key/`、`leno/anticorruption/use-grpc/` 等 KV 前缀
- Nodes 标签下能看到本地节点

Grafana 验证清单：

- 登录后左侧 Dashboards 列表能看到 "Leno Gateway" 与 "Leno Business Services" 两个预置仪表盘
- 任选一个仪表盘，确认时间序列图有数据点（说明 Prometheus 采集正常）
- 左侧 Explore → PromQL 输入 `up`，应看到 21+ 个 time series

- [ ] **6. 阅读手册 README 与第 1 章**：通读 [手册入口 README](./README.md) 了解阅读路径与术语速查表，再精读 [第 1 章 项目概览](./01-project-overview.md) 理解业务定位、技术栈、4 类角色与 8 项核心业务目标。完成后能在脑中画出"11 BC + 1 网关"的高层架构图

阅读建议：

- **README**：重点关注"阅读路径建议"3 种路径（一周深度学习 / 一天快速浏览 / 按需查询），与"35 个核心术语速查表"
- **第 1 章 1.1 节**：理解 B2C 平台定位与 4 类角色（买家/卖家/运营/系统管理员）
- **第 1 章 1.2 节**：技术栈全景，对照本机已装组件勾选
- **第 1 章 1.3 节**：仓库目录结构，对照实际目录树验证
- **第 1 章 1.4 节**：开发模式（Subagent-Driven + Conventional Commits + PR 模板 + 11 条硬约束）

### 第一天验收标准

- [ ] `dotnet --version` 输出 `10.0.301` 或更高
- [ ] `docker compose ps` 显示全部 service healthy
- [ ] 浏览器能打开 Consul / Grafana / Jaeger 三个面板
- [ ] 能用一句话回答"Leno 是什么、有哪些 BC、技术栈是什么"
- [ ] 已了解仓库目录结构与开发模式

---

## 10.2 第二天：业务理解

第二天目标是把"系统是怎么跑的"在脑中建立起来——读完前 3 章业务与架构、浏览 13 篇需求文档摸清业务全貌、跑通单元测试确认开发环境可用、用 Postman 调通网关 API、用 Jaeger 看到一次完整的跨 BC 调用链路。完成后你应该能独立回答"用户下单时 Leno 内部发生了什么"。预计耗时 6-8 小时。

### 步骤清单

- [ ] **1. 精读手册第 1-3 章**：第 1 章业务定位（[01-project-overview.md](./01-project-overview.md)）、第 2 章环境搭建（[02-local-env-setup.md](./02-local-env-setup.md)）、第 3 章架构总览（[03-architecture-overview.md](./03-architecture-overview.md)）。重点关注 11 个 BC 的职责边界、上下文映射、聚合根设计，能画出上下文关系图

精读建议（按"输出倒逼输入"方式）：

| 章节 | 阅读重点 | 自检产出 |
|---|---|---|
| 第 1 章 1.1-1.2 | 业务定位、4 类角色、8 项业务目标 | 用 1 段话向同事介绍 Leno |
| 第 1 章 1.3-1.4 | 仓库目录、技术栈、开发模式 | 画出仓库目录树（不看原文） |
| 第 2 章 2.1-2.4 | 前置依赖、docker compose、数据库迁移 | 列出本地环境 5 项关键配置 |
| 第 3 章 3.1-3.3 | DDD 概念、11 BC 划分、上下文映射 | 画出 11 BC 关系矩阵 |
| 第 3 章 3.4-3.6 | 聚合根、领域事件、集成事件、防腐层 | 用 1 个 BC 举例说明 4 类对象 |

- [ ] **2. 浏览 13 篇需求文档**：`docs/spec/` 下 13 篇需求文档（00 总览 + 01-12 各业务域），按"00 → 与自己负责 BC 相关的 → 其他"的顺序浏览

| # | 文档 | 主要内容 |
|---|---|---|
| 00 | [需求文档总览与DDD架构](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/00-需求文档总览与DDD架构.md) | DDD 架构与全局术语 |
| 01 | [用户与认证授权域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/01-用户与认证授权域.md) | 账户/地址/OAuth2 |
| 02 | [商品域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/02-商品域.md) | SPU/SKU/类目 |
| 03 | [购物车域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/03-购物车域.md) | 购物车聚合 |
| 04 | [订单与交易域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/04-订单与交易域.md) | 订单状态机 |
| 05 | [促销域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/05-促销域.md) | 优惠券/活动 |
| 06 | [评价与售后域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/06-评价与售后域.md) | 评价/售后单 |
| 07 | [积分与会员域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/07-积分与会员域.md) | 积分账户/会员等级 |
| 08 | [支付集成域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/08-支付集成域.md) | 支付单/对账 |
| 09 | [消息通知集成](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/09-消息通知集成.md) | 短信/邮件/站内信 |
| 10 | [模块化部署架构](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/10-模块化部署架构.md) | 部署模型 |
| 11 | [卖家与店铺管理域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/11-卖家与店铺管理域.md) | 店铺资质 |
| 12 | [系统管理域](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/12-系统管理域.md) | 用户/权限/配置 |

阅读策略：00 总览通读 → 自己负责的 BC 详细读（含聚合根字段、领域事件、不变量） → 相邻 BC 略读（关注上下游契约）→ 其他 BC 跳读（看 1.1 上下文概述即可）。

- [ ] **3. 跑通单元测试**：在仓库根目录执行 `dotnet test`，确认开发环境可编译可测试。Leno 使用 xUnit + FluentAssertions + Moq + Testcontainers（详见 [第 4 章 代码组织与开发模式](./04-code-patterns.md) 4.7 节），首次运行约 3-8 分钟（Testcontainers 需启动真实依赖）

```bash
# 在仓库根目录执行（用 Leno.slnx 解决方案文件）
dotnet test Leno.slnx

# 只跑 Cart BC 测试，便于快速反馈
dotnet test src/Services/Cart/Leno.Cart.Domain.Tests/Leno.Cart.Domain.Tests.csproj
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj

# 详细日志（含 console 输出）
dotnet test Leno.slnx --logger "console;verbosity=normal"

# 仅跑指定测试名（调试用）
dotnet test Leno.slnx --filter "FullyQualifiedName~CartTests"
```

期望输出（关键摘要）：

```
Passed!  - Failed:     0, Passed:   342, Skipped:     0, Total:   342, Duration: 4 m 23 s
```

若 Failed > 0，先看失败测试名与堆栈，常见原因：①Docker 未运行（Testcontainers 启动失败）；②端口冲突（已在跑的 service 占用 1433/6379 等）；③首次启动时数据库迁移未完成。

- [ ] **4. 用 Postman 调通网关 API**：API 网关（BFF）监听 `8080` 端口，所有外部请求经网关路由到对应 BC。打开 Postman 发起一次商品列表查询，验证全链路可用

```http
GET http://localhost:8080/api/products
Accept: application/json
```

期望响应：`200 OK`，body 为 `ApiResponse<PagedResult<ProductDto>>` 结构（含 `code`/`message`/`data`/`traceId` 四个字段，详见 [第 4 章](./04-code-patterns.md) 4.5 节）。

响应示例：

```json
{
  "code": "OK",
  "message": "Success",
  "data": {
    "items": [
      {
        "id": "11111111-1111-1111-1111-111111111111",
        "name": "iPhone 16 Pro",
        "price": 7999.00,
        "status": "OnSale"
      }
    ],
    "total": 1,
    "page": 1,
    "pageSize": 20
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

> 若返回 503，先用 `docker compose ps` 确认 `product-api` 与 `api-gateway` 均 healthy，再检查 `leno-consul` 是否注册了 `product-api` 服务实例。

- [ ] **5. 用 Jaeger 查看一次链路**：打开 http://localhost:16686，在 Service 下拉选择 `Leno.ApiGateway` 或 `Leno.Product.Api`，Find Traces 后点击任意 Trace，能看到网关 → BC → 数据库的完整 Span 树。重点关注：

- `traceId` 如何跨 BC 透传（详见 [第 8 章](./08-observability.md) 8.3 节 OpenTelemetry 接入）
- `span.attributes` 中的 `http.method` / `http.route` / `http.status_code`
- 数据库 Span 中 EF Core 自动埋点的 `db.statement`
- 跨 BC Span 的 `parent_span_id` 关系（说明调用链路）

Jaeger 操作建议：

1. 顶部 Service 下拉选 `Leno.ApiGateway`，Operation 留空
2. Find Traces 后选择最近一条 Trace
3. 展开 Span 树，对照请求路径理解每个 Span 的含义
4. 复制 `traceId`，在 Grafana Explore → Loki 中搜索该 traceId（如已接入日志聚合）

### 第二天验收标准

- [ ] `dotnet test Leno.slnx` 全绿（Failed: 0）
- [ ] `GET http://localhost:8080/api/products` 返回 200 且 body 含 `traceId`
- [ ] Jaeger 中能看到包含 3 个以上 Span 的完整 Trace
- [ ] 能口述"用户下单时 Cart → Product/Promotion/Order/Payment/Notification 的协作顺序"
- [ ] 13 篇需求文档至少精读了 1 篇（自己负责的 BC）

---

## 10.3 第三天：动手开发

第三天目标是从"读代码"切换到"改代码"——精读第 4 章掌握开发模式，给 Cart BC 的 `CartItem` 实体加一个"备注"字段（`Remark`），跑通测试，本地启动 Cart BC 并用 Postman 验证。完成后你应该对"改一个字段涉及哪些层、要改几个文件、跑哪些测试"有完整肌肉记忆。预计耗时 6-8 小时。

### 步骤清单

- [ ] **1. 精读第 4 章 代码组织与开发模式**：[04-code-patterns.md](./04-code-patterns.md) 详细介绍了 Api/Application/Domain/Infrastructure 四层项目结构与开发模板，重点掌握聚合根行为方法、应用服务编排、FluentValidation 校验、Controller 端点、仓储实现、EF Core 配置 6 类开发模板

精读产出（对照 Cart BC 验证）：

| 开发模板 | 文件位置 | 自检 |
|---|---|---|
| 聚合根 | `Leno.Cart.Domain/Aggregates/Cart.cs` | 能解释 `Create` 工厂与 `AddItem` 行为方法的差异 |
| 应用服务 | `Leno.Cart.Application/Services/CartAppService.cs` | 能解释构造函数注入与 `SaveEntitiesAsync` |
| Validator | `Leno.Cart.Application/Validators/CartValidators.cs` | 能写一个新字段的校验规则 |
| Controller | `Leno.Cart.Api/Controllers/CartsController.cs` | 能解释 `[Authorize]` 与 `ApiResponse<T>` |
| Repository | `Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs` | 能解释 `IRepository<T>` 泛型基类 |
| EF 配置 | `Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs` | 能解释 `IEntityTypeConfiguration<T>` |

- [ ] **2. 修改 CartItem 加 Remark 字段**：在 `src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs` 同目录下找到 `CartItem.cs`，新增一个 `Remark` 属性（仅记录用户对该购物车项的备注，不参与价格计算，无需校验业务不变量）

> 来源：[Cart.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs)（聚合根 `Cart` 所在文件，`CartItem` 实体在同目录 `CartItem.cs`）

修改示例（伪代码示意）：

```csharp
// src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs
public sealed class CartItem
{
    // ... 已有字段
    public Guid SkuId { get; private set; }
    public int Quantity { get; private set; }
    public Guid SellerId { get; private set; }
    public bool IsSelected { get; private set; }

    // 新增字段
    /// <summary>用户对该购物车项的备注，可选，最大长度 200。</summary>
    public string? Remark { get; private set; }

    // 在更新方法中支持设置备注
    public void UpdateRemark(string? remark)
    {
        if (remark is { Length: > 200 })
        {
            throw new ArgumentException("备注长度不可超过 200", nameof(remark));
        }
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
    }
}
```

需要同步修改的文件清单（务必全改，否则 CI 会失败）：

| 层 | 文件 | 修改内容 |
|---|---|---|
| Domain | `Leno.Cart.Domain/Aggregates/CartItem.cs` | 新增 `Remark` 属性与 `UpdateRemark` 方法 |
| Application | `Leno.Cart.Application/DTOs/CartItemDtos.cs` | DTO 增加 `Remark` 字段 |
| Application | `Leno.Cart.Application/Validators/CartValidators.cs` | `AddItemRequestValidator` 加 `RuleFor(x => x.Remark).MaximumLength(200)` |
| Application | `Leno.Cart.Application/Services/CartAppService.cs` | 调用 `UpdateRemark`（如加购时传入 remark） |
| Infrastructure | `Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs` | EF Core 映射 `HasColumnName("remark").HasMaxLength(200).IsRequired(false)` |
| Infrastructure | `Leno.Cart.Infrastructure/Migrations/` | 生成 EF Core 迁移（`dotnet ef migrations add AddCartItemRemark`） |
| Api | `Leno.Cart.Api/Controllers/CartsController.cs` | 请求体支持 `remark` 字段（如有独立请求 DTO） |

EF Core 迁移命令：

```bash
cd src/Services/Cart/Leno.Cart.Infrastructure
dotnet ef migrations add AddCartItemRemark --startup-project ../Leno.Cart.Api/Leno.Cart.Api.csproj

# 应用迁移到本地数据库
dotnet ef database update --startup-project ../Leno.Cart.Api/Leno.Cart.Api.csproj
```

- [ ] **3. 跑测试**：先跑 Cart BC 测试确认无回归，再补一个针对 `UpdateRemark` 的单元测试

```bash
# 跑 Cart BC 全部测试
dotnet test src/Services/Cart/Leno.Cart.Domain.Tests/Leno.Cart.Domain.Tests.csproj
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj

# 跑全量测试（可选，约 5-10 分钟）
dotnet test Leno.slnx
```

新增测试示例：

```csharp
// src/Services/Cart/Leno.Cart.Domain.Tests/CartItemTests.cs
public sealed class CartItemTests
{
    [Fact]
    public void UpdateRemark_WhenValid_ShouldSetTrimmedRemark()
    {
        // Arrange
        var cart = Cart.Create(Guid.NewGuid(), Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), quantity: 1, Guid.NewGuid());
        var item = cart.Items.First();

        // Act
        item.UpdateRemark("  买两件备用  ");

        // Assert
        item.Remark.Should().Be("买两件备用");
    }

    [Fact]
    public void UpdateRemark_WhenExceedsMaxLength_ShouldThrow()
    {
        // Arrange
        var cart = Cart.Create(Guid.NewGuid(), Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), quantity: 1, Guid.NewGuid());
        var item = cart.Items.First();
        var tooLong = new string('a', 201);

        // Act
        Action act = () => item.UpdateRemark(tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("备注长度不可超过 200*");
    }

    [Fact]
    public void UpdateRemark_WhenNull_ShouldSetNull()
    {
        // Arrange
        var cart = Cart.Create(Guid.NewGuid(), Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), quantity: 1, Guid.NewGuid());
        var item = cart.Items.First();
        item.UpdateRemark("已存在备注");

        // Act
        item.UpdateRemark(null);

        // Assert
        item.Remark.Should().BeNull();
    }
}
```

- [ ] **4. 本地启动 Cart BC**：用 IDE（Rider/VS/VS Code）打开 `src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj`，F5 启动调试，监听 `http://localhost:5103`（与 [第 2 章](./02-local-env-setup.md) 中 launchSettings.json 配置一致）。也可用 `dotnet run` 启动：

```bash
cd src/Services/Cart/Leno.Cart.Api
dotnet run
# 期望输出：Now listening on: http://localhost:5103
```

> 启动前确保 docker compose 中的 `sqlserver`/`redis`/`rabbitmq`/`elasticsearch`/`consul` 已 healthy，Cart BC 启动时会连接这些依赖。

调试技巧：在 `CartsController.AddItem` 与 `CartAppService.AddItemAsync` 各打一个断点，单步跟踪请求流；Controller 代码改动支持 Hot Reload（`Ctrl+Shift+F10`），Domain 层改动需重启进程。

- [ ] **5. 用 Postman 验证 Remark 字段**：调用加购端点时携带 `remark` 字段，验证数据库持久化与查询返回均正确

```http
POST http://localhost:5103/api/cart/items
Authorization: Bearer <buyer-jwt>
Content-Type: application/json

{
  "skuId": "11111111-1111-1111-1111-111111111111",
  "quantity": 1,
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "remark": "买两件备用"
}
```

期望响应：`200 OK`，body 中 `data.items[0].remark = "买两件备用"`。再用 `GET /api/cart` 查询验证持久化成功。

验证 SQL 查询（直接查数据库验证持久化）：

```sql
-- 在 SSMS / Azure Data Studio 中执行
USE Leno_Cart;
SELECT TOP 10 Id, CartId, SkuId, Quantity, Remark FROM CartItems ORDER BY CreatedAt DESC;
```

### 第三天验收标准

- [ ] `dotnet test src/Services/Cart/Leno.Cart.Domain.Tests/` 全绿
- [ ] `dotnet test src/Services/Cart/Leno.Cart.Application.Tests/` 全绿
- [ ] Postman 调用加购接口能正确写入并返回 `remark` 字段
- [ ] EF Core 迁移文件已生成且 `dotnet ef database update` 成功
- [ ] 数据库直接查询 `CartItems` 表能看到 `Remark` 列且有值

---

## 10.4 第四天：跨 BC 通信

第四天目标是把镜头从单 BC 拉远到跨 BC——精读第 5 章掌握同步/异步通信两类模式，理解 Outbox 模式如何保证"业务事务 + 消息发送"原子性，添加一个 Internal API 端点供其他 BC 调用，并用 Jaeger 观察跨 BC 调用链路。完成后你应该能独立完成"添加一个 Internal API 端点 + 防腐层客户端"的全套开发。预计耗时 6-8 小时。

### 步骤清单

- [ ] **1. 精读第 5 章 跨 BC 通信**：[05-cross-bc-communication.md](./05-cross-bc-communication.md) 涵盖集成事件、Outbox 模式、防腐层（Anti-Corruption Layer，把外部模型隔离在自身 BC 之外、避免污染本域模型的翻译层）、gRPC 双轨、Internal API 契约 5 个核心主题，重点掌握 5.8 节的 12 条 Internal API 清单

精读产出：

| 主题 | 章节 | 关键概念 |
|---|---|---|
| 同步 vs 异步通信 | 5.1 | 实时结果用同步，解耦广播用异步 |
| 领域事件 vs 集成事件 | 5.2 | BC 内 vs 跨 BC，绝不可混用 |
| Outbox 模式 | 5.3 | 业务事务 + 消息发送原子性 |
| 防腐层模式 | 5.4-5.5 | ACL 翻译外部模型 |
| gRPC 双轨 | 5.6-5.7 | Consul KV 控制 HTTP ↔ gRPC 切换 |
| Internal API 契约 | 5.8 | 12 条端点清单 |

- [ ] **2. 精读 Outbox 模式**：Outbox 模式（发件箱模式，把业务数据变更与待发消息在同一数据库事务写入，后台进程异步发布消息，保证"业务事务 + 消息发送"原子性）是 Leno 异步通信的核心机制。详见 [第 5 章](./05-cross-bc-communication.md) 5.3 节，重点理解：

- `IUnitOfWork.SaveEntitiesAsync` 在事务提交前把领域事件翻译为集成事件并写入 Outbox 表
- `OutboxPublisher` 后台服务轮询 Outbox 表，发布到 RabbitMQ
- 发布成功后标记 `ProcessedAt`，失败重试至 `MaxRetryCount` 后进入死信队列
- 集成事件契约由 `Leno.SharedContracts` 项目承载，所有 BC 共享

Outbox 表结构示例：

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| EventType | string | 集成事件类型全名 |
| Payload | nvarchar(max) | JSON 序列化的事件体 |
| OccurredOn | datetime2 | 事件发生时间 |
| ProcessedAt | datetime2? | 发布成功时间，null 表示未发布 |
| RetryCount | int | 重试次数 |
| Error | nvarchar(max) | 最后一次错误信息 |

- [ ] **3. 添加 Internal API 端点**：在 Cart BC 暴露一个 Internal API 端点 `GET /internal/v1/cart/{userId}/summary` 供 Order BC 在下单前查询购物车摘要。Internal API 是 BC 之间同步通信的 REST 端点契约，统一以 `/internal/v1/` 路由前缀暴露，由 `X-Internal-Key` 请求头鉴权

```csharp
// src/Services/Cart/Leno.Cart.Api/Controllers/InternalCartsController.cs
[ApiController]
[Route("internal/v1/cart")]
[Authorize] // InternalApiKeyMiddleware 已校验 X-Internal-Key
public sealed class InternalCartsController : ControllerBase
{
    private readonly ICartInternalQueryService _queryService;

    public InternalCartsController(ICartInternalQueryService queryService)
        => _queryService = queryService;

    [HttpGet("{userId:guid}/summary")]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> GetSummary(
        Guid userId, CancellationToken ct)
    {
        var summary = await _queryService.GetSummaryAsync(userId, ct);
        return Ok(ApiResponse<CartSummaryDto>.Ok(summary));
    }
}
```

参考 [第 5 章](./05-cross-bc-communication.md) 5.8 节的 12 条 Internal API 清单，确保命名、路由、鉴权符合规范：

| BC | 路由 | HTTP 方法 | 用途 |
|---|---|---|---|
| Product | `/internal/v1/products/skus/{skuId}` | GET | 查询 SKU 详情 |
| Product | `/internal/v1/products/skus/batch` | POST | 批量查询 SKU |
| Promotion | `/internal/v1/promotions/calculate` | POST | 计算订单优惠 |
| Promotion | `/internal/v1/promotions/lock-coupon` | POST | 锁定优惠券 |
| Promotion | `/internal/v1/promotions/release-coupons` | POST | 释放优惠券 |
| PointsMembership | `/internal/v1/points/trial-offset` | POST | 试算积分抵扣 |
| PointsMembership | `/internal/v1/points/freeze` | POST | 冻结积分 |
| PointsMembership | `/internal/v1/points/release` | POST | 释放积分 |
| UserAuth | `/internal/v1/users/{userId}/contacts` | GET | 查询用户联系方式 |
| Order | `/internal/v1/orders/{orderId}/status` | GET | 查询订单状态 |
| Payment | `/internal/v1/payments/{orderId}/info` | GET | 查询支付信息 |
| Notification | `/internal/v1/notifications/send` | POST | 发送通知 |

Internal API 鉴权流程：

1. 调用方在 `appsettings.json` 配置 `AntiCorruption:TargetInternalApiKeys:Cart` = 目标 BC 的 InternalApiKey
2. 调用方在发起 HttpClient 请求时通过 `X-Internal-Key` 头携带该 key
3. 被调用方 `InternalApiKeyMiddleware` 校验请求头值是否等于本 BC 的 InternalApiKey（从 Consul KV `leno/security/internal-key/cart` 读取）
4. 校验失败返回 401，成功继续管线

- [ ] **4. 用另一 BC 调用 Cart Internal API**：在 Order BC 添加防腐层客户端 `CartAntiCorruptionService`，调用刚加的 `/internal/v1/cart/{userId}/summary` 端点。防腐层模式（详见 [第 5 章](./05-cross-bc-communication.md) 5.5 节）确保 Order BC 不会把 Cart 的模型污染到本域

```csharp
// src/Services/Order/Leno.Order.Infrastructure/AntiCorruption/CartAntiCorruptionService.cs
internal sealed class CartAntiCorruptionService : AntiCorruptionBase, ICartAntiCorruptionService
{
    private const string TargetBc = "Cart";
    private const string SummaryEndpointPrefix = "internal/v1/cart/";

    public CartAntiCorruptionService(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options)
        : base(httpClient, options, TargetBc, SummaryEndpointPrefix)
    {
    }

    public async Task<CartSummaryDto?> GetSummaryAsync(Guid userId, CancellationToken ct)
        => await GetAsync<CartSummaryDto>($"{userId}/summary", ct);
}
```

并在 Order BC 的 `appsettings.json` 配置 `AntiCorruption:TargetInternalApiKeys:Cart`，值取自 Consul KV `leno/security/internal-key/cart`。

DI 注册（在 `Leno.Order.Infrastructure` 的 `ServiceCollectionExtensions`）：

```csharp
services.AddScoped<ICartAntiCorruptionService, CartAntiCorruptionService>();
services.AddHttpClient<CartAntiCorruptionService>((sp, c) =>
{
    var opts = sp.GetRequiredService<IOptions<AntiCorruptionOptions>>().Value;
    c.BaseAddress = new Uri(opts.TargetBcBaseUrl["Cart"]);
    c.DefaultRequestHeaders.Add("X-Internal-Key", opts.TargetInternalApiKeys["Cart"]);
})
.AddTransientHttpErrorPolicy(policy =>
    policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(100 * attempt)));
```

- [ ] **5. 用 Jaeger 观察跨 BC 链路**：本地启动 Order BC + Cart BC，调用 Order BC 的下单预览端点（会触发 `Order → Cart` 的 Internal API 调用），打开 http://localhost:16686 查找最近 Trace，应看到：

- Span 1: `Leno.Order.Api` `POST /api/orders/preview`
- Span 2: `Leno.Cart.Api` `GET /internal/v1/cart/{userId}/summary`（子 Span，父级是 Span 1）
- Span 3: `Leno.Cart.Infrastructure` `SELECT TOP 1 FROM Cart WHERE UserId = @p0`（EF Core 自动埋点）

`traceId` 在两个 BC 间透传，`X-Internal-Key` 头不写入 Span 属性（敏感信息）。详见 [第 8 章 可观测性](./08-observability.md) 8.3 节。

跨 BC 链路调试技巧：

1. 在 Order BC 发起请求前，记下 `X-Trace-Id` 头（如有自定义）
2. Jaeger Search → Tags 过滤 `http.route = /internal/v1/cart/{userId}/summary`
3. 找到目标 Trace 后展开，对照代码理解每个 Span 的来源
4. 异常场景：Span `error = true` + `exception.stacktrace` 给出堆栈

### 第四天验收标准

- [ ] Cart BC 暴露的 `/internal/v1/cart/{userId}/summary` 端点可用 Postman 调通（携带正确 `X-Internal-Key`）
- [ ] Order BC 通过防腐层客户端成功调用 Cart Internal API
- [ ] Jaeger 中能看到 Order → Cart 的跨 BC Span 树
- [ ] 单元测试覆盖防腐层客户端（用 Mock HttpMessageHandler 模拟 200/404/500 三种场景）

---

## 10.5 第五天：可观测与部署

第五天目标是从"代码能跑"升级到"系统可观测、可部署、可运维"——精读第 6-9 章掌握存储/缓存、安全/认证、可观测性、部署/运维四大主题，用 Jaeger 追踪一次完整请求、用 Grafana 看指标、读 Helm Chart 与 Runbook 理解生产部署形态。完成后你应该能独立排查一次"503 网关错误"故障。预计耗时 6-8 小时。

### 步骤清单

- [ ] **1. 精读第 6-9 章**：[06-storage-and-cache.md](./06-storage-and-cache.md)（EF Core/Redis/Elasticsearch）、[07-security-and-auth.md](./07-security-and-auth.md)（JWT/OAuth2/RBAC/XSS/CSRF）、[08-observability.md](./08-observability.md)（日志/追踪/指标三支柱）、[09-deployment-and-ops.md](./09-deployment-and-ops.md)（容器化/Helm/Consul/CI-CD/Runbook）

精读产出对照表：

| 章节 | 核心知识点 | 自检问题 |
|---|---|---|
| 第 6 章 | EF Core Code First / Redis 缓存策略 / ES 读模型 | 缓存击穿、穿透、雪崩的区别与防护？ |
| 第 7 章 | JWT 结构 / OAuth2 流程 / RBAC 模型 / XSS-CSRF 防护 | JWT 与 Session 的差异？Leno 用哪种？ |
| 第 8 章 | Serilog 结构化日志 / OpenTelemetry / Prometheus+Grafana | 三支柱是什么？为何不可相互替代？ |
| 第 9 章 | 多阶段 Dockerfile / Helm Chart / Consul / CI 9 Job | 蓝绿部署与金丝雀的差异？ |

- [ ] **2. 用 Jaeger 追踪完整请求**：构造一次完整下单链路（加购 → 预览 → 创建订单 → 模拟支付完成），在 Jaeger 中查找包含 10 个以上 Span 的 Trace，应能看到 Order → Product/Promotion/PointsMembership/Payment/Notification 多个 BC 协作。重点关注：

- Span 之间的父子关系（`parent_span_id`）
- 跨 BC 调用的 `service.name` 标签
- 数据库 Span 的 `db.system` / `db.operation`
- 异常 Span 的 `error` 标签与 `exception.stacktrace`

完整下单链路 Span 树示例：

```
[Span 1] Leno.ApiGateway POST /api/orders
  ├── [Span 2] Leno.Order.Api POST /api/orders
  │     ├── [Span 3] Leno.Order.Application OrderAppService.CreateAsync
  │     ├── [Span 4] Leno.Product.Api GET /internal/v1/products/skus/{skuId}
  │     │     └── [Span 5] EF Core SELECT TOP 1 FROM Skus
  │     ├── [Span 6] Leno.Promotion.Api POST /internal/v1/promotions/calculate
  │     ├── [Span 7] Leno.PointsMembership.Api POST /internal/v1/points/trial-offset
  │     ├── [Span 8] EF Core INSERT INTO Orders
  │     └── [Span 9] RabbitMQ Publish OrderCreatedIntegrationEvent
  └── [Span 10] Leno.Order.Api 200 OK Response
```

- [ ] **3. 用 Grafana 看指标**：打开 http://localhost:3000，使用预置仪表盘

| 仪表盘 | 路径 | 关注指标 |
|---|---|---|
| Leno Gateway | Dashboards → Leno Gateway | `gateway_requests_total` / `gateway_active_requests` / `gateway_5xx_error_rate` |
| Leno Business Services | Dashboards → Leno Business Services | 各 BC 的 `http_server_duration_seconds` / `http_server_requests_total` |

参考 [第 8 章](./08-observability.md) 8.4 节用 PromQL 查询：

```promql
# 网关 5xx 错误率
sum(rate(gateway_requests_total{status_code=~"5.."}[5m]))
  / sum(rate(gateway_requests_total[5m]))

# 各 BC P99 延迟
histogram_quantile(0.99,
  sum(rate(http_server_duration_seconds_bucket[5m])) by (le, service_name))

# 防腐层 gRPC vs HTTP 调用占比
sum(rate(anticorruption_grpc_request_total[5m])) by (service)
  / sum(rate(anticorruption_grpc_request_total[5m] + anticorruption_failure_total{path="http"}[5m])) by (service)

# 熔断器 Open 状态告警
anticorruption_circuit_open == 1
```

- [ ] **4. 阅读 Helm Chart**：通读 `deploy/helm/leno/` 目录结构，理解 Chart 如何把 docker-compose 编排能力迁移到 Kubernetes

> 来源：[deploy/helm/leno/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/deploy/helm/leno/)

```
deploy/helm/leno/
├── Chart.yaml              # Chart 元数据（name/version/appVersion）
├── values.yaml             # 默认配置（被环境 values 覆盖）
├── values-dev.yaml         # 开发环境（单副本、无 HPA）
├── values-staging.yaml     # 预发环境（2 副本 + HPA）
├── values-prod.yaml        # 生产环境（3 副本 + HPA + 更高资源上限）
└── templates/
    ├── _helpers.tpl        # 模板辅助函数
    ├── configmap.yaml      # 应用配置
    ├── deployment.yaml     # 工作负载
    ├── hpa.yaml            # 自动扩缩容
    ├── ingress.yaml        # 入口路由
    ├── migration-job.yaml  # EF Core 迁移 Job（Helm hook）
    ├── secret.yaml         # 敏感配置
    ├── service.yaml        # 服务端点
    └── NOTES.txt           # 安装后提示
```

详见 [第 9 章](./09-deployment-and-ops.md) 9.3 节 Helm Chart 结构详解。重点理解：

- `Chart.yaml` 中的 `appVersion` 与镜像 tag 对应
- `values-dev/staging/prod.yaml` 三环境差异化（副本数、HPA、资源 limits、镜像 tag）
- `migration-job.yaml` 通过 Helm `pre-upgrade` hook 在 BC 部署前执行 EF Core 迁移
- `hpa.yaml` 基于 CPU 利用率自动扩缩容（`targetCPUUtilizationPercentage: 70`）

- [ ] **5. 阅读 Runbook**：通读 [docs/runbooks/m4-grpc-poc-verification.md](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/runbooks/m4-grpc-poc-verification.md) 理解 Runbook 7 个固定章节（背景/前置条件/操作步骤/验证/回滚/常见问题/相关文档）的写作规范，重点是：

- Consul KV 配置切换的精确命令（curl + KV 路径）
- 1 周观察期的指标目标与数据源
- 紧急回滚的秒级生效机制（ConsulConfigWatcher 热更新）
- 4 周稳定运行验收 checklist

Runbook 关键命令示例（参考 [docs/runbooks/m4-grpc-poc-verification.md](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/runbooks/m4-grpc-poc-verification.md)）：

```bash
# 1. 写入 Consul KV 启用 Order BC 的 gRPC
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'true'

# 2. 观察日志（ConsulConfigWatcher 5 秒内拉取新配置）
kubectl logs deployment/leno-order-api -f | grep "UseGrpc"

# 3. 紧急回滚（1-2 秒内生效，无需重启）
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'false'
```

### 第五天验收标准

- [ ] Jaeger 中能找到包含 10+ Span 的完整下单链路 Trace
- [ ] Grafana 中能看到各 BC 的实时请求量与延迟
- [ ] 能口述 Helm Chart 8 个模板的职责
- [ ] 能复述 Runbook 7 个固定章节与紧急回滚命令

---

## 10.6 提交首个 PR

5 天学习结束后，把第 3 天的 `CartItem.Remark` 字段变更与第 4 天的 Internal API 端点合并为一个 PR 提交。PR 是 Pull Request 的缩写，是开发者向主仓库发起"请把我的分支合并进来"的协作请求，是 GitHub/GitLab 等代码托管平台的标准协作单元。Leno 强制使用 Conventional Commits 规范提交，并要求至少 1 名 reviewer 通过才能合并。预计耗时 2-4 小时。

### 步骤清单

- [ ] **1. 创建 feature 分支**：从最新 `main` 切出特性分支，命名遵循 `feat/<scope>-<topic>` 或 `fix/<scope>-<topic>`

```bash
# 拉最新 main
git checkout main
git pull origin main

# 切特性分支
git checkout -b feat/cart-item-remark

# 确认当前分支
git branch --show-current
# 期望输出：feat/cart-item-remark
```

分支命名遵循 `feat/<scope>-<topic>` / `fix/<scope>-<topic>` / `refactor/<scope>-<topic>` / `perf/<scope>-<topic>` / `docs/<scope>-<topic>`，scope 用 BC 名小写。

- [ ] **2. 用 Conventional Commits 提交**：把第 3-4 天的改动按 Conventional Commits 规范分多个 commit 提交。Conventional Commits（约定式提交规范）要求提交信息形如 `type(scope): subject`，便于自动生成变更日志与版本号

```bash
# 暂存并提交 Domain 层变更
git add src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs
git commit -m "feat(cart): 添加购物车项备注字段"

# 暂存并提交 Application 层变更
git add src/Services/Cart/Leno.Cart.Application/DTOs/CartItemDtos.cs \
        src/Services/Cart/Leno.Cart.Application/Validators/CartValidators.cs
git commit -m "feat(cart): DTO 与校验器支持备注字段"

# 暂存并提交 Infrastructure 层变更
git add src/Services/Cart/Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Migrations/*
git commit -m "feat(cart): EF Core 映射与迁移支持备注字段"

# 暂存并提交 Internal API 端点
git add src/Services/Cart/Leno.Cart.Api/Controllers/InternalCartsController.cs
git commit -m "feat(cart): 暴露购物车摘要 Internal API 端点"

# 暂存并提交测试
git add src/Services/Cart/Leno.Cart.Domain.Tests/CartItemTests.cs
git commit -m "test(cart): 补充备注字段单元测试"
```

Conventional Commits 类型速查：

| type | 用途 | 示例 |
|---|---|---|
| `feat` | 新功能 | `feat(cart): 添加购物车项备注字段` |
| `fix` | Bug 修复 | `fix(order): 修复下单时金额计算溢出` |
| `refactor` | 重构（无行为变化） | `refactor(product): 抽取 SKU 查询基类` |
| `perf` | 性能优化 | `perf(gateway): 缓存路由配置降低延迟` |
| `docs` | 文档 | `docs(handbook): 新增第 10 章新人上手清单` |
| `test` | 测试 | `test(cart): 补充备注字段单元测试` |
| `chore` | 构建/工具/依赖 | `chore(deps): 升级 Serilog 到 4.0` |
| `ci` | CI 配置 | `ci(workflows): 新增 proto-lint Job` |

提交规范要求：

- **subject 用中文**，简洁明了（不超过 50 字符为佳）
- **scope 用 BC 名小写**（如 `cart` / `order` / `product` / `gateway` / `handbook`）
- **type 必须是上表 8 类之一**，CI 会校验
- **body 与 footer 可选**，多行时用空行分隔
- **breaking change** 在 footer 加 `BREAKING CHANGE: <说明>`

- [ ] **3. 推送分支到远程**：推送特性分支到远程仓库，便于发起 PR

```bash
git push -u origin feat/cart-item-remark

# 期望输出：
# Enumerating objects: 25, done.
# Counting objects: 100% (25/25), done.
# ...
# To github.com:leno/leno.git
#  * [new branch]      feat/cart-item-remark -> feat/cart-item-remark
# branch 'feat/cart-item-remark' set up to track 'origin/feat/cart-item-remark'
```

- [ ] **4. 创建 PR**：在 GitHub/GitLab 上发起 Pull Request，base 选 `main`，compare 选 `feat/cart-item-remark`。PR 描述按模板填写：

> **PR 模板位置**：仓库使用 `.github/pull_request_template.md`（即 [`.github/pull_request_template.md`](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/.github/pull_request_template.md)），手动创建 PR 时会自动加载。Leno 暂未在 `docs/` 下放置 `docs/pr-template.md`，如需查阅模板内容请直接阅读 `.github/pull_request_template.md`。

PR 模板包含 6 个固定章节：

1. **变更说明**：简要描述本 PR 做了什么、为什么做。例：本 PR 为 CartItem 新增 `Remark` 备注字段，并暴露 `GET /internal/v1/cart/{userId}/summary` Internal API 供 Order BC 下单预览时查询购物车摘要。
2. **变更类型**：勾选 `feat/fix/refactor/perf/docs/test/chore/ci` 中的一项或多项。本 PR 勾选 `feat` 与 `test`。
3. **关联 Issue / Spec**：关联的 issue 号或 spec 文档路径（如 `Implements docs/spec/03-购物车域.md`）
4. **影响范围**：
   - **限界上下文**：Cart（如 `Cart`）
   - **变更层**：Domain / Application / Infrastructure / Api / Contracts / Tests / Docs / Infra（如 `Domain/Application/Infrastructure/Api/Tests`）
   - **向后兼容**：是 / 否；若否则说明迁移路径（如 `是`，新增字段 nullable，旧客户端不传 remark 仍可正常工作）
5. **验证清单**：8 项 checkbox，包括 `dotnet build` 通过、`dotnet test` 通过、新增功能有测试、敏感配置未硬编码、Domain 层未跨 BC 引用、域事件与集成事件未混用、错误码已映射、文档已同步
6. **部署注意事项**：如需特别部署步骤（EF 迁移、Consul KV 写入、buf generate、helm upgrade），在此说明。例：本 PR 需在部署前执行 EF Core 迁移 `dotnet ef database update`，无需 Consul KV 或 buf generate。

填写完整后提交 PR，平台会自动触发 CI 流水线。

- [ ] **5. 等 CI 通过**：PR 创建后会触发 CI 流水线（详见 [第 9 章](./09-deployment-and-ops.md) 9.5 节 9 个 Job），重点关注：

| Job | 用途 | 失败影响 |
|---|---|---|
| `build-solution` | 编译 `Leno.slnx`，0 错误 0 警告 | 阻断合并 |
| `integration-tests` | 跑全量集成测试（含 Testcontainers） | 阻断合并 |
| `migration-check` | 校验 EF Core 迁移可前向应用 | 阻断合并 |
| `proto-lint-breaking` | 校验 .proto 不向后不兼容 | 阻断合并（若改了 proto） |
| `build-services` matrix 12 | 各 BC 独立构建 | 不阻断但需关注 |
| `docker-build` matrix 12 | 各 BC Docker 镜像构建 | 不阻断但需关注 |
| `validate-compose` | 校验 docker-compose 语法 | 不阻断但需关注 |
| `generate-grpc-contracts` | 重新生成 gRPC 客户端代码 | 阻断合并（若改了 proto） |
| `staging-integration-tests` | staging 环境集成测试 | 阻断合并 |

CI 失败时根据日志修复后 `git push` 续传，无需关闭 PR。常见 CI 失败原因：

- `build-solution` 报编译错误：本地 `dotnet build Leno.slnx` 复现并修复
- `integration-tests` 报某测试 Failed：本地 `dotnet test --filter "<TestName>"` 复现
- `migration-check` 报 `Migration X is not forward-only`：检查迁移是否含 `DropColumn` 等破坏性操作
- `proto-lint-breaking` 报字段编号变更：参考 ADR 0005 proto 向后兼容约束

- [ ] **6. 等 reviewer 审阅**：CI 全绿后请至少 1 名 reviewer 进行 Code Review（代码审阅，PR 合并前由团队其他成员对变更进行检查的协作环节，关注可读性、正确性、安全性与一致性）。Reviewer 关注点：

- 命名是否符合 [编码规范](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/编码规范.md)
- 是否遵循 [第 4 章](./04-code-patterns.md) 四层项目结构与开发模板
- Domain 层是否纯净（无 Infrastructure/SharedContracts 引用）
- 测试覆盖是否充分（正常/异常/边界）
- 是否引入硬编码配置（应走环境变量或 Consul KV）
- PR 描述与验证清单是否完整

Reviewer 提出修改意见后，本地修改 → `git commit --fixup` 或新 commit → `git push` → 在 PR 中回复评论。所有意见 resolve 后 reviewer approve，最后由 maintainer 执行 Squash Merge 合并到 `main`。

### 第六天验收标准

- [ ] feature 分支已推送且 PR 已创建
- [ ] CI 5 个 Job 全部通过
- [ ] 至少 1 名 reviewer approve
- [ ] PR 已合并到 `main`（Squash Merge），本地 `main` 已同步
- [ ] PR 描述包含变更说明、变更类型、关联 Spec、影响范围、验证清单勾选、部署注意事项 6 个章节
- [ ] 本地分支已清理（`git branch -d feat/cart-item-remark` + 删除远程分支）

---

## 10.7 进阶学习路径

完成首周 5 天上手 + 1 天提 PR 后，进入 1-3 个月的进阶学习阶段。下列 5 项进阶路径按"广度 → 深度"排序，建议按顺序推进：

| # | 进阶路径 | 资源位置 | 预计耗时 | 关键产出 |
|---|---|---|---|---|
| 1 | 通读 13 篇需求文档 | [docs/spec/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/spec/) | 2-3 周 | 能独立绘制 11 BC 上下文映射图与跨 BC 通信矩阵 |
| 2 | 研读 7 个 ADR 架构决策记录 | [docs/decisions/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/) | 1-2 周 | 理解 gRPC 双轨、熔断器三状态机、防腐层模式、Guid 迁移等关键决策 |
| 3 | 精读 Runbook 与运维手册 | [docs/runbooks/m4-grpc-poc-verification.md](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/runbooks/m4-grpc-poc-verification.md) + [第 9 章](./09-deployment-and-ops.md) | 1 周 | 能独立执行一次 gRPC 切换与紧急回滚演练 |
| 4 | 实施 Plan 中的下一阶段任务 | [docs/superpowers/plans/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/superpowers/plans/) | 按迭代节奏 | 独立承担 1-2 个 Plan 任务，完成 spec → PR → 合并全流程 |
| 5 | 研读下一阶段优化 spec | [docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md) | 1-2 周 | 理解 v2 阶段优化方向，能参与下一阶段 Plan 拆分讨论 |

7 个 ADR 清单速查（位于 `docs/decisions/`）：

| ADR | 标题 | 关键决策 |
|---|---|---|
| 0001 | [grpc-dual-track-with-http-fallback](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0001-grpc-dual-track-with-http-fallback.md) | gRPC + HttpClient 双轨，Consul KV 控制切换 |
| 0002 | [circuit-breaker-three-state-machine](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0002-circuit-breaker-three-state-machine.md) | Closed/Open/HalfOpen 三状态机 |
| 0003 | [anticorruption-dispatcher-adapter-pattern](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md) | `AntiCorruptionDispatcher` 模板方法 + 适配器 |
| 0004 | [iorderstatus-provider-refactor](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0004-iorderstatus-provider-refactor.md) | ReviewAfterSales 调 Order 抽象为 `IOrderStatusProvider` |
| 0005 | [proto-backward-compatibility-constraint](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0005-proto-backward-compatibility-constraint.md) | .proto 字段只增不删，buf breaking 校验 |
| 0006 | [guid-int64-poc-simplification-history](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0006-guid-int64-poc-simplification-history.md) | POC 阶段 Guid → int64 简化历史与限制 |
| 0007 | [guid-string-migration-strategy](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/decisions/0007-guid-string-migration-strategy.md) | 生产化阶段 Guid → string 迁移策略 |

---

## 要点回顾

本章把前序 9 章的知识点串成一条"5 天上手 + 1 天提 PR + 进阶路径"的实战清单，核心要点：

1. **第一天环境就绪**：`git clone` + `mise install dotnet@10.0.301` + `docker compose -f docker-compose.yml up -d` + 验证 Consul/Grafana/Jaeger 三大面板 + 阅读 [手册 README](./README.md) 与 [第 1 章](./01-project-overview.md)，6 步完成本地环境就绪
2. **第二天业务理解**：精读第 1-3 章 + 浏览 13 篇需求文档 + `dotnet test` 跑通单元测试 + `GET http://localhost:8080/api/products` 调通网关 API + Jaeger 查看链路，5 步建立系统全貌认知
3. **第三天动手开发**：精读第 4 章 + 修改 [src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs) 同目录 `CartItem.cs` 加 `Remark` 字段 + 跑测试 + 本地启动 Cart BC + Postman 验证，5 步完成首次代码改动
4. **第四天跨 BC 通信**：精读第 5 章 + 理解 Outbox 模式 + 添加 Internal API 端点（参考 5.8 节 12 条 Internal API 清单）+ 用 Order BC 防腐层调用 + Jaeger 观察链路，5 步掌握跨 BC 协作
5. **第五天可观测与部署**：精读第 6-9 章 + Jaeger 追踪完整请求 + Grafana 看指标 + 阅读 [deploy/helm/leno/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/deploy/helm/leno/) Helm Chart + 阅读 [Runbook](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docs/runbooks/m4-grpc-poc-verification.md)，5 步掌握运维基础
6. **第六天提交 PR**：创建 feature 分支 + Conventional Commits 提交（如 `feat(cart): 添加购物车项备注字段`）+ 推送 + 创建 PR + 等 CI 通过 + 等 reviewer 审阅，6 步完成首个 PR 全流程，PR 模板位于 [`.github/pull_request_template.md`](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/.github/pull_request_template.md)（Leno 暂未在 `docs/` 下放置 `docs/pr-template.md`）
7. **进阶学习路径**：13 篇需求文档（`docs/spec/`）+ 7 个 ADR（`docs/decisions/`）+ Runbook + Plan 实施 + 下一阶段优化 spec（`docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md`），5 项路径覆盖 1-3 个月进阶规划

## 常见问题

**Q1：`docker compose up` 后某 BC 一直 restarting，怎么排查？**

A：按 [第 9 章](./09-deployment-and-ops.md) 9.10 Q1 的步骤排查：①`docker compose logs <service>` 查看具体异常；②确认 `.env` 中的 `MSSQL_SA_PASSWORD` / `RABBITMQ_DEFAULT_USER` 等环境变量已配置；③确认依赖的 `sqlserver`/`redis`/`rabbitmq`/`elasticsearch`/`consul` 已 healthy；④`start_period: 30s` 期间 healthcheck 失败是正常的，超出 30 秒仍失败才标记 unhealthy；⑤检查 BC `appsettings.json` 中 `ConnectionStrings` 配置是否指向正确的容器名（如 `leno-sqlserver` 而非 `localhost`）。

**Q2：`dotnet test` 跑集成测试时 Testcontainers 启动失败？**

A：常见原因：①Docker Desktop 未运行，先 `docker info` 验证；②端口冲突，确认 1433/6379/5672/9200 端口未被其他进程占用，可用 `netstat -ano | findstr :1433` 查占用；③Windows 文件共享权限不足，Testcontainers 需挂载测试代码到容器内，需在 Docker Desktop Settings → Resources → File Sharing 添加项目目录；④内存不足，Testcontainers 同时启动 4 个容器需 4GB+ 内存，可在 `ContainerFixture` 配置中调整为顺序启动；⑤Linux 容器与 Windows 容器混合，确认 Docker Desktop 使用 Linux 容器模式（默认）。

**Q3：调用 Internal API 返回 401，怎么定位？**

A：Internal API 用 `X-Internal-Key` 请求头鉴权（详见 [第 5 章](./05-cross-bc-communication.md) 5.8 节）：①确认请求头名为 `X-Internal-Key`（不是 `X-Internal-Key-Id`）；②确认值是**目标 BC** 的 InternalApiKey（不是调用方自己的），从 Consul KV `leno/security/internal-key/{target-bc}` 读取；③确认调用方 `appsettings.json` 的 `AntiCorruption:TargetInternalApiKeys` 字典配置了目标 BC 的 key；④确认被调用方 `InternalApiKeyMiddleware` 已在 `Program.cs` 注册（`app.UseInternalApiKey()`）；⑤用 curl 直接测试 `curl -H "X-Internal-Key: <key>" http://localhost:5152/internal/v1/products/skus/test`。

**Q4：Conventional Commits 提交信息写错了，已 push 怎么改？**

A：①若仅最近一个 commit 未被他人拉取，`git commit --amend` 修改后 `git push --force-with-lease` 续传（`--force-with-lease` 比 `--force` 更安全，会检查远程是否被他人更新）；②若已开 PR 且有多个 commit，可在 PR 描述中说明，由 maintainer 在 Squash Merge 时统一修改 squash 提交信息；③严禁对已合入 `main` 的提交做 rebase 或 amend。最佳实践：本地每次 commit 前先看 `git log -1` 与即将提交的 type/scope 是否一致。

**Q5：PR 的 CI 跑 integration-tests Job 失败，本地却能通过？**

A：CI 与本地的差异主要在：①CI 用 GitHub Actions 的 ubuntu-latest runner，Testcontainers 启动需 `sudo` 权限（已配置）；②CI 跑全量 `Leno.slnx`，可能某个不相关 BC 的测试失败连累整体 Job；③CI runner 内存有限（7GB），4 个 Testcontainers 同时启动可能 OOM，可临时在 PR 描述中标注"重试一次"。建议先看 Job 日志定位失败的具体测试名再 `dotnet test --filter "FullyQualifiedName~<TestName>"` 本地复现。若是偶发性失败（如网络超时、容器启动慢），可在 PR 描述中说明并 re-run failed jobs。

**Q6：Jaeger 中找不到跨 BC 的 Trace，怎么排查？**

A：①确认两个 BC 都启用了 OpenTelemetry（`AddLenoOpenTelemetry` 已在 `Program.cs` 注册，详见 [第 8 章](./08-observability.md) 8.3 节）；②确认 `OTEL_EXPORTER_OTLP_ENDPOINT` 环境变量指向 Jaeger 的 OTLP 端点（默认 `http://leno-jaeger:4317` 或 `http://localhost:4317`）；③确认请求确实跨 BC（如 Order → Product），单 BC 请求不会有跨 BC Span；④Jaeger 默认只保留 1 小时数据，超过后查不到，需重新发起请求；⑤检查 Jaeger Service 下拉列表是否包含目标 BC，若不包含说明 OTLP 上报失败，看 BC 日志是否有 OTLP 错误。

**Q7：feature 分支合并后本地怎么清理？**

A：标准清理流程：①`git checkout main` 切回主分支；②`git pull origin main` 拉最新合并后的代码；③`git branch -d feat/cart-item-remark` 删除本地分支（已合并则无 `-D` 警告）；④`git push origin --delete feat/cart-item-remark` 删除远程分支。若 PR 用 Squash Merge 合并，本地分支会因 commit hash 不在 main 历史中而被判定"未合并"，需用 `git branch -D` 强制删除（这是预期行为）。建议合并后立即清理，避免分支堆积。

**Q8：如何选择进阶学习路径的优先级？**

A：按"业务理解 → 架构理解 → 运维理解 → 实施贡献"的顺序：①先通读 13 篇需求文档建立业务全貌（路径 1）；②再研读 7 个 ADR 理解关键架构决策（路径 2）；③精读 Runbook 掌握运维机制（路径 3）；④最后参与 Plan 实施（路径 4）与下一阶段 spec 讨论（路径 5）。若团队有紧急任务，可跳过路径 2-3 直接进入路径 4 在实战中边做边学，但路径 1（需求文档）不可跳过，否则会出现"代码看懂但不知业务为何这么设计"的盲区。

**Q9：5 天上手清单的节奏太赶，可以延长吗？**

A：完全可以根据个人节奏调整。建议的最少进度：①第一周必须完成第一天 + 第二天 + 第六天（即环境就绪、业务理解、提交一个最小的 PR，哪怕是改文档）；②第二周完成第三-五天（动手开发 + 跨 BC 通信 + 可观测）；③第三周开始进阶路径。手册的 5 天节奏是参考目标，不是硬性 deadline。

## 结语

恭喜你读完了整本《Leno 电商平台系统开发手册》。从第 1 章的业务定位、4 类角色、8 项核心业务目标，到第 2 章的本地环境一键启动；从第 3 章的 11 BC 限界上下文与上下文映射，到第 4 章的四层项目结构与 6 类开发模板；从第 5 章的跨 BC 通信（集成事件 + Outbox 模式 + 防腐层 + gRPC 双轨 + 12 条 Internal API），到第 6 章的 EF Core + Redis + Elasticsearch 三类存储；从第 7 章的 JWT + OAuth2 + RBAC + XSS/CSRF 防护，到第 8 章的可观测性三支柱（Serilog + OpenTelemetry + Prometheus/Grafana）；从第 9 章的容器化 + Helm Chart + Consul + CI/CD + Runbook，到本章的 5 天上手 + 1 天 PR + 进阶路径——你现在已经具备了独立承担 Leno 一个 BC 开发任务的全部知识基础。

但**读完手册只是起点，不是终点**。真正的成长发生在你按下 F5 调试 Cart BC 时、在 Jaeger 中追踪一次跨 BC 调用时、为 reviewer 的 Code Review 评论而反复修改时、在 Grafana 中看到自己写的指标第一次画出来时。把这些实战场景当作手册的"扩展章节"，每解决一个实际问题，你对系统的理解就深一层。

后续学习方向：

- **深入 DDD 与微服务架构**：读 Eric Evans《领域驱动设计》与 Vaughn Vernon《实现领域驱动设计》，理解 Leno 实践背后的理论根基。重点对照"聚合根"、"值对象"、"上下文映射"、"防腐层"四个概念在 Leno 代码中的落地
- **深入 .NET 10 与云原生**：跟 .NET 10 release notes、CNCF landscape，理解 AOT、OTel、gRPC、Kubernetes 等技术在 .NET 生态的最新进展。Leno 拥抱最新技术栈，了解前沿有助于参与团队的技术决策
- **深入电商业务**：与产品/运营同学 1:1，理解买家/卖家/运营/系统管理员四类角色的真实痛点，把"代码读懂"升级为"业务读透"。技术是为业务服务的，脱离业务的技术优化往往方向跑偏
- **参与开源贡献**：把阅读过程中发现的手册错误、代码坏味道、文档缺失提 PR 修，从"读者"转变为"作者"。`docs(handbook): ...` 类型提交是新人最合适的起步 PR

Leno 是一个不断演进的系统，本手册也会持续更新。若你在阅读与实战中发现任何错误、不清晰或有改进建议，欢迎提 PR 修改对应章节——这正是 Conventional Commits 中 `docs(handbook): ...` 类型提交的典型场景。你的每一条改进都会让下一位新人少走一段弯路，这也是 Leno 团队"Subagent-Driven + Conventional Commits + PR 模板 + 11 条硬约束"开发模式的初衷——让协作可复现、可审阅、可传承。

最后送给新人三句话：

1. **遇到问题先查手册再问人**——手册已覆盖 80% 常见问题，问人前先看"常见问题"与"要点回顾"
2. **每次改动都跑测试**——`dotnet test` 是你最可靠的朋友，5 分钟测试能省 5 小时排查
3. **每个 PR 都是学习机会**——reviewer 的每条评论都是知识传递，认真对待每一条

祝你在 Leno 的开发旅程顺利，期待你的第一个 PR。

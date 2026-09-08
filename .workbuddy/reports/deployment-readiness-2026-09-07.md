# Leno 项目部署就绪度评审报告

> 评审人：架构师 高见远（software-architect）｜汇总：齐活林（交付总监）｜日期：2026-09-07

## 总体就绪度评分：**55 / 100**

**结论**：工程化基础扎实（CI 覆盖全面、12 套 Dockerfile、Helm chart 结构完整、可观测性有雏形），但存在 4 项会导致生产部署"必然失败"的阻塞项。距生产上线约需 **2-3 周整改**。

---

## 一、就绪度清单

| 维度 | 评级 | 现状与证据 |
|---|---|---|
| 构建可运行性 | ⚠️ 基本通过 | `dotnet build Leno.slnx -c Release` 编译通过（本机仅剩 obj 权限类错误，非代码问题）；build_errors.txt 的 Payment CS0246 已消失；743 个警告（多为 CS0618） |
| 仓库卫生 | ✅ | 553 个 dll / 271 个 cache / node_modules 均未被 git 追踪，.gitignore 有效 |
| 容器化 | ⚠️ | 12 套 Dockerfile（11 BC + 网关）；**8 个服务无 Dockerfile**：Identity、UserCenter、AccessControl、Inventory、Points、Membership、Review、AfterSales；`.dockerignore` 未排除 node_modules/TestResults；容器以 root 运行 |
| Helm Chart | ⚠️ 结构好、配置链断裂 | deployment 探针与代码端点一致（/health/live、/health/ready）、HPA、pre-install/upgrade 迁移 hook、三套 values 分环境；但见 P0-1/2/3；ingress 无 TLS、无 startupProbe/PDB/NetworkPolicy、**声明了 serviceMonitor 开关但无对应模板** |
| 环境变量与密钥 | ❌ | Helm 只注入 `ConnectionStrings__Default` 等 4 个变量，代码实际读 `ConnectionStrings:{Bc}Db`（无 Default 键）、`Redis:Configuration`（fallback localhost）、`RabbitMQ:Host/Password`（fallback localhost/guest）；5 个生产 Secret chart 不创建、无创建脚本；`.env.example` 缺 `LENO_INTERNAL_API_KEY_SHARED`、`OAUTH2_PUBLIC_BASE_URL` |
| 生产基础设施 | ❌ | docker-compose 仅为开发栈（Consul dev 模式、SQL Express、Redis 无密码）；values.yaml 自认"chart 不部署基础设施"但仓库内零方案；Consul 是网关服务发现单点依赖 |
| 数据库迁移 | ✅ 方向对，执行通道断 | `scripts/generate-migration-scripts.ps1` 已产出 11 BC 幂等 SQL + CI 空库验证；但 migration-job 用 aspnet 运行时镜像跑 `dotnet ef`（无 SDK），必然失败 |
| CI/CD | ⚠️ | ci.yml 覆盖 build/测试/覆盖率/11 服务 docker build/buf lint/迁移验证/Pact/前端 build；**只 build 不 push，无 CD**；镜像 tag 固定 "1.0.0" |
| 前端 | ❌ | 4 个应用仅 2 个进 CI，无 Dockerfile/nginx 托管方案 |
| 可观测性 | ⚠️ | Serilog 仅 Console JSON；OTel→Jaeger（compose）；prometheus.yml 静态 targets 适配 compose 主机名，K8s 下失效；alertmanager webhook 为 REPLACE_ME 占位 |
| 安全 | ⚠️ | 亮点：InternalApiKey fail-closed + 固定时间比较；短板：**12 个 appsettings.Docker.json 明文密码入库**（视为已泄露，需轮换）、无 TLS、JWT/内部 API key 无轮换、root 容器 |
| 灰度路由 | ⚠️ | 网关 appsettings.json 第 178-200 行灰度路由指向 identity/access-control/user-center/review/after-sales/membership 集群——这些服务**永远不会有实例注册**，灰度一开必 503；**Inventory 未入路由清单，已确认为遗漏，需补齐路由+集群定义** |

## 二、阻塞项 P0（不解决无法上生产）

1. **配置链改造（主通道：Consul KV）**【已确认决策：Consul KV 规划为生产配置主通道】：
   - 生产全量业务配置（`ConnectionStrings:{Bc}Db`、`Redis:Configuration`、`RabbitMQ:Host/Username/Password`、ES、ServiceUrls）预写入 Consul KV，由 `ConsulReloadableConfigurationProvider`（WebApplicationExtensions.cs 第 77-84 行）加载。
   - **先做 KV 覆盖面审计**：逐键核对 provider 实际加载/热更新的键与代码读取键是否一致（重点验证各 BC `Dependencies/ServiceCollectionExtensions.cs`），缺失键先补 env 注入兜底。
   - deployment.yaml 保留**最小引导 env**：`Consul__Address`、`Security__Jwt__SecretKey`、`OpenTelemetry__OtlpEndpoint`、`ASPNETCORE_ENVIRONMENT`（鸡生蛋问题：应用需先知道 Consul 地址才能读 KV）。
   - 新增**启动 fail-fast 校验**：关键配置键缺失（将 fallback 到 localhost/默认值）时拒绝启动——当前静默 fallback localhost 是生产重大隐患。
   - 新增 **Consul KV 种子化方案**：init Job 或脚本在部署前写入/校验 KV；Consul 开启 ACL，KV 变更走变更单。
2. **migration-job 改造**：改为执行已产出的幂等 SQL（busybox + sqlcmd 侧车或专用 migrator 镜像）；先在 staging 验证 hook 时序。
3. **密钥注入链打通**：启用 External Secrets Operator 或提供 `kubectl create secret` 清单/脚本；删除 secret.yaml 的 `REPLACE_ME` 占位逻辑，缺失即 fail（helm required 校验）。注意：即使 KV 为主通道，JWT SecretKey 等引导密钥仍走 K8s Secret 注入。
4. **生产基础设施方案落档**：SQL Server（AG/云 RDS）、Redis（哨兵/集群）、RabbitMQ（镜像队列）、Consul（server 3 节点 + 持久化 + ACL）各出一份生产部署/托管选择 + values 连接配置。**Consul 既是 KV 配置主通道又是服务发现核心，单点影响加倍，高可用优先级最高。**
5. **灰度范围与服务补齐（含 Inventory）**【已确认决策：Inventory 未入网关路由属遗漏】：
   - 网关 `appsettings.json` 补充 Inventory 路由与集群定义（灰度清单 + Consul 服务发现集群）。
   - 8 个无 Dockerfile 服务（Identity、UserCenter、AccessControl、**Inventory**、Points、Membership、Review、AfterSales）补齐容器化 + CI docker build + helm 条目；首版若仅上线 11 BC，需同时关闭灰度路由并下线无效集群定义。

## 三、高风险项 P1（能上但风险大）

- 无 CD：镜像无 registry 推送、无 helm upgrade 自动化；镜像 tag 应改为 git sha 不可变 tag。
- 明文密码已入库：清洗 12 个 appsettings.Docker.json 并**轮换全部已泄露凭据**。
- 无 TLS（ingress 无 tls 段，内部全 HTTP）。
- Prometheus 生产抓取缺失（无 ServiceMonitor 模板/注解）；alertmanager webhook 占位。
- 日志集中采集未定（Serilog 仅 Console，缺 Loki/Fluent Bit 等方案）。
- JWT/内部 API key 无轮换机制；Consul dev 模式单点。

## 四、改进项 P2

- startupProbe、PDB、NetworkPolicy、非 root 容器、优化 .dockerignore。
- 前端容器化/nginx 托管 + 4 应用全部进 CI；日志与 trace 关联。
- 清理 743 编译警告、渐进开启 TreatWarningsAsErrors。

## 五、推荐部署流程（分阶段）

1. **阶段 1｜修配置链（P0-1/2/3，主通道 Consul KV）**：审计 KV 覆盖面 → Consul KV 种子化脚本/init Job → deployment.yaml 最小引导 env + fail-fast 校验 → 改 migration-job/secret 模板 → `helm template` + `helm lint` 验证 → dev 集群（values-dev）空库全量部署走通。
2. **阶段 2｜补基础设施（P0-4）**：选定中间件生产形态，staging 搭建（Consul 3 节点 server + ACL——配置主通道+服务发现双职责，最先落地），手工建 5 个 Secret 验证注入，预写 KV 并验证热更新。
3. **阶段 3｜镜像流水线**：CI 增加 push 步骤（tag=git sha），values 参数化 imageRegistry/tag。
4. **阶段 4｜Staging 端到端验证**：迁移 Job → 12 服务就绪 → 网关路由（含 Inventory 补齐后验证）→ 探针全绿 → Prometheus 抓取生效 → 集成测试与冒烟（下单/支付主链路）。
5. **阶段 5｜灰度决策（P0-5）**：明确首版上线范围（建议仅 11 BC，关闭灰度路由并下线无效集群定义）；8 个缺失服务另行排期补齐。
6. **阶段 6｜生产发布**：迁移 hook → `helm upgrade --atomic --timeout` → 就绪探针验证 → 网关域名冒烟。
7. **阶段 7｜可观测性与告警生效**：ServiceMonitor/注解、alertmanager 真实 webhook、Grafana 看板核对。
8. **回滚预案**：`--atomic` 自动回滚 + 幂等迁移脚本可重放；镜像按 sha 秒回上一版本；Consul KV 配置变更走独立变更单。

## 六、待确认事项

1. 干净环境（CI）下全量编译是否 0 错误（本机受 obj 权限干扰）。—— 待 CI 首次完整构建验证
2. ~~Consul KV 是否规划为生产配置主通道~~ —— **已确认（2026-09-07）：是**。P0-1 已按此修订：KV 覆盖面审计 + 种子化 + 最小引导 env + fail-fast。
3. ~~Inventory 未出现在网关灰度清单中，是有意拆分还是遗漏~~ —— **已确认（2026-09-07）：遗漏**。需补网关路由/集群定义，并纳入 8 服务容器化补齐清单。

# Leno 生产基础设施方案

> 版本：v1.0（部署就绪度评审 P0-4 落档）
> 范围：Leno 电商平台生产/预发基础设施选型、部署清单、备份恢复与网络安全方案。
> 本文档只做方案落档，不修改任何源码与 Helm 模板；引用现有 values 的路径均已注明。

---

## 0. 现状与设计前提（代码事实）

以下结论均来自对仓库代码与部署物的核查，是本方案选型的依据：

| 事实 | 出处 |
|------|------|
| 19 个 BC 各自独立数据库（Database-per-BC），连接串键 `{Bc}Db` 共 19 个 | `deploy/consul/kv-seed.json`、`deploy/scripts/create-secrets.ps1`（BcList） |
| Consul KV 是生产配置主通道，`leno/config/**` 优先级高于 env 兜底，且 `ConsulConfigWatcher` 通过阻塞查询监听 KV 实现灰度开关（`leno/anticorruption/use-grpc/{bc}`） | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulReloadableConfigurationProvider.cs`、`ConsulConfigWatcher.cs`、`ConsulGrayReleaseService.cs` |
| 网关通过 Consul 做服务发现（YARP `IDestinationResolver`），各服务通过 Consul Agent HTTP API（`AgentServiceRegistration`）自注册 | `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs`、`src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ConsulServiceRegistrationExtensions.cs` |
| Redis 实际用途共 6 类：①通用缓存 `CacheService`；②布隆过滤器（**基于 Bitmap 自实现，不依赖 RedisBloom 模块**）；③事件总线幂等去重 `RedisIdempotencyStore`；④JWT 黑名单 `JwtBlacklistService`；⑤用户会话 `RedisUserSessionStore`；⑥滑动窗口限流（Lua 脚本）。另有缓存失效 Pub/Sub | `Leno.Infrastructure.Caching`、`Leno.Infrastructure.EventBus/EventBus/RedisIdempotencyStore.cs`、`Leno.ApiGateway/Services/JwtBlacklistService.cs`、`Leno.ApiGateway/Middleware/CacheMiddleware.cs` |
| 未发现分布式互斥锁（RedLock 类）用法，Redis 不承载强一致锁 | 全量检索无 `RedLock`/`DistributedLock` 命中 |
| Elasticsearch 是 CQRS 读模型存储（索引 CRUD + 搜索，事件驱动同步），**不是**日志检索 | `Leno.Infrastructure.ReadModel/ReadModel/EsReadModelRepository.cs`、`ReadModelSyncConsumerBase.cs` |
| 消息总线：MassTransit 8.3.6 + MassTransit.RabbitMQ，发布到 Topic 交换机按事件类型路由，队列默认 durable classic | `Leno.Infrastructure.EventBus.csproj`、`RabbitMqEventBus.cs` |
| 开发栈为 docker-compose：Consul `agent -dev`、SQL Server 2019 **Express**、Redis 无密码、RabbitMQ 3.12 单节点、ES 关安全 | `docker-compose.yml` |
| Helm chart 不部署基础设施，Staging/Prod values 已开启 `externalSecrets`（ESO，backend 默认 consul） | `deploy/helm/leno/values.yaml`（§294-306）、`values-staging.yaml`、`values-prod.yaml` |
| 迁移：幂等 SQL（`deploy/helm/leno/files/migrations/*.sql`）由 migration Job 用 sqlcmd 执行 | `deploy/helm/leno/templates/migration-job.yaml` |
| 服务间鉴权：InternalApiKey 中间件，共享密钥 `Security__InternalApiKey__Shared` / `InternalAuth__ApiKey` | `Leno.Infrastructure.Auth/Middleware/InternalApiKeyMiddleware.cs`、`kv-seed.json` |

**关键推论（选型的约束）**：

1. **Consul 是全系统最高优先级组件**：服务启动时要读 KV（读失败可能阻塞启动），运行中服务注册/发现、灰度 Watch 都依赖它。必须 3 节点 server + 持久化 + ACL。
2. **Redis 丢失的影响面大**（登录态失效、限流重置、幂等去重失效、缓存雪崩），但不承载强一致状态 → HA 目标定为"秒级故障转移"（Sentinel 级即可，不必 Cluster）。
3. **ES 是业务读模型，不可降级为"可无"**，但其数据可由事件/主库重放重建 → RPO 要求宽松（分钟级~小时级），可接受较小规格。
4. **SQL Server 生产必须付费版本**：开发栈的 2019 Express 有单库 10GB 硬限制且不允许生产使用，迁移脚本已在 Helm files 中备好，生产选型需按 19 库容量规划。
5. **MassTransit 队列类型切换是应用侧一行配置**（端点配置声明 quorum），不改也能跑；本方案按"消息不丢"目标选型。

---

## 1. 各组件生产形态选型

### 1.1 SQL Server（最重的组件）

**现状**：19 个独立库（`ConnectionStrings__{Bc}Db`），每 BC 一个库，migration Job 按 BC 执行 sqlcmd。开发栈单实例 Express。

#### 选项对比

| 维度 | A. 云托管（Azure SQL / 阿里云 RDS SQL Server） | B. K8s 自建 StatefulSet | C. 云主机/VM 自建 Always On AG |
|------|------|------|------|
| 高可用 | 内置（RDS 主备/多可用区，Azure SQL SLA 99.99%） | 极难：SQL Server AG 在 K8s 上无官方成熟 Operator，PVC 依赖存储稳定性 | 成熟：3 节点 AG + 域控/证书，DBA 标准玩法 |
| 备份 | 平台自动 + 时间点恢复 | 自己搭 Ola Hallengren 脚本 + 对象存储，需自证可靠性 | 同 A 但需自己建设 |
| 19 库成本 | 按实例计费，一实例多库即可承载（RDS 单实例最多 100 库），中规格月费可控 | 需为 K8s 节点付高配资源 + 存储卷 | 3 台中高配云主机常驻 |
| 版本合规 | 平台侧正版授权 | **需自购 Server/Enterprise License，Express 不可用于生产** | 同左 |
| 运维门槛 | 最低，DBA 外包给云 | 最高（AG + K8s + 存储三重复杂度） | 高，需 DBA 在编 |
| 与 K8s 配合 | 通过 PrivateLink/VPC 内网直连，连接串进 Consul KV 即可 | 同 Pod 网络 | 同 A |
| 风险 | 云厂商锁定（连接串协议兼容 TDS，代码零改动） | 存储抖动直接拖垮交易库 | VM 宕机切换窗口比云托管长 |

**推荐：A（云托管 RDS / Azure SQL）**。理由：
1. 19 个库本质是 **1 个逻辑实例下的多库拆分**（库拆分已完成，RDS 单实例可全部承载），托管化成本优势明显；
2. Leno 团队无专职 DBA，AG/备份/恢复演练自建不现实；
3. TDS 协议不变，`ConnectionStrings__{Bc}Db` 只改 host，代码零改动，Consul KV 换值即完成切换。

**库拆分/合并建议（实例归组，不动库边界）**：保持 Database-per-BC 不变（微服务边界 + migration Job 依赖它），生产按流量把 19 库归入 2 个 RDS 实例：
- **实例 1（交易核心，高规格）**：Order、Payment、Inventory、Cart、Promotion、Product
- **实例 2（用户与支撑，中规格）**：Identity、UserAuth、AccessControl、Membership、Points、PointsMembership、Review、ReviewAfterSales、Notification、SellerShop、SystemAdmin、UserCenter

迁移 Job 的连接串本就来自 Consul KV，按实例改 host 即可，无需改 Helm 模板。Staging 可两实例合一。

> 备选：若预算无法上云托管，退而求其次选 **C（云主机 AG）**；**明确不建议 B（K8s 自建生产 SQL Server）**——SQL Server AG on K8s 社区方案不成熟，生产事故代价远超节省的托管费。

### 1.2 Redis

**用途已确认**（见 §0）：缓存、布隆过滤器（Bitmap）、幂等去重、JWT 黑名单、会话、限流 Lua、Pub/Sub 缓存失效通知。**无分布式锁、无 RedisBloom/JSON 模块依赖、无持久化业务数据**（幂等与会话可容忍重建），数据量以缓存为主。

#### 选项对比

| 维度 | A. 云托管 Redis（阿里云 Tair/RDS Redis、Azure Cache） | B. K8s 内 Redis Sentinel（1 主 2 从 3 哨兵） | C. K8s 内 Redis Cluster |
|---|---|---|---|
| 故障转移 | 秒级，平台托管 | 哨兵仲裁，10-30 秒级（StackExchange.Redis 自动重定向） | 秒级但拓扑复杂 |
| 承载数据特征 | 通用 | 单机内存上限，当前缓存规模（布隆过滤器 10M 元素约 12MB 位图 + 缓存）远未到瓶颈 | 需按 slot 分片，**Pub/Sub 与 Lua 多 key 脚本在 Cluster 下受限**（现有限流 Lua、批量 SETBIT 脚本需 key 同 slot 改造） |
| Pub/Sub（缓存失效通知） | 支持 | 支持 | 支持，但跨 slot 广播模式有坑 |
| 客户端兼容 | StackExchange.Redis 直连 | StackExchange.Redis 原生支持 Sentinel 配置串 | 需 cluster 模式连接串 |
| 成本（staging/prod） | 高/中 | 极低（3 个小 Pod）/低 | 6+ Pod 起步 |

**推荐：B（K8s 内 Redis Sentinel）**。理由：
1. Redis 在 Leno 中是**纯易失性辅助层**（会话/黑名单可重登，幂等可重建，缓存可回源），Sentinel 级 10-30 秒转移完全够用，不值得为它付托管费；
2. 现有 Lua 脚本与 Bitmap 用法**不兼容 Cluster**，选 C 反而引入代码改造（违背本次"不改源码"前提）；
3. Bitnami Redis chart（`architecture=replication` + `sentinel.enabled=true`）一行 values 拉起，连接串形如 `serviceName=redis-master,sentinel://...` 由 StackExchange.Redis 原生解析。

> 若后续布隆过滤器规模上到亿级或引入强一致锁，再评估云托管。**HA 等级：Sentinel 级（P1），不需要 Cluster（P2）。**

### 1.3 RabbitMQ

**拓扑已确认**：MassTransit 8.3.6，Topic 交换机 + durable 队列（默认 classic）。消息是集成事件（订单/库存/积分联动等），**不允许丢失**。

#### 选项对比

| 维度 | A. 单节点 + 持久化（durable queue + persistent message + 镜像盘） | B. Classic 镜像队列 3 节点 | C. Quorum 队列 3 节点集群 |
|---|---|---|---|
| 消息可靠性 | 节点宕机即不可用，RTO = 重启时长；磁盘持久化但单点 | 队列多副本，节点故障消息不丢 | Raft 多数派，**官方当前主推**，网络分区下 C HQ 行为 |
| 官方态度 | 仅适合开发 | **3.13 起弃用，4.x 已移除** | RabbitMQ 3.8+ 稳定支持，未来方向 |
| 与 MassTransit 8.3.6 兼容 | 完全 | 完全（3.13 版本下） | 完全（需在端点配置声明队列类型，见下） |
| 资源开销 | 最低 | 中（副本全量同步） | 中 |

**推荐：C（3 节点集群 + Quorum 队列，RabbitMQ 3.13）**。理由：
1. 集成事件驱动跨 BC 数据联动（如订单→积分→通知），消息丢失=数据不一致，单节点不可接受；
2. 镜像队列已进入弃用通道，新集群一步到位选 quorum，避免二次迁移；
3. MassTransit 8.x 支持 per-endpoint 声明 quorum（`AddConfigureEndpointsCallback` 中 `SetQueueArgument("x-queue-type", "quorum")`，一行配置级别的小改动，可列入后续优化项，不阻塞本方案——过渡期 classic durable 队列在 3.13 完全可用）；
4. 版本选 3.13（镜像队列仍可用作回退，4.x 移除镜像后无退路）。

> Staging 可先用单节点 + `persistence`（PVC），prod 必须 3 节点。

### 1.4 Consul（最高优先级组件）

**双重职责已确认**：① 生产配置主通道（KV + Watch 灰度）；② 网关服务发现 + 服务自注册。任一失效影响所有服务的启动与运行时配置。

#### 选项对比

| 维度 | A. HashiCorp 官方 `consul-k8s` Helm Chart：server 3 节点 + 持久化 + ACL | B. K8s 内自写 StatefulSet 部署 Consul | C. 云托管/HashiCorp Cloud Platform (HCP) |
|---|---|---|---|
| server 高可用 | 3 节点 Raft，官方 chart 一键拉起 + `server.storage` PVC 持久化 | 需自己处理 Raft/PVC/anti-affinity/升级 | 平台托管 |
| ACL | chart 内置 `global.acls.manageSystemACLs=true`，自动签发 agent token | 全手工 | 支持 |
| Agent 注册通道 | DaemonSet 客户端 agent，服务经本机 8500 自注册（与现有 `AgentServiceRegistration` 代码路径完全一致） | 同左但需自建 DaemonSet | 需自管 agent |
| 升级/运维 | 官方支持 | 全自担 | 最低 |
| 成本 | 3 server Pod（各 0.5C1G 级）+ 3 client agent | 同左 | 订阅费 |

**推荐：A（HashiCorp 官方 Helm Chart）**。理由：
1. Leno 的注册/发现代码走 Consul Agent HTTP API，K8s 部署唯一正确形态是 **每节点 DaemonSet client agent**，官方 chart 原生提供，自写 StatefulSet 无法覆盖这一点；
2. ACL 自动化管理（agent token 分发）是自建方案的深水区，chart 一行 values 解决；
3. KV 备份用 `consul snapshot save` 定时任务即可闭环（见 §4）。

**ACL 策略设计（token 最小化分离）**：

| Token | 策略内容 | 使用方 |
|---|---|---|
| `bootstrap` 管理员 token | `management` | 仅运维保管（Vault/KMS），不落 KV |
| `agent` token | `node`/`service:prefix ""` write（注册/注销服务），`agent` read | 每 client agent（chart 自动管理） |
| `kv-write` token | `key_prefix "leno/" policy=write` | 运维 CI（seed-consul-kv.ps1）、ESO（若用 consul backend 读 secret） |
| `kv-read` token | `key_prefix "leno/" policy=read` + `key_prefix "leno/anticorruption/" read` | 所有业务服务（配置加载 + 灰度 Watch） |
| `service-read` token | `service:prefix "" read`、`node` read | 网关服务发现 |

注：`seed-consul-kv.ps1` 已支持 `-ConsulToken` 参数，ACL 开启后直接传入 `kv-write` token 即可，脚本无需改动。

### 1.5 Elasticsearch

**用途已确认**：CQRS 读模型存储（商品/评价等读侧索引，事件经 `ReadModelSyncConsumerBase` 异步投影），**不是**日志检索。如实结论：**不能降级替代为"无"**（读接口依赖 ES 搜索），但数据可重建。

#### 选项对比

| 维度 | A. 云托管 ES（阿里云 ES / Elastic Cloud） | B. K8s 自建（ECK Operator 或 Bitnami chart） | C. 降级：砍掉 ES，读模型回数据库 |
|---|---|---|---|
| 可靠性 | 平台托管 | 单节点（staging）/3 节点小集群（prod） | 不适用 |
| 与现有代码兼容 | Elastic.Clients.Elasticsearch 8.x 直连 | 同左 | **需改代码**（`EsReadModelRepository` 全套），违背不改码前提 |
| 成本 | 高（最低规格也不便宜） | 低（3 节点 2C4G 级可跑读模型） | - |
| 数据丢失影响 | 投影重建（重放事件/回源主库） | 同左 | - |

**推荐：B（K8s 自建，Bitnami chart）**。理由：
1. ES 承载的是**可重建的投影数据**，RPO 宽松（§4 给出按小时级），不值得托管费；
2. 读模型 QPS 低于主库，3 节点小集群 + 单副本分片足够；
3. 不选 C：改代码成本高、收益低，仅作为远期架构演进备选记录在案。
4. 日志检索**不使用**此 ES：日志走 Loki/云日志服务（与 Grafana 监控栈一致），避免业务 ES 与日志互相拖垮——如实说明：当前仓库未见日志入 ES 的代码。

---

## 2. 环境矩阵

| 组件 | dev（现状，docker-compose） | staging（K8s 最小可用） | prod（K8s 高可用） |
|---|---|---|---|
| SQL Server | 单实例 2019 Express | RDS 小规格 1 实例（19 库合入）或 K8s 单节点 mssql + 20Gi PVC | RDS 高可用版 × 2 实例（交易核心 4C16G 起 / 用户支撑 2C8G 起） |
| Redis | 单节点无密码 | Sentinel：1 主 1 从 + 3 哨兵，各 0.25C256Mi | Sentinel：1 主 2 从 + 3 哨兵，主 1C2Gi / 从同规格 |
| RabbitMQ | 单节点 3.12 | 单节点 + PVC（Bitnami `replicaCount=1`） | 3 节点集群 + quorum 队列，各 0.5C1Gi |
| Consul | `agent -dev` | server 3 节点 + ACL + 10Gi PVC（**staging 也必须 3 节点**：KV 是配置主通道，单点会拖垮整套 staging 验证） | server 3 节点 + ACL + 20Gi PVC + DaemonSet agent，anti-affinity 打散节点 |
| Elasticsearch | 单节点 | 单节点 + PVC（Bitnami `replicaCount=1`） | 3 节点 + 单副本分片，各 2C4Gi |
| 备份 | 无（可随时重建） | KV snapshot 日 1 次；RDS 自动日备；RabbitMQ 定义导出周 1 次；ES 快照周 1 次 | 见 §4 |

**容量三档预估**（以缓存数据量与 QPS 量级估算，上线后按监控修正）：

| 档位 | 适用 | 关键规格 |
|---|---|---|
| 小（staging） | 全组件最小可用 | 见上表 staging 列 |
| 中（prod 起步） | 日订单 < 50 万 | RDS 4C16G×2 实例、Redis 2Gi、RabbitMQ 3×1Gi、ES 3×4Gi |
| 大（prod 扩容） | 日订单 > 50 万 | RDS 8C32G、Redis 主从各 8Gi、ES 分片数与副本按索引增长、增加读实例 |

---

## 3. Staging 部署清单（可执行）

前置：K8s 集群就绪、`kubectl` 上下文指向 staging、`helm` v3、命名空间 `leno`。

```bash
# ---------- 0. 命名空间 ----------
kubectl create namespace leno

# ---------- 1. Consul（最高优先级，先于一切） ----------
helm repo add hashicorp https://helm.releases.hashicorp.com
helm repo update
helm install consul hashicorp/consul -n leno \
  --set global.name=leno-consul \
  --set server.replicas=3 \
  --set server.storage=10Gi \
  --set server.affinity=anti-affinity \
  --set global.acls.manageSystemACLs=true \
  --set ui.enabled=true \
  --set connectInject.enabled=false \
  --set client.enabled=true          # DaemonSet client agent，服务自注册走本机 agent
# 产出：bootstrap token 存于 Secret leno-consul-bootstrap-acl-token（运维保管）
# 另按 §1.4 建 kv-write/kv-read/service-read token，写入 K8s Secret 供 ESO 与 seed 脚本使用

# ---------- 2. Redis Sentinel ----------
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install redis bitnami/redis -n leno \
  --set architecture=replication \
  --set sentinel.enabled=true \
  --set auth.enabled=true --set auth.password="${LENO_REDIS_PASSWORD}" \
  --set master.resources.requests.cpu=100m \
  --set replica.replicaCount=1        # prod: 2
# 连接串（写入 Consul KV Redis__Configuration）：
#   serviceName=redis-master,sentinel:True,redis:{password}@redis-sentinel.leno.svc:26379

# ---------- 3. RabbitMQ ----------
helm install rabbitmq bitnami/rabbitmq -n leno \
  --set auth.username=leno --set auth.password="${LENO_RABBITMQ_PASSWORD}" \
  --set replicaCount=1 \              # prod: 3 + persistence 20Gi
  --set persistence.enabled=true --set persistence.size=10Gi

# ---------- 4. Elasticsearch ----------
helm install elasticsearch bitnami/elasticsearch -n leno \
  --set singleNode.enabled=true \     # prod: replicas=3, minimumMasterNodes=2
  --set master.persistence.size=30Gi \
  --set coordinating.replicas=0 --set data.replicas=0   # small 模式由 master 节点兼数据

# ---------- 5. SQL Server（staging 若不用 RDS，K8s 单节点过渡） ----------
kubectl apply -f - <<'EOF'
apiVersion: apps/v1
kind: StatefulSet
metadata: { name: sqlserver, namespace: leno }
spec:
  serviceName: sqlserver
  replicas: 1
  selector: { matchLabels: { app: sqlserver } }
  template:
    metadata: { labels: { app: sqlserver } }
    spec:
      containers:
      - name: mssql
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
        - { name: ACCEPT_EULA, value: "Y" }
        - { name: MSSQL_SA_PASSWORD, valueFrom: { secretKeyRef: { name: leno-sqlserver-sa, key: password } } }
        - { name: MSSQL_PID, value: "Developer" }   # staging 用 Developer 版；生产必须 RDS/付费授权
        ports: [{ containerPort: 1433 }]
        volumeMounts: [{ name: data, mountPath: /var/opt/mssql }]
  volumeClaimTemplates:
  - metadata: { name: data }
    spec: { accessModes: ["ReadWriteOnce"], resources: { requests: { storage: 50Gi } } }
EOF

# ---------- 6. External Secrets Operator ----------
helm install external-secrets external-secrets/external-secrets \
  -n external-secrets --create-namespace
# SecretStore（consul backend）要点：
#   provider: consul → address http://consul-server.leno.svc:8500
#   auth: tokenRef → Secret leno-consul-kv-read（§1.4 的 kv-read token）
#   ESO 从 Consul KV 同步 Secret 到业务命名空间（values.yaml externalSecrets.backend=consul 已预设）

# ---------- 7. 与现有脚本的衔接（顺序重要） ----------
# 7.1 用 create-secrets.ps1 兜底建立 chart 引用的 K8s Secret（在 ESO 通道稳定前保底，之后逐步由 ESO 接管）
#     连接串指向本次部署的组件：
#       LENO_DB_{BC}      = Server=sqlserver.leno.svc,1433;Database=Leno{Bc};User Id=leno_app;Password=...
#       LENO_REDIS_CONFIGURATION = （上步 sentinel 连接串）
#       LENO_RABBITMQ_HOST = rabbitmq.leno.svc
#       LENO_ES_URI       = http://elasticsearch.leno.svc:9200
#       LENO_CONSUL_URL   = http://consul-server.leno.svc:8500
pwsh deploy/scripts/create-secrets.ps1 -Namespace leno

# 7.2 用 seed-consul-kv.ps1 种子化 KV（传 kv-write token，敏感值走环境变量）
$env:LENO_DB_ORDER = "..."; $env:LENO_RABBITMQ_PASSWORD = "..."; # 等
pwsh deploy/scripts/seed-consul-kv.ps1 `
  -ConsulAddress "http://consul-server.leno.svc:8500" `
  -ConsulToken "<kv-write token>"

# ---------- 8. 业务部署（Helm chart） ----------
helm install leno deploy/helm/leno -n leno -f deploy/helm/leno/values-staging.yaml
# migration Job 在各 BC init 时执行 sqlcmd（migration-job.yaml），前置：DB 已在 RDS/mssql 实例创建
```

**顺序要点**：Consul → Redis/RabbitMQ/ES/SQL Server → ESO → `create-secrets.ps1`（K8s Secret 兜底）→ `seed-consul-kv.ps1`（KV 主通道）→ `helm install leno`。Consul 必须最先就绪，否则业务 Pod 启动时 KV 配置加载失败。

---

## 4. 备份与恢复

| 组件 | 工具 | 频率 | 保留期 | 恢复演练要求 |
|---|---|---|---|---|
| SQL Server | RDS 自动备份 + 手动全量；K8s 过渡态用 Ola Hallengren 脚本 + 对象存储 | 全量日 1 次 + 日志每 15 分钟 | 全量 30 天，日志 7 天 | 季度 1 次单库 PITR 恢复演练（staging 演练，核对 19 库清单完整性） |
| Consul KV | CronJob：`consul snapshot save`，快照上传对象存储；KV 敏感项另有 `create-secrets.ps1` 可重放 | 快照日 1 次（30 分钟粒度可提升） | 30 天 | 半年 1 次：全新集群 `consul snapshot restore` + seed 脚本重放，验证服务启动 |
| Redis | 依赖其易失定位：不强制备份；RDB 日 1 次作调试快照 | RDB 日 1 | 3 天 | 不演练；验证"清空 Redis 后系统可自愈"（重登、缓存回源、幂等重建） |
| RabbitMQ | 拓扑定义导出（`rabbitmqctl export_definitions`）入 Git；消息依赖持久化 + quorum 副本，不备份消息体 | 定义导出：每次拓扑变更 | Git 永久 | 半年 1 次：空集群按导出定义重建 + 业务自检 |
| Elasticsearch | 快照到对象存储（`_snapshot` S3/MinIO 仓库）；兜底：事件重放重建索引 | 快照日 1 | 14 天 | 半年 1 次重建演练（确认投影代码可从事件全量重建） |

### RPO / RTO 目标表

| 组件 | RPO 目标 | RTO 目标 | 说明 |
|---|---|---|---|
| SQL Server（prod） | ≤ 15 分钟（日志备份粒度） | ≤ 30 分钟 | 交易库是唯一强一致状态源 |
| Consul | ≤ 24 小时（snapshot 日备） | ≤ 15 分钟 | 丢失段可由 seed 脚本 + 变更记录补齐；server 3 节点下整集群丢失为极端场景 |
| Redis | 接受全量丢失（RPO = ∞） | ≤ 1 分钟（Sentinel 切换） | 全部数据可自愈/重建 |
| RabbitMQ | 0（quorum + durable + 持久化消息） | ≤ 15 分钟（3 副本下节点级故障 ≈ 0） | 消息不丢为硬目标 |
| Elasticsearch | ≤ 1 小时（快照）或事件重放 | ≤ 4 小时（重建/恢复） | 读模型，最宽松 |

---

## 5. 网络与安全

### 5.1 组件间访问矩阵（命名空间 `leno` 内，端口为主端口）

| 调用方 | 目标 | 端口 | 凭据 |
|---|---|---|---|
| ApiGateway | 各 BC API（ServiceUrls）/ Consul | HTTP 8080 / 8500 | InternalApiKey（下游）+ kv-read token |
| 各 BC 服务 | 归属实例的 SQL Server | 1433 | 库级账号（`leno_app`，最小表权限） |
| 各 BC 服务 + 网关 | Redis Sentinel | 26379 / 6379 | Redis 密码（sentinel 连接串） |
| 各 BC 服务 | RabbitMQ | 5672 | vhost `/` + leno 账号 |
| 各 BC 服务 | Elasticsearch | 9200 | ES 基本认证 |
| 各 BC 服务 | Consul（KV 读 + 自注册） | 8500 | kv-read token + agent token |
| ESO | Consul | 8500 | kv-read token |
| 运维 CI / 管理端 | Consul UI、RabbitMQ Management(15672)、Kibana（如加） | 8500 / 15672 | Ingress + 独立认证，不暴露公网 |

网络策略：默认 NetworkPolicy 全 deny，按上表白名单放行；基础设施组件（SQL Server/Redis/RabbitMQ/ES/Consul server）不建任何 Ingress，仅集群内可达。

### 5.2 ACL / 密码轮换节奏

| 凭据 | 位置 | 轮换节奏 |
|---|---|---|
| Consul bootstrap token | KMS/Vault，专人保管 | 不轮换，改用分权 token 降低风险 |
| Consul kv-read/kv-write/service-read token | K8s Secret（ESO 通道） | 半年 1 次（或人员变动时即时） |
| SQL Server 库账号 | Consul KV（连接串整体） | 半年 1 次；KV 改值 + 滚动重启即生效 |
| Redis 密码 | Consul KV `Redis__Configuration` | 半年 1 次 |
| RabbitMQ 账号 | Consul KV `RabbitMQ__*` | 半年 1 次 |
| JWT SecretKey / InternalApiKey 共享密钥 | Consul KV | JWT：按 token 生命周期双密钥过渡；InternalApiKey：季度 1 次，双 key 灰度 |
| ES 密码 | Consul KV `Elasticsearch__Uri` | 半年 1 次 |

轮换依赖 Consul KV 主通道：改 KV 值 → Watch/滚动重启生效，是全系统凭据轮换的统一路径，不依赖镜像重建。

### 5.3 与 InternalApiKey 体系的关系

- 服务间东西向调用（BC ↔ BC、网关 → BC）继续使用 **InternalApiKey 中间件**（`Security__InternalApiKey__Shared`），密钥本体存 Consul KV，由 seed 脚本下发、ESO 同步；
- 本方案新增的组件凭据（DB/Redis/MQ/ES/Consul token）属**南北向基础设施凭据**，走同一 KV 通道但 token 权限不同，与 InternalApiKey 互相独立；
- 轮换时 InternalApiKey 采用"新 key 先加白、旧 key 观察期后下线"的双 key 灰度，避免滚动窗口内 401 风暴。

---

## 6. 待用户决策项

| # | 决策项 | 选项 | 默认建议 |
|---|---|---|---|
| D1 | SQL Server 生产形态 | 云托管 RDS / 云主机 AG / K8s 自建 | **云托管**（§1.1）；staging 先用 K8s 单节点 mssql（Developer 版）验证链路 |
| D2 | 云厂商 | Azure / 阿里云 / 腾讯云 | 无强依赖（连接串协议兼容），建议按团队现有账号与合规要求定，**倾向阿里云/腾讯云 RDS**（国内访问延迟） |
| D3 | 预算档位 | 小/中/大（§2 容量三档） | **中档**起步：RDS 4C16G×2 + K8s 自建 Redis/RabbitMQ/ES，月成本显著低于全托管 |
| D4 | RabbitMQ 版本与队列类型 | 3.13 + 后续切 quorum / 直接 4.x | **3.13 起步**，应用侧 quorum 配置列入优化项（一行端点配置） |
| D5 | ESO secret backend | consul / vault | **consul**（values.yaml 现状）；若后续引入 Vault 再迁移，KV 兜底通道保留 |
| D6 | staging SQL Server | K8s mssql（Developer） / 小规格 RDS | **K8s 单节点**（省预算）；联调期结束再评估切小规格 RDS |
| D7 | 日志栈 | Loki / 云日志服务 / ES（业务 ES 兼用） | **Loki 或云日志**，明确不复用业务 ES（§1.5） |

---

## 附：本方案引用的现有部署物

| 路径 | 说明 |
|---|---|
| `deploy/consul/kv-seed.json` | KV 种子清单（19 库连接串 + Redis/MQ/ES/JWT/InternalApiKey + 灰度开关） |
| `deploy/scripts/seed-consul-kv.ps1` | KV 种子化脚本（已支持 `-ConsulToken`，ACL 兼容） |
| `deploy/scripts/create-secrets.ps1` | K8s Secret 兜底创建脚本 |
| `deploy/helm/leno/values.yaml` | `externalSecrets.backend: consul`、Consul 地址 Secret 引用 |
| `deploy/helm/leno/values-staging.yaml` / `values-prod.yaml` | 两副本/三副本 HPA 配置、`externalSecrets.enabled: true` |
| `deploy/helm/leno/templates/migration-job.yaml` | sqlcmd 迁移 Job（依赖连接串 Secret） |
| `deploy/docs/consul-kv-coverage-audit.md` | KV 键映射审计（连接串键名规则） |

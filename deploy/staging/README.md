# Leno staging 端到端部署 Runbook

> 依据：`deploy/docs/production-infrastructure-plan.md` §3（v1.2 定稿）与 §6 已确认决策 D1-D7（2026-09-07，全部按默认建议生效）。
> 范围：staging（K8s 最小可用）首轮部署演练。**不修改任何 src/ 源码与 helm chart 模板**；发现的差距统一记在文末"遗留事项"。
> 安全约定：任何步骤不得写入真实密码/密钥到仓库；敏感值一律通过环境变量注入（`${ENV_VAR}`）。

---

## 0. 本 Runbook 的物料与脚本总览

| 物料 | 路径 | 说明 |
|---|---|---|
| Consul values | `deploy/helm/consul-staging-values.yaml` | 本仓库，步骤 1 |
| SQL Server | `deploy/staging/mssql-staging.yaml` | 本仓库，步骤 2 |
| Loki 栈 values | `deploy/staging/loki-staging-values.yaml` | 本仓库，步骤 6 |
| KV 种子清单 | `deploy/consul/kv-seed.json` | 本仓库，步骤 4 |
| Secret 创建脚本 | `deploy/scripts/create-secrets.ps1` | 本仓库，步骤 4（**过渡态 Secret 权威通道**） |
| KV 种子化脚本 | `deploy/scripts/seed-consul-kv.ps1` | 本仓库，步骤 4 |
| Leno helm chart | `deploy/helm/leno/` | 本仓库，步骤 5 |
| CD 流水线 | `.github/workflows/cd.yml` | 本仓库，步骤 7 |

**已脚本化 vs 需目标机器手工执行**：

| 动作 | 支撑方式 |
|---|---|
| Secret 五件套创建 | `create-secrets.ps1`（幂等，`--dry-run=client` + `apply`） |
| Consul KV 种子化 | `seed-consul-kv.ps1`（`${ENV_VAR}` 占位符解析，敏感值不落盘） |
| 迁移 SQL | helm chart 内置 pre-install/pre-upgrade Job（`sqlcmd` 执行幂等 SQL） |
| 集群/节点准备、helm install 基础设施、建库建账号、ACL token 创建、`leno-security-jwt`/`leno-staging-mssql`/`leno-staging-grafana-admin` 三个手工 Secret | **目标机器手工执行**（本 Runbook 给出命令） |

## 0.1 过渡态 Secret 来源（D5，如实说明）

- `values-staging.yaml` 中 `externalSecrets.enabled: true`。经核对 chart 模板，该开关**当前的实际行为**只是：`templates/secret.yaml` 不渲染 `leno-security-jwt` 占位 Secret；chart **没有**任何渲染 `ExternalSecret` CRD 的模板，`externalSecrets.backend: consul` 在 ESO 官方 provider 中也不存在（方案 §3 步骤 6 已说明）。
- 因此 staging 首轮（过渡态 c）：**全部 Secret 由 `create-secrets.ps1` + 三个手工 Secret 提供**，ESO/Vault 仅安装不接管（步骤 3 可跳过）。联调稳定后切换目标态 a（ESO + Vault），届时敏感项迁入 Vault。
- 注意：`deployment.yaml` 无条件引用 `leno-security-jwt`，过渡态必须按步骤 4.2 手工创建，否则 Pod 起不来。

## 0.2 前置条件

- [ ] K8s 集群就绪：3 台 worker（建议 4C8G/100G SSD，方案 §2"小"档），`kubectl` 上下文指向 staging 集群；
- [ ] `helm` v3.16+、`kubectl`（版本与集群大版本一致）已装在运维机；
- [ ] 工具镜像可达（国内环境请自配镜像加速或预先导入）：`mcr.microsoft.com/mssql/server:2022-latest`、`mcr.microsoft.com/mssql-tools18:latest`、bitnami/hashicorp/grafana chart 相关镜像；
- [ ] 集群有可用 StorageClass 或已手工预建 PV（mssql 50Gi + consul 10Gi×3 + ES 30Gi + rabbitmq 10Gi + loki 10Gi）；
- [ ] 本仓库已检出，工作目录 = 仓库根（下文所有相对路径以此为基准）。

## 步骤 0：命名空间与环境变量

```bash
kubectl create namespace leno
kubectl config set-context --current --namespace=leno
```

**LENO_* 环境变量清单**（三方已核对一致：`.env.example` ↔ `create-secrets.ps1` BcList ↔ `kv-seed.json`，均为 19 个 BC 键）：

| 变量 | 用途 | 必填 |
|---|---|---|
| `MSSQL_SA_PASSWORD` | mssql SA 密码（`leno-staging-mssql` Secret） | ✅ |
| `JWT_SECRET_KEY` | JWT 密钥（`leno-security-jwt` Secret + KV `Jwt__SecretKey`），≥64 字节 | ✅ |
| `INTERNAL_AUTH_API_KEY` | 内部鉴权（`leno-security-jwt` Secret + KV `InternalAuth__ApiKey`），≥32 字节 | ✅ |
| `LENO_INTERNAL_API_KEY_SHARED` | 服务间鉴权 Shared（KV `Security__InternalApiKey__Shared`），≥32 字节 | ✅ |
| `LENO_DB_{19 个 BC 大写名}` | 连接串（Secret `leno-db-connectionstrings` + KV `ConnectionStrings__*`） | ✅ |
| `LENO_REDIS_CONFIGURATION` | Redis Sentinel 连接串（步骤 2 安装 Redis 后回填） | ✅ |
| `LENO_RABBITMQ_HOST/PORT/USERNAME/PASSWORD/VIRTUALHOST` | MQ（`leno-mq-rabbitmq` Secret + KV `RabbitMQ__*`） | PASSWORD 必填 |
| `LENO_ES_URI` | ES 地址（`leno-es-connection` Secret + KV `Elasticsearch__Uri`） | ✅ |
| `LENO_CONSUL_URL` | Consul 地址（`leno-consul-address` Secret） | ✅ |
| `LENO_CONSUL_TOKEN` | ACL kv-write token（步骤 1 产出，seed 脚本使用） | ✅ |
| `LENO_SERVICEURL_{7 个 API}` | 服务发现兜底（KV 有 default，**staging 需覆盖**，见步骤 4.3 注） | 按步骤 4.3 |
| `GF_SECURITY_ADMIN_USER/PASSWORD` | Grafana 管理员（`leno-staging-grafana-admin` Secret） | ✅ |

建议在运维机 `cp .env.example .env` 填值后逐条 `export`（或用 dotenv 方式加载；`.env` 已被 gitignore，严禁提交）。

## 步骤 1：Consul（最高优先级，先于一切）

```bash
helm repo add hashicorp https://helm.releases.hashicorp.com
helm repo update
helm install consul hashicorp/consul -n leno -f deploy/helm/consul-staging-values.yaml
```

**验证**：

```bash
# 1) 3 个 server Pod Running（anti-affinity 要求节点 ≥ 3，Pending 说明节点不够）
kubectl -n leno get pods -l "app=consul,component=server"
# 2) 成员齐全（3 server + 每 node 一个 client agent）
kubectl -n leno exec consul-server-0 -- consul members
# 3) PVC 已绑定
kubectl -n leno get pvc
```

**ACL token 产出**（chart 已自动 bootstrap，Secret 名格式 `<release>-consul-bootstrap-acl-token`）：

```bash
# bootstrap 管理员 token —— 运维妥善保管，不落 KV、不进 Git
kubectl -n leno get secret consul-consul-bootstrap-acl-token -o jsonpath='{.data.token}' | base64 -d
export BT=$(kubectl -n leno get secret consul-consul-bootstrap-acl-token -o jsonpath='{.data.token}' | base64 -d)

# kv-write token（seed-consul-kv.ps1 用；策略 = 方案 §1.4 分权表）
kubectl -n leno exec -i consul-server-0 -- sh -c \
  "CONSUL_HTTP_TOKEN=$BT consul acl policy create -name leno-kv-write -rules -" <<'RULES'
key_prefix "leno/" { policy = "write" }
RULES
export LENO_CONSUL_TOKEN=$(kubectl -n leno exec consul-server-0 -- sh -c \
  "CONSUL_HTTP_TOKEN=$BT consul acl token create -policy-name leno-kv-write -format json" \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['SecretID'])")
kubectl -n leno create secret generic leno-consul-kv-write-token \
  --from-literal=token="$LENO_CONSUL_TOKEN" --dry-run=client -o yaml | kubectl apply -f -

# kv-read / service-read token 同理（策略规则见方案 §1.4 表格；目标态经 ESO+Vault 分发）
# kv-read:  key_prefix "leno/" { policy = "read" } key_prefix "leno/anticorruption/" { policy = "read" }
# service-read: service_prefix "" { policy = "read" } node_prefix "" { policy = "read" }
```

## 步骤 2：SQL Server（D6：K8s 单节点 Developer 版）

```bash
kubectl -n leno create secret generic leno-staging-mssql \
  --from-literal=password="${MSSQL_SA_PASSWORD}" --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -f deploy/staging/mssql-staging.yaml
```

> 若集群无动态供给，先按 `mssql-staging.yaml` 中 storageClassName 注释预建 PV。

**建库 + 建业务账号**（19 库；库名 = `Leno{Bc}`，与连接串一致；migration Job 与业务服务共用 `leno_app` 连接串）：

```bash
DBS="LenoAccessControl LenoAfterSales LenoCart LenoIdentity LenoInventory LenoMembership \
LenoNotification LenoOrder LenoPayment LenoPoints LenoPointsMembership LenoProduct \
LenoPromotion LenoReview LenoReviewAfterSales LenoSellerShop LenoSystemAdmin \
LenoUserAuth LenoUserCenter"
kubectl -n leno exec -i sqlserver-0 -- /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -No \
  -Q "CREATE LOGIN [leno_app] WITH PASSWORD = '${MSSQL_SA_PASSWORD}', CHECK_POLICY = ON"
for db in $DBS; do
  kubectl -n leno exec -i sqlserver-0 -- /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -No \
    -Q "CREATE DATABASE [${db}]; USE [${db}]; CREATE USER [leno_app] FOR LOGIN [leno_app]; ALTER ROLE db_owner ADD MEMBER [leno_app];"
done
```

> staging 为省事给 `leno_app` 授了 `db_owner`（migration Job 需要 DDL 权限）；prod 按方案 §5.1 收敛为最小表权限。

**验证**：Pod ready（readiness 即 `sqlcmd SELECT 1`）→ `kubectl -n leno get pods -l app=sqlserver`；19 库存在 → 上一步 sqlcmd `SELECT name FROM sys.databases` 核对。

**Redis / RabbitMQ / Elasticsearch**：安装命令**直接引用方案 §3 步骤 2/3/4**（不重复抄写），需注意：
- Redis 安装后，把 Sentinel 连接串回填 `LENO_REDIS_CONFIGURATION`：`redis-sentinel.leno.svc:26379,serviceName=mymaster,password=${LENO_REDIS_PASSWORD}`（masterSet 自定义时按方案 §3 步骤 2 的注意项对齐）；
- ES 安装后 `LENO_ES_URI=http://elasticsearch.leno.svc:9200`；RabbitMQ `LENO_RABBITMQ_HOST=rabbitmq.leno.svc`。

## 步骤 3：ESO + Vault（目标态预备，**过渡态可先跳过**）

按方案 §3 步骤 6 安装（Vault + external-secrets chart）。过渡态（D5-c）下无需创建 ClusterSecretStore；跳过本步不影响后续步骤。联调稳定切换目标态时再回补，敏感项迁入 Vault 后 `values.yaml` 的 `externalSecrets.backend` 需改为 `vault`。

## 步骤 4：过渡态 Secret + Consul KV 种子化

### 4.1 五件套 Secret（脚本支撑，过渡态权威通道）

```powershell
pwsh deploy/scripts/create-secrets.ps1 -Namespace leno
```

**验证**：`kubectl -n leno get secret leno-db-connectionstrings leno-mq-rabbitmq leno-redis-connection leno-es-connection leno-consul-address` 全部存在，抽查 `kubectl -n leno get secret leno-db-connectionstrings -o jsonpath='{.data.OrderDb}' | base64 -d`。

### 4.2 三个手工 Secret（脚本未覆盖，按 0.1 节说明必须手工建）

```bash
# leno-security-jwt：deployment.yaml 无条件引用（externalSecrets.enabled=true 时 chart 不渲染它）
kubectl -n leno create secret generic leno-security-jwt \
  --from-literal=secret-key="${JWT_SECRET_KEY}" \
  --from-literal=internal-api-key="${INTERNAL_AUTH_API_KEY}" \
  --dry-run=client -o yaml | kubectl apply -f -

# leno-staging-mssql：若步骤 2 已建则跳过
# leno-staging-grafana-admin：步骤 6 Loki 栈的 Grafana 凭据
kubectl -n leno create secret generic leno-staging-grafana-admin \
  --from-literal=admin-user="${GF_SECURITY_ADMIN_USER}" \
  --from-literal=admin-password="${GF_SECURITY_ADMIN_PASSWORD}" \
  --dry-run=client -o yaml | kubectl apply -f -
```

### 4.3 Consul KV 种子化（脚本支撑；KV 是生产配置主通道）

Consul 服务为 ClusterIP（不建 Ingress，方案 §5.1），运维机经 port-forward 访问：

```bash
kubectl -n leno port-forward svc/consul-server 8500:8500 &
# 干跑核对（不写库）
pwsh deploy/scripts/seed-consul-kv.ps1 -DryRun \
  -ConsulAddress "http://localhost:8500" -ConsulToken "${LENO_CONSUL_TOKEN}"
# 正式写入
pwsh deploy/scripts/seed-consul-kv.ps1 \
  -ConsulAddress "http://localhost:8500" -ConsulToken "${LENO_CONSUL_TOKEN}"
```

**验证**：`curl -H "X-Consul-Token: ${LENO_CONSUL_TOKEN}" http://localhost:8500/v1/kv/leno/config/ServiceUrls__ProductApi?raw`；灰度开关 `.../leno/anticorruption/use-grpc/order?raw` 应为 `false`。

> ⚠ **ServiceUrls 必须覆盖**：`kv-seed.json` 中 `ServiceUrls__*` 的 default 是 dev 的 compose 服务名（`http://leno-product-api` 等）；staging 的 release 名为 `leno`，实际服务 DNS 为 `http://leno-product:5152` 等（chart 渲染规则 `{release}-{service}:{port}`，见 `values.yaml` serviceUrls）。且 **KV 优先级高于 env 兜底**，写错即全错。写 KV 前先 `export`：
> `LENO_SERVICEURL_PRODUCTAPI=http://leno-product:5152`、`LENO_SERVICEURL_PROMOTIONAPI=http://leno-promotion:5155`、`LENO_SERVICEURL_POINTSMEMBERSHIPAPI=http://leno-pointsmembership:5157`、`LENO_SERVICEURL_ORDERAPI=http://leno-order:5154`、`LENO_SERVICEURL_PAYMENTAPI=http://leno-payment:5158`、`LENO_SERVICEURL_USERAUTHAPI=http://leno-user-auth:5151`、`LENO_SERVICEURL_ACCESSCONTROLAPI=http://leno-access-control-api`（accesscontrol 无独立部署，保留 KV default 或与网关路由核实）。

## 步骤 5：业务部署（Helm chart）

```bash
helm install leno deploy/helm/leno -n leno -f deploy/helm/leno/values-staging.yaml
```

> 如镜像在私有 GHCR：先 `kubectl -n leno create secret docker-registry` 并在 values `global.imagePullSecrets` 引用。

**验证**：

```bash
# 迁移 Job 全部 Completed（11 个 BC，名称 = leno-<bc>-migration，hook 成功后自删，升级前可看 -a）
kubectl -n leno get jobs
kubectl -n leno get pods                            # 全部 Running/Ready
kubectl -n leno get deployment leno-api-gateway -o wide
# 健康（services 探针路径 /health/ready、/health/live）
kubectl -n leno port-forward svc/leno-api-gateway 8080:8080 &
curl -s http://localhost:8080/health/ready && curl -s http://localhost:8080/health/live
# Consul 服务自注册核验
kubectl -n leno exec consul-server-0 -- consul catalog services
```

**排错速查**：Pod `CreateContainerConfigError` → 查 0.1/4.2（Secret 缺失）；`CrashLoopBackOff` 且日志 403 → Consul KV 读被 ACL 拒（见遗留事项 ③）；migration Job 失败 → 查连接串 Secret 与建库步骤。

## 步骤 6：Loki 日志栈（D7）

```bash
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
helm install loki grafana/loki-stack -n leno -f deploy/staging/loki-staging-values.yaml
```

**验证**：`kubectl -n leno get pods -l app.kubernetes.io/name=loki`（Loki/Promtail/Grafana 就绪）；`kubectl -n leno port-forward svc/loki-grafana 3000:80` → 浏览器登录（步骤 4.2 的 Grafana Secret 凭据）→ Explore 里选 Loki 数据源，能查到 Promtail 采集的容器日志。

## 步骤 7：CD 触发（`.github/workflows/cd.yml`）

- 手动 `workflow_dispatch`：输入 `environment=staging`、`image_tag`（CI 推送的 tag，如 `sha-abc1234` 或 `main`）；
- 镜像坐标：`ghcr.io/<owner小写>/leno-<service>:<tag>`，CD 自动 `--set global.imageRegistry/imageNamespace/imageTag`（namespace = 仓库 owner 小写）；
- 前置：仓库 Settings → Environments 建 `staging`，配置环境级 Secret `KUBECONFIG_STAGING`（base64 编码 kubeconfig，最小权限 SA）；
- CD 语义 = `helm upgrade --install --atomic` + rollout status 检查，失败自动回滚。

> ⚠ 见遗留事项 ⑤：cd.yml 目标 namespace 是 `leno-staging`，与本 Runbook/方案的 `leno` 不一致——首次用 CD 前**必须**先对齐（改 cd.yml 或手工把上述步骤迁入 `leno-staging`）。

## 回滚

- **业务（leno release）**：`helm rollback leno <revision> -n leno`（`helm history leno -n leno` 查版本）；日常发版直接依赖 `--atomic` 自动回滚。
- **迁移 SQL**：幂等脚本只增不删，业务回滚一般不需回滚库；确需回库结构时**手工执行反向 SQL**（禁止把旧 chart 重装当"回滚迁移"，hook 语义是 pre-upgrade 重放）。
- **Consul KV 变更回退**：KV 没有"版本"，靠两点兜底——① `consul snapshot save` 日备（方案 §4，恢复用 `consul snapshot restore`）；② 灰度开关类键直接改回旧值即可（`ConsulConfigWatcher` 阻塞查询实时感知）。⚠ 手工改 KV 走 `seed-consul-kv.ps1` 修改清单后重放，或用 kv-write token PUT，**不要**用 bootstrap token 日常写。
- **基础设施**：consul/mssql/loki 不建议 `helm rollback`（有状态组件），按组件各自恢复手段处理（SQL Server 还原备份、Consul snapshot restore）。

## 遗留事项（按重要性排序）

1. **`leno-security-jwt` 无过渡态自动化**：`create-secrets.ps1` 不创建它，而 `externalSecrets.enabled=true` 时 chart 也不渲染 → 步骤 4.2 手工建。建议后续给 `create-secrets.ps1` 增加 JWT/InternalAuth 两个键（复用 `JWT_SECRET_KEY`/`INTERNAL_AUTH_API_KEY`）。
2. **`externalSecrets.enabled: true` 名不符实**（D5 过渡态已知）：chart 无 ExternalSecret 模板、`backend: consul` 的 provider 不存在。切换目标态时需改 values（`backend: vault`）并补 ESO SecretStore/ExternalSecret 清单。
3. **业务服务未注入 Consul ACL token**：代码支持 `Consul:Token` 配置键（`ConfigCenterExtensions` / `ConsulServiceRegistrationExtensions` 均读取），但 `deployment.yaml` 无对应 env 注入 → ACL 开启后服务读 KV/自注册可能 403。二选一：给 chart 增加从 Secret 注入 `Consul__Token`；或 staging 首轮临时降级 ACL（去掉 `global.acls.manageSystemACLs`）。
4. **`leno_app` 数据库账号密码**当前直接复用 `MSSQL_SA_PASSWORD`（staging 从简）；prod 独立密码并按最小权限收敛。
5. **CD namespace 不一致**：`cd.yml` 用 `leno-staging`，本 Runbook/方案 §3 用 `leno`，首次 CD 演练前对齐。
6. **Loki chart 形态**：loki-stack 单副本适合 staging；prod 按方案 D7 重新评估（grafana/loki SSE 模式 + 独立 Grafana + 保留期专项设计）。
7. **helm lint 未执行**：运维机未安装 helm/未拉取 chart 仓库；三个 YAML 已通过 `yaml.safe_load_all` 语法校验，`helm lint` 留待目标机器执行（`helm lint deploy/helm/leno -f deploy/helm/leno/values-staging.yaml`）。

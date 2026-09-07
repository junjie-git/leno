<#
.SYNOPSIS
  一键创建 Leno 运行所需的五个 K8s Secret（P0-F）。

.DESCRIPTION
  创建/更新以下 Secret（kubectl create secret --dry-run=client | kubectl apply，幂等）：
    1. leno-db-connectionstrings  key={Bc}Db（与代码 GetConnectionString 键严格一致）
    2. leno-mq-rabbitmq           key=username/password（及可选 host/port）
    3. leno-redis-connection      key=configuration
    4. leno-es-connection         key=uri
    5. leno-consul-address        key=url

  敏感值一律从环境变量读取（与 deploy/consul/kv-seed.json 的 ENV_VAR 命名一致），
  脚本内无真实密码；必需环境变量缺失时报错退出，绝不写入占位符。

  使用前提：kubectl 已登录目标集群/命名空间。
  注意：deployment.yaml/migration-job.yaml 引用这些 Secret，需在部署前创建。

.PARAMETER Namespace
  目标命名空间，默认 default。

.PARAMETER BcList
  逗号分隔的 BC 名单（用于连接串 Secret 的 key 推导），默认覆盖 helm chart
  部署的 11 个 BC + 审计表中的全部 19 个 BC 连接串键。

.PARAMETER SkipDatabase
  跳过数据库连接串 Secret（如已由 DBA/平台侧创建）。

.EXAMPLE
  $env:LENO_DB_ORDER = "Server=sqlserver,1433;Database=LenoOrder;User Id=sa;Password=***;TrustServerCertificate=True"
  $env:LENO_REDIS_CONFIGURATION = "redis:6379"
  $env:LENO_RABBITMQ_PASSWORD = "***"
  $env:LENO_ES_URI = "http://elasticsearch:9200"
  $env:LENO_CONSUL_URL = "http://consul:8500"
  ./create-secrets.ps1 -Namespace leno
#>
[CmdletBinding()]
param(
    [string]$Namespace,
    [string]$BcList,
    [switch]$SkipDatabase
)

$ErrorActionPreference = "Stop"

if (-not $Namespace) { $Namespace = "default" }

# BC 名 → 连接串 Secret key（与代码 GetConnectionString("{Bc}Db") 一致，
# 见 deploy/docs/consul-kv-coverage-audit.md §3.1）
if (-not $BcList) {
    $BcList = "accesscontrol,aftersales,cart,identity,inventory,membership,notification,order,payment,points,pointsmembership,product,promotion,review,reviewaftersales,sellershop,systemadmin,userauth,usercenter"
}

# env 变量名 = LENO_DB_{大写BC名}（与 kv-seed.json 一致）
$bcKeys = @{}
foreach ($bc in ($BcList -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
    $envName = "LENO_DB_" + $bc.ToUpper()
    $bcKeys[$envName] = $bc + "Db"     # accesscontrol -> AccessControlDb
}

function Get-RequiredEnv {
    param([string]$Name)
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) {
        throw "必需的环境变量 $Name 未设置（脚本不写入占位符，请先注入真实值）"
    }
    return $value
}

function New-Secret {
    param([string]$Name, [hashtable]$KeyValues)

    Write-Host "==> 创建/更新 Secret：$Name（命名空间 $Namespace）"
    $args = @("create", "secret", "generic", $Name, "-n", $Namespace, "--dry-run=client", "-o", "yaml")
    foreach ($kv in $KeyValues.GetEnumerator()) {
        Write-Host "    key: $($kv.Key)"
        $args += @("--from-literal", "$($kv.Key)=$($kv.Value)")
    }
    # 客户端干跑生成 manifest 后 apply，保证幂等
    $manifest = & kubectl @args
    if ($LASTEXITCODE -ne 0) { throw "kubectl create secret dry-run 失败：$Name" }
    $manifest | & kubectl apply -f -
    if ($LASTEXITCODE -ne 0) { throw "kubectl apply 失败：$Name" }
    Write-Host "[OK] $Name"
}

Write-Host "Leno Secret 创建脚本（命名空间：$Namespace）"
Write-Host ""

# ---------------------------------------------------------------------------
# 1. 数据库连接串
# ---------------------------------------------------------------------------
if (-not $SkipDatabase) {
    $dbKeys = @{}
    foreach ($envName in $bcKeys.Keys) {
        $dbKeys[$bcKeys[$envName]] = Get-RequiredEnv $envName
    }
    New-Secret -Name "leno-db-connectionstrings" -KeyValues $dbKeys
}
else {
    Write-Host "==> 跳过 leno-db-connectionstrings（-SkipDatabase）"
}

# ---------------------------------------------------------------------------
# 2. RabbitMQ
# ---------------------------------------------------------------------------
$mqKeys = @{
    "username" = if ($env:LENO_RABBITMQ_USERNAME) { $env:LENO_RABBITMQ_USERNAME } else { "leno" }
    "password" = Get-RequiredEnv "LENO_RABBITMQ_PASSWORD"
    "host"     = if ($env:LENO_RABBITMQ_HOST) { $env:LENO_RABBITMQ_HOST } else { "rabbitmq" }
    "port"     = if ($env:LENO_RABBITMQ_PORT) { $env:LENO_RABBITMQ_PORT } else { "5672" }
    "vhost"    = if ($env:LENO_RABBITMQ_VIRTUALHOST) { $env:LENO_RABBITMQ_VIRTUALHOST } else { "/" }
}
New-Secret -Name "leno-mq-rabbitmq" -KeyValues $mqKeys

# ---------------------------------------------------------------------------
# 3. Redis
# ---------------------------------------------------------------------------
New-Secret -Name "leno-redis-connection" -KeyValues @{
    "configuration" = Get-RequiredEnv "LENO_REDIS_CONFIGURATION"
}

# ---------------------------------------------------------------------------
# 4. Elasticsearch
# ---------------------------------------------------------------------------
New-Secret -Name "leno-es-connection" -KeyValues @{
    "uri" = Get-RequiredEnv "LENO_ES_URI"
}

# ---------------------------------------------------------------------------
# 5. Consul 地址（引导配置，KV 配置源依赖它）
# ---------------------------------------------------------------------------
New-Secret -Name "leno-consul-address" -KeyValues @{
    "url" = Get-RequiredEnv "LENO_CONSUL_URL"
}

Write-Host ""
Write-Host "全部 Secret 创建/更新完成。"
Write-Host "提示：leno-security-jwt 由 helm chart（externalSecrets.enabled=false 时按 values 渲染）或 ESO 创建。"

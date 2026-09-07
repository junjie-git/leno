<#
.SYNOPSIS
  将 deploy/consul/kv-seed.json 清单中的配置键批量写入 Consul KV（P0-D）。

.DESCRIPTION
  生产配置主通道为 Consul KV：所有 BC 的 Program.cs 调用 AddLenoConsulConfig()
  （Winton Consul 源位于配置链尾，KV 优先级高于 env 兜底与 appsettings.json）。
  本脚本读取 deploy/consul/kv-seed.json（键名 = 配置键，':' → '__'，与代码读取键
  严格一致，见 deploy/docs/consul-kv-coverage-audit.md），解析 value 中的
  ${ENV_VAR} 占位符后通过 Consul HTTP API（PUT /v1/kv/{key}）写入。

  安全约束：
  - 清单与脚本中禁止出现真实密码，敏感值一律使用 ${ENV_VAR} 占位符引用环境变量；
  - 环境变量缺失时默认报错拒绝写入（不写入未解析的占位符文本）；
  - 非 sensitive 项可声明 default 作为环境变量缺失时的兜底默认值。

.PARAMETER ConsulAddress
  Consul HTTP 地址。默认取环境变量 LENO_CONSUL_URL，否则 http://localhost:8500。

.PARAMETER ConsulToken
  Consul ACL Token（可选，启用 ACL 的集群必填）。

.PARAMETER ManifestPath
  KV 种子清单路径，默认 deploy/consul/kv-seed.json（相对仓库根）。

.PARAMETER DryRun
  只打印将执行的写入动作（含值是否解析成功），不实际请求 Consul。

.EXAMPLE
  # 干跑：核对将要写入的键值
  ./seed-consul-kv.ps1 -DryRun

.EXAMPLE
  # 正式写入（敏感值先注入环境变量）
  $env:LENO_DB_ORDER = "Server=sqlserver,1433;Database=LenoOrder;..."
  $env:LENO_RABBITMQ_PASSWORD = "..."
  ./seed-consul-kv.ps1 -ConsulAddress "http://consul:8500"
#>
[CmdletBinding()]
param(
    [string]$ConsulAddress,
    [string]$ConsulToken,
    [string]$ManifestPath,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. 定位仓库根与清单文件
# ---------------------------------------------------------------------------
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)  # deploy/scripts/ -> deploy/ -> repo root
if (-not $ManifestPath) {
    $ManifestPath = Join-Path $repoRoot "deploy/consul/kv-seed.json"
}
if (-not (Test-Path $ManifestPath)) {
    throw "KV 种子清单不存在：$ManifestPath"
}

# ---------------------------------------------------------------------------
# 2. 解析 Consul 地址与 Token
# ---------------------------------------------------------------------------
if (-not $ConsulAddress) {
    $ConsulAddress = if ($env:LENO_CONSUL_URL) { $env:LENO_CONSUL_URL } else { "http://localhost:8500" }
}
$ConsulAddress = $ConsulAddress.TrimEnd('/')

$headers = @{}
if ($ConsulToken) {
    $headers["X-Consul-Token"] = $ConsulToken
}
elseif ($env:LENO_CONSUL_TOKEN) {
    $headers["X-Consul-Token"] = $env:LENO_CONSUL_TOKEN
}

# ---------------------------------------------------------------------------
# 3. 占位符解析：${ENV_VAR} -> 环境变量值
# ---------------------------------------------------------------------------
function Resolve-Placeholder {
    param([string]$Text, [string]$KeyName, [object]$ItemDefault)

    if (-not $Text) { return $null }

    # 手工扫描 ${ENV_VAR}（避免依赖 MatchEvaluator 委托，兼容 Windows PowerShell 5.1）
    $sb = New-Object System.Text.StringBuilder
    $pos = 0
    foreach ($m in [regex]::Matches($Text, '\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}')) {
        [void]$sb.Append($Text.Substring($pos, $m.Index - $pos))
        $varName = $m.Groups["name"].Value
        $envValue = [Environment]::GetEnvironmentVariable($varName)
        if ([string]::IsNullOrEmpty($envValue)) {
            # 环境变量缺失：非敏感项允许 default 兜底；敏感项一律报错
            if ($null -ne $ItemDefault -and "$ItemDefault" -ne "") {
                [void]$sb.Append([string]$ItemDefault)
            }
            else {
                throw "键 $KeyName 引用的环境变量 $varName 未设置（敏感值禁止在清单中写死，请先注入环境变量）"
            }
        }
        else {
            [void]$sb.Append($envValue)
        }
        $pos = $m.Index + $m.Length
    }
    [void]$sb.Append($Text.Substring($pos))
    return $sb.ToString()
}

# ---------------------------------------------------------------------------
# 4. 写入函数（HTTP API：PUT /v1/kv/{key}）
# ---------------------------------------------------------------------------
function Set-ConsulKv {
    param([string]$Key, [string]$Value)

    $uri = "$ConsulAddress/v1/kv/$Key"
    if ($DryRun) {
        Write-Host "[DRY-RUN] PUT $uri"
        Write-Host "          value: $Value"
        return
    }

    try {
        $response = Invoke-RestMethod -Method Put -Uri $uri -Headers $headers -Body $Value -ContentType "text/plain; charset=utf-8"
        if ($response -ne $true) {
            throw "Consul 写入 $Key 未返回 true"
        }
        Write-Host "[OK] $Key"
    }
    catch {
        Write-Error "写入 Consul KV 失败：$Key（$uri）：$($_.Exception.Message)"
        throw
    }
}

# ---------------------------------------------------------------------------
# 5. 读取清单并执行
# ---------------------------------------------------------------------------
$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$prefix = $manifest.prefix
if (-not $prefix) { $prefix = "leno/config/" }

Write-Host "Consul 地址：$ConsulAddress"
Write-Host "清单文件：$ManifestPath"
Write-Host "KV 前缀：$prefix"
if ($DryRun) { Write-Host "=== DRY-RUN 模式：不实际写入 ===" }
Write-Host ""

$failCount = 0

# 5.1 业务配置（leno/config/**）
foreach ($item in $manifest.items) {
    $fullKey = $prefix + $item.key
    try {
        $value = Resolve-Placeholder -Text $item.value -KeyName $fullKey -ItemDefault $item.default
        if ([string]::IsNullOrEmpty($value)) {
            throw "解析后的值为空"
        }
        Set-ConsulKv -Key $fullKey -Value $value
    }
    catch {
        Write-Error "跳过 $fullKey：$($_.Exception.Message)"
        $failCount++
    }
}

# 5.2 灰度开关（leno/anticorruption/use-grpc/{bc}，不经 leno/config 前缀）
$ac = $manifest.antiCorruption
if ($ac -and $ac.keys) {
    foreach ($bc in $ac.keys) {
        $fullKey = $ac.prefix + $bc
        Set-ConsulKv -Key $fullKey -Value $ac.value
    }
}

Write-Host ""
if ($DryRun) {
    Write-Host "DRY-RUN 完成。去掉 -DryRun 后执行实际写入。"
}
elseif ($failCount -gt 0) {
    throw "有 $failCount 个键写入失败/被跳过，请检查环境变量后重试。"
}
else {
    Write-Host "全部 KV 写入完成。"
}

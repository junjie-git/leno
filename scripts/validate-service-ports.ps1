<#
.SYNOPSIS
  服务端口三方一致性校验（双轨下线 A5，2026-09-23）。

.DESCRIPTION
  以 deploy/services.manifest.json 为单一来源，校验：
    1. docker-compose.yml        —— <composeService> 的宿主端口 == manifest.httpPort
    2. deploy/helm/leno/values.yaml —— services.<name>.httpPort == manifest.httpPort
                                     services.<name>.service.port == manifest.httpPort
                                     services.<name>.grpcPort == manifest.grpcPort（如有）
    3. values.yaml serviceUrls.* 中已登记的端口与 manifest 一致（自动枚举全部键）
    4. deploy/consul/kv-seed.json —— ServiceUrls__* 主机名与 manifest composeService 一致
  任一项不一致即退出码 1（CI 失败）。

.NOTES
  改端口时先改 manifest，再同步两侧清单；本脚本在 CI job validate-service-ports 中执行。
#>
param(
  [string]$RepoRoot = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"
$fail = New-Object System.Collections.Generic.List[string]

$manifestPath = Join-Path $RepoRoot "deploy/services.manifest.json"
$composePath = Join-Path $RepoRoot "docker-compose.yml"
$valuesPath = Join-Path $RepoRoot "deploy/helm/leno/values.yaml"

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$compose = Get-Content $composePath -Raw -Encoding UTF8
$values = Get-Content $valuesPath -Raw -Encoding UTF8
$vLines = $values -split "`n"

$services = @($manifest.gateway) + @($manifest.services)

foreach ($svc in $services) {
  $name = $svc.name
  $composeName = $svc.composeService
  $httpPort = [int]$svc.httpPort

  # 1) compose：定位服务块起点，向下找 "ddd:8080"
  $idx = $compose.IndexOf("  ${composeName}:")
  if ($idx -lt 0) {
    $fail.Add("compose 缺服务块：$composeName")
  }
  else {
    $seg = $compose.Substring($idx, [Math]::Min(1200, $compose.Length - $idx))
    $m = [regex]::Match($seg, '"(\d+):8080"')
    if (-not $m.Success) {
      $fail.Add("compose $composeName 未找到端口映射")
    }
    elseif ([int]$m.Groups[1].Value -ne $httpPort) {
      $fail.Add("compose $composeName 端口 $($m.Groups[1].Value) != manifest $httpPort")
    }
  }

  # 网关不在 Helm 的 services 映射中（单独部署），跳过 Helm 侧校验
  if ($name -eq 'apigateway') { continue }

  # 2) Helm values：跟踪当前服务块，校验 httpPort / service.port / grpcPort
  $current = $null
  $seenHttp = $false
  for ($i = 0; $i -lt $vLines.Count; $i++) {
    $line = $vLines[$i]
    if ($line -match "^  ([a-z][a-z0-9]+):\s*$") { $current = $Matches[1] }
    if ($current -ne $name) { continue }
    $s = $line.Trim()
    if ($s -match "^httpPort:\s*(\d+)") {
      $seenHttp = $true
      if ([int]$Matches[1] -ne $httpPort) {
        $fail.Add("values.yaml $name httpPort $($Matches[1]) != manifest $httpPort")
      }
    }
    elseif ($s -match "^service: \{ type: ClusterIP, port: (\d+) \}") {
      if ([int]$Matches[1] -ne $httpPort) {
        $fail.Add("values.yaml $name service.port $($Matches[1]) != manifest $httpPort")
      }
    }
    elseif ($s -match "^grpcPort:\s*(\d+)") {
      $grpcExpected = if ($null -ne $svc.grpcPort) { [int]$svc.grpcPort } else { $null }
      if ($null -ne $grpcExpected -and [int]$Matches[1] -ne $grpcExpected) {
        $fail.Add("values.yaml $name grpcPort $($Matches[1]) != manifest $grpcExpected")
      }
    }
    elseif ($s -match "^readinessProbe: \{ httpGet: \{ path: (?<p>[^,]+), port: (?<port>\d+) \}") {
      if ([int]$Matches.port -ne $httpPort) {
        $fail.Add("values.yaml $name readinessProbe port $($Matches.port) != manifest httpPort $httpPort")
      }
    }
    elseif ($s -match "^livenessProbe: \{ httpGet: \{ path: (?<p>[^,]+), port: (?<port>\d+) \}") {
      if ([int]$Matches.port -ne $httpPort) {
        $fail.Add("values.yaml $name livenessProbe port $($Matches.port) != manifest httpPort $httpPort")
      }
    }
  }
  if (-not $seenHttp) {
    $fail.Add("values.yaml 缺 $name 的 httpPort（或服务块名不匹配）")
  }
}

# 3) serviceUrls 已登记端口校验 —— 自动枚举 values.yaml serviceUrls.* 全部键
$serviceUrlKeys = @()
for ($i = 0; $i -lt $vLines.Count; $i++) {
  if ($vLines[$i] -match '^(\s*)serviceUrls:\s*$') {
    $baseIndent = $Matches[1].Length
    for ($j = $i + 1; $j -lt $vLines.Count; $j++) {
      $line = $vLines[$j]
      if ($line.Trim() -eq '' -or $line.TrimStart().StartsWith('#')) { continue }
      $indent = $line.Length - $line.TrimStart().Length
      if ($indent -le $baseIndent) { break }  # 回到 serviceUrls 同级或更浅层级，段结束
      if ($line -match '^\s+([A-Za-z]+):\s*\{ service: "([a-z0-9-]+)", port: (\d+) \}\s*$') {
        $serviceUrlKeys += [pscustomobject]@{ Key = $Matches[1]; Service = $Matches[2]; Port = [int]$Matches[3] }
      }
    }
    break
  }
}
$serviceUrlChecked = 0
foreach ($item in $serviceUrlKeys) {
  # 以 values.yaml 中登记的 service 名反查 manifest（比键名推断更可靠）
  $svc = $manifest.services | Where-Object { $_.name -eq $item.Service -or $_.composeService -like "*$($item.Service)*" } | Select-Object -First 1
  if (-not $svc) { continue }
  $serviceUrlChecked++
  if ($item.Port -ne [int]$svc.httpPort) {
    $fail.Add("values.yaml serviceUrls.$($item.Key) port $($item.Port) != manifest $($svc.name) httpPort $($svc.httpPort)")
  }
}

# 4) Consul KV 种子 ServiceUrls__* 主机名一致性（主机名形如 leno-<composeService>，如 leno-order-api / leno-access-control-api）
$kvSeedPath = Join-Path $RepoRoot "deploy/consul/kv-seed.json"
$kvChecked = 0
if (Test-Path $kvSeedPath) {
  $kvSeed = Get-Content $kvSeedPath -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($entry in @($kvSeed.items)) {
    if ($entry.key -notmatch '^ServiceUrls__([A-Za-z]+)$') { continue }
    # 映射规则：ServiceUrls__<X>Api -> 去掉 Api 后缀 -> manifest.name（忽略大小写，如 AccessControl -> accesscontrol）
    $apiName = $Matches[1] -replace 'Api$', ''
    $svc = $manifest.services | Where-Object { $_.name -ieq $apiName } | Select-Object -First 1
    if (-not $svc) { continue }
    $url = [string]$entry.value
    # value 为 ${ENV_VAR} 占位符时，以非敏感 default 为准校验
    if ($url -match '^\$\{[^}]*\}$' -and $entry.PSObject.Properties['default']) {
      $url = [string]$entry.default
    }
    $expectedHost = "leno-$($svc.composeService)"
    $kvChecked++
    # 精确比对主机名（子串匹配会把 leno-order-api-wrong 误判为合法），端口/路径不参与
    $hostMatch = [regex]::Match($url, '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?\s]+)')
    if (-not $hostMatch.Success) {
      $fail.Add("kv-seed.json $($entry.key) 值 '$url' 无法解析出主机名（预期主机 '$expectedHost'）")
    }
    elseif ($hostMatch.Groups[1].Value -ne $expectedHost) {
      $fail.Add("kv-seed.json $($entry.key) 值 '$url' 未指向 manifest 服务的预期主机 '$expectedHost'（实际主机 '$($hostMatch.Groups[1].Value)'）")
    }
  }
}

if ($fail.Count -gt 0) {
  Write-Output "::error::服务端口三方一致性校验失败（共 $($fail.Count) 项）："
  foreach ($f in $fail) { Write-Output "  - $f" }
  exit 1
}

Write-Output "服务端口三方一致性校验通过：manifest / compose / Helm values / Consul KV 种子 ServiceUrls 全部一致（serviceUrls 枚举 $serviceUrlChecked 项、kv-seed 命中 $kvChecked 项，共 $($services.Count) 个服务）。"
exit 0

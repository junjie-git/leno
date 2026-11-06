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

# ACL gRPC 客户端键 -> manifest.name（键与代码读取键 AntiCorruption:GrpcEndpoints:<Key> 一致）
$bcKeyToName = @{
  Product = 'product'; Cart = 'cart'; Order = 'order'; Promotion = 'promotion'
  Payment = 'payment'; SellerShop = 'sellershop'; Points = 'points'
  Review = 'review'; AccessControl = 'accesscontrol'; Inventory = 'inventory'; Identity = 'identity'
}

# 实际承载 gRPC 服务的 BC（MapGrpcService）；identity / aftersales 仅作为客户端，不校验 Grpc 监听端点
$grpcServerNames = @('product', 'cart', 'order', 'promotion', 'payment', 'sellershop', 'points', 'review')

# 按缩进定位 compose 服务块：从 "  <service>:" 起，到下一个两空格缩进的同级键为止
function Get-ComposeServiceBlock {
  param([string]$Text, [string]$ServiceName)
  $start = $Text.IndexOf("`n  ${ServiceName}:")
  if ($start -lt 0) { return $null }
  $body = $Text.Substring($start + 1)
  $next = [regex]::Match($body, "`r?`n  [a-z][a-z0-9-]*:\s*`r?`n")
  if ($next.Success) { return $body.Substring(0, $next.Index) }
  return $body
}

foreach ($svc in $services) {
  $name = $svc.name
  $composeName = $svc.composeService
  $httpPort = [int]$svc.httpPort
  $grpcPort = if ($null -ne $svc.grpcPort) { [int]$svc.grpcPort } else { $null }

  # 1) compose：宿主端口映射 + gRPC 独立端点 + 服务端内部钥
  $idx = $compose.IndexOf("  ${composeName}:")
  if ($idx -lt 0) {
    $fail.Add("compose 缺服务块：$composeName")
    $block = $null
  }
  else {
    $block = Get-ComposeServiceBlock -Text $compose -ServiceName $composeName
    $m = [regex]::Match($block, '"(\d+):8080"')
    if (-not $m.Success) {
      $fail.Add("compose $composeName 未找到端口映射")
    }
    elseif ([int]$m.Groups[1].Value -ne $httpPort) {
      $fail.Add("compose $composeName 端口 $($m.Groups[1].Value) != manifest $httpPort")
    }

    if ($null -ne $grpcPort -and $grpcServerNames -contains $name) {
      if ($block -notmatch "Kestrel__Endpoints__Grpc__Url=http://\+:$grpcPort(\s|`r?`n)") {
        $fail.Add("compose $composeName 缺 gRPC 监听端点 Kestrel__Endpoints__Grpc__Url=http://+:$grpcPort")
      }
      if ($block -notmatch "Kestrel__Endpoints__Grpc__Protocols=Http2") {
        $fail.Add("compose $composeName gRPC 端点协议必须为 Http2")
      }
      if ($block -notmatch "AntiCorruption__InternalApiKey=") {
        $fail.Add("compose $composeName 缺 AntiCorruption__InternalApiKey（gRPC 服务端拦截器 fail-closed）")
      }
    }

    # 客户端 gRPC 端点端口必须等于目标 BC 的 manifest grpcPort
    foreach ($em in [regex]::Matches($block, "AntiCorruption__GrpcEndpoints__([A-Za-z]+)=http://[^:\s]+:(\d+)")) {
      $key = $em.Groups[1].Value
      $port = [int]$em.Groups[2].Value
      if (-not $bcKeyToName.ContainsKey($key)) { continue }
      $target = $manifest.services | Where-Object { $_.name -eq $bcKeyToName[$key] } | Select-Object -First 1
      if (-not $target) { continue }
      if ($port -ne [int]$target.grpcPort) {
        $fail.Add("compose $composeName 端点 $key 端口 $port != manifest $($target.name) grpcPort $($target.grpcPort)")
      }
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

# 5) Consul KV 种子 AntiCorruption__GrpcEndpoints__*：主机名 == leno-<composeService>，端口 == manifest.grpcPort
$kvGrpcChecked = 0
if (Test-Path $kvSeedPath) {
  $kvSeed = Get-Content $kvSeedPath -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($entry in @($kvSeed.items)) {
    if ($entry.key -notmatch '^AntiCorruption__GrpcEndpoints__([A-Za-z]+)$') { continue }
    $key = $Matches[1]
    if (-not $bcKeyToName.ContainsKey($key)) { continue }
    $target = $manifest.services | Where-Object { $_.name -eq $bcKeyToName[$key] } | Select-Object -First 1
    if (-not $target) { continue }
    $url = [string]$entry.value
    if ($url -match '^\$\{[^}]*\}$' -and $entry.PSObject.Properties['default']) {
      $url = [string]$entry.default
    }
    $expectedHost = "leno-$($target.composeService)"
    $expectedPort = [int]$target.grpcPort
    $kvGrpcChecked++
    $parsed = [regex]::Match($url, '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?\s]+):(\d+)$')
    if (-not $parsed.Success) {
      $fail.Add("kv-seed.json $($entry.key) 值 '$url' 无法解析为 http://<host>:<port>（预期 ${expectedHost}:${expectedPort}）")
      continue
    }
    if ($parsed.Groups[1].Value -ne $expectedHost) {
      $fail.Add("kv-seed.json $($entry.key) 主机 $($parsed.Groups[1].Value) != 预期 $expectedHost")
    }
    if ([int]$parsed.Groups[2].Value -ne $expectedPort) {
      $fail.Add("kv-seed.json $($entry.key) 端口 $($parsed.Groups[2].Value) != manifest $($target.name) grpcPort $expectedPort")
    }
  }
}

# 6) values.yaml global.grpcEndpoints.*：service == manifest.name，port == manifest.grpcPort
$grpcEndpointChecked = 0
for ($i = 0; $i -lt $vLines.Count; $i++) {
  if ($vLines[$i] -match '^(\s*)grpcEndpoints:\s*$') {
    $baseIndent = $Matches[1].Length
    for ($j = $i + 1; $j -lt $vLines.Count; $j++) {
      $line = $vLines[$j]
      if ($line.Trim() -eq '' -or $line.TrimStart().StartsWith('#')) { continue }
      $indent = $line.Length - $line.TrimStart().Length
      if ($indent -le $baseIndent) { break }
      if ($line -notmatch '^\s+([A-Za-z]+):\s*\{ service: "([a-z0-9-]+)", port: (\d+) \}\s*$') { continue }
      $key = $Matches[1]
      $serviceName = $Matches[2]
      $port = [int]$Matches[3]
      if (-not $bcKeyToName.ContainsKey($key)) {
        $fail.Add("values.yaml global.grpcEndpoints.$key 未登记到 ACL 客户端键映射")
        continue
      }
      $target = $manifest.services | Where-Object { $_.name -eq $bcKeyToName[$key] } | Select-Object -First 1
      if (-not $target) {
        $fail.Add("values.yaml global.grpcEndpoints.$key 在 manifest 中无对应服务")
        continue
      }
      $grpcEndpointChecked++
      if ($serviceName -ne $target.name) {
        $fail.Add("values.yaml global.grpcEndpoints.$key service '$serviceName' != manifest $($target.name)")
      }
      if ($port -ne [int]$target.grpcPort) {
        $fail.Add("values.yaml global.grpcEndpoints.$key port $port != manifest $($target.name) grpcPort $($target.grpcPort)")
      }
    }
    break
  }
}

if ($fail.Count -gt 0) {
  Write-Output "::error::服务端口三方一致性校验失败（共 $($fail.Count) 项）："
  foreach ($f in $fail) { Write-Output "  - $f" }
  exit 1
}

Write-Output "服务端口三方一致性校验通过：manifest / compose / Helm values / Consul KV 种子全部一致（serviceUrls 枚举 $serviceUrlChecked 项、grpcEndpoints 枚举 $grpcEndpointChecked 项、kv-seed ServiceUrls 命中 $kvChecked 项、kv-seed gRPC 端点命中 $kvGrpcChecked 项，共 $($services.Count) 个服务）。"
exit 0

<#
.SYNOPSIS
  服务端口三方一致性校验（双轨下线 A5，2026-09-23）。

.DESCRIPTION
  以 deploy/services.manifest.json 为单一来源，校验：
    1. docker-compose.yml        —— <composeService> 的宿主端口 == manifest.httpPort
    2. deploy/helm/leno/values.yaml —— services.<name>.httpPort == manifest.httpPort
                                     services.<name>.service.port == manifest.httpPort
                                     services.<name>.grpcPort == manifest.grpcPort（如有）
    3. values.yaml serviceUrls.* 中已登记的端口与 manifest 一致（仅校验可映射项）
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
  }
  if (-not $seenHttp) {
    $fail.Add("values.yaml 缺 $name 的 httpPort（或服务块名不匹配）")
  }
}

# 3) serviceUrls 已登记端口抽查（映射：Api 后缀 -> manifest name）
$urlMap = @{
  "PaymentApi" = "payment"; "OrderApi" = "order"; "ProductApi" = "product"
  "PromotionApi" = "promotion"; "IdentityApi" = "identity"
}
foreach ($k in $urlMap.Keys) {
  $n = $urlMap[$k]
  $svc = $manifest.services | Where-Object { $_.name -eq $n } | Select-Object -First 1
  if (-not $svc) { continue }
  $m = [regex]::Match($values, "${k}: \{ service: ""[a-z]+"", port: (\d+) \}")
  if ($m.Success -and [int]$m.Groups[1].Value -ne [int]$svc.httpPort) {
    $fail.Add("values.yaml serviceUrls.$k 端口 $($m.Groups[1].Value) != manifest $($svc.httpPort)")
  }
}

if ($fail.Count -gt 0) {
  Write-Output "::error::服务端口三方一致性校验失败（共 $($fail.Count) 项）："
  foreach ($f in $fail) { Write-Output "  - $f" }
  exit 1
}

Write-Output "服务端口三方一致性校验通过：manifest / compose / Helm values 共 $($services.Count) 个服务全部一致。"
exit 0

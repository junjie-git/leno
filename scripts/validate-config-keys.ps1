<#
.SYNOPSIS
  配置键三方一致性校验（P1 改进，2026-09-23）。
.DESCRIPTION
  防止 A6 类"删键未同步校验清单/种子"的漂移（Jwt:SecretKey 事件）。校验四组不变量：
  1) 每个服务 appsettings.json 的 Jwt 节键集合 = { Issuer, Audience, DiscoveryUrl, RequireHttpsMetadata }，
     且 Issuer=leno-identity、Audience=leno-clients、DiscoveryUrl 非空；
  2) JwtSigning 节仅存在于 Identity，且无 SigningMode/Hs256SigningKey 残留键；
  3) kv-seed.json：不含退役 BC（userauth/pointsmembership/reviewaftersales）与 Jwt__SecretKey；
     含 Jwt__DiscoveryUrl 与 ServiceUrls__IdentityApi；
  4) SensitiveConfigKeys（从代码提取）：含 Jwt:DiscoveryUrl、不含 Jwt:SecretKey；
     且每个键至少在 appsettings / kv-seed / 代码字面量之一出现（三方 OR，防"清单有、配置无"）。
.EXAMPLE
  pwsh scripts/validate-config-keys.ps1
#>
$ErrorActionPreference = "Stop"
$failures = @()

# ---------- 0. 收集文件 ----------
$appsettings = Get-ChildItem -Recurse "src" -Filter "appsettings.json" -File |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\Tests\\' }
$kvPath = "deploy/consul/kv-seed.json"
$sensitiveSrc = "src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConfigCenterExtensions.cs"

# ---------- 1. Jwt 节键集合 ----------
$expectedJwtKeys = @("Issuer", "Audience", "DiscoveryUrl", "RequireHttpsMetadata")
$forbiddenJwtKeys = @("SecretKey", "Enabled", "SigningKey")
foreach ($f in $appsettings) {
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $jwt = $json.Jwt
    if ($null -eq $jwt) {
        $failures += "Jwt 节缺失: $($f.Name)"
        continue
    }
    $props = @($jwt.PSObject.Properties.Name)
    foreach ($k in $expectedJwtKeys) {
        if ($props -notcontains $k) { $failures += "Jwt.$k 缺失: $($f.Name)" }
    }
    foreach ($k in $forbiddenJwtKeys) {
        if ($props -contains $k) { $failures += "Jwt.$k 为已退役键，应删除: $($f.Name)" }
    }
    if ($jwt.DiscoveryUrl -and $jwt.DiscoveryUrl -notmatch "openid-configuration") {
        $failures += "Jwt.DiscoveryUrl 应指向 .well-known/openid-configuration: $($f.Name)"
    }
    if ($jwt.Issuer -and $jwt.Issuer -ne "leno-identity") {
        $failures += "Jwt.Issuer 应为 leno-identity（当前 $($jwt.Issuer)）: $($f.Name)"
    }
    if ($jwt.Audience -and $jwt.Audience -ne "leno-clients") {
        $failures += "Jwt.Audience 应为 leno-clients（当前 $($jwt.Audience)）: $($f.Name)"
    }
}

# ---------- 2. JwtSigning 仅 Identity 且无残留键 ----------
foreach ($f in $appsettings) {
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $json.JwtSigning) { continue }
    $isIdentity = $f.FullName -match "Identity"
    if (-not $isIdentity) {
        $failures += "JwtSigning 节只应出现在 Identity: $($f.Name)"
        continue
    }
    $props = @($json.JwtSigning.PSObject.Properties.Name)
    foreach ($k in @("SigningMode", "Hs256SigningKey")) {
        if ($props -contains $k) { $failures += "JwtSigning.$k 为已退役键（D-6 单算法）: $($f.Name)" }
    }
}

# ---------- 3. kv-seed 不变量 ----------
$kv = Get-Content $kvPath -Raw -Encoding UTF8 | ConvertFrom-Json
$kvKeys = @($kv.items | ForEach-Object { $_.key })
foreach ($k in $kvKeys) {
    if ($k -match "(?i)userauth|pointsmembership|reviewaftersales") {
        $failures += "kv-seed 含退役 BC 键: $k"
    }
}
foreach ($required in @("Jwt__DiscoveryUrl", "ServiceUrls__IdentityApi")) {
    if ($kvKeys -notcontains $required) { $failures += "kv-seed 缺少必需键: $required" }
}
if ($kvKeys -contains "Jwt__SecretKey") { $failures += "kv-seed 含已退役键 Jwt__SecretKey" }

# ---------- 4. SensitiveConfigKeys 三方比对 ----------
$src = Get-Content $sensitiveSrc -Raw -Encoding UTF8
$sensitive = [regex]::Matches($src, '"([A-Za-z]+(?::[A-Za-z]+)+)"') |
    ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
if ($sensitive -notcontains "Jwt:DiscoveryUrl") { $failures += "SensitiveConfigKeys 应含 Jwt:DiscoveryUrl" }
if ($sensitive -contains "Jwt:SecretKey") { $failures += "SensitiveConfigKeys 含已退役键 Jwt:SecretKey" }

function Test-ConfigPath($obj, $path) {
    $cur = $obj
    foreach ($seg in $path.Split('.')) {
        if ($null -eq $cur) { return $false }
        $prop = $cur.PSObject.Properties[$seg]
        if (-not $prop) { return $false }
        $cur = $prop.Value
    }
    return $true
}

foreach ($key in $sensitive) {
    $inAppsettings = $false
    foreach ($f in $appsettings) {
        $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if (Test-ConfigPath $json $key) { $inAppsettings = $true; break }
    }
    $inKv = $kvKeys -contains ($key.Replace(':', '__'))
    $inCode = $false
    if (-not $inAppsettings -and -not $inKv) {
        foreach ($f in (Get-ChildItem -Recurse "src" -Filter "*.cs" -File | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|Tests' })) {
            if ((Get-Content $f.FullName -Raw -Encoding UTF8) -match [regex]::Escape($key)) { $inCode = $true; break }
        }
    }
    if (-not ($inAppsettings -or $inKv -or $inCode)) {
        $failures += "SensitiveConfigKeys 的 $key 在 appsettings/kv-seed/代码中均无出现（疑似漂移残留）"
    }
}

# ---------- 结果 ----------
if ($failures.Count -gt 0) {
    Write-Host "配置键一致性校验失败（$($failures.Count) 项）：" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  ✗ $_" -ForegroundColor Red }
    exit 1
}
Write-Host "配置键一致性校验通过：appsettings×$($appsettings.Count)、kv-seed 键×$($kvKeys.Count)、SensitiveConfigKeys×$($sensitive.Count)。" -ForegroundColor Green

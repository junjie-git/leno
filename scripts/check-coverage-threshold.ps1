#!/usr/bin/env pwsh
<#
.SYNOPSIS
校验各项目测试覆盖率门槛（M5.5）。
.DESCRIPTION
解析 cobertura XML，按项目分类校验：
- Domain 层 >= 80%
- Application 层 >= 60%
- Infrastructure 层 >= 40%
不达标则退出码 1，CI 阻断。
#>

param(
    [string]$TestResultsDir = "./TestResults",
    [double]$DomainThreshold = 80.0,
    [double]$ApplicationThreshold = 60.0,
    [double]$InfrastructureThreshold = 40.0
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TestResultsDir)) {
    Write-Error "测试结果目录不存在: $TestResultsDir"
    exit 1
}

$totalCoverageByCategory = @{
    "Domain" = @{ Sum = 0.0; Count = 0; Threshold = $DomainThreshold }
    "Application" = @{ Sum = 0.0; Count = 0; Threshold = $ApplicationThreshold }
    "Infrastructure" = @{ Sum = 0.0; Count = 0; Threshold = $InfrastructureThreshold }
}

Get-ChildItem -Path $TestResultsDir -Recurse -Filter "coverage.cobertura.xml" | ForEach-Object {
    [xml]$xml = Get-Content $_.FullName
    $lineRate = [double]$xml.coverage."line-rate" * 100
    $assemblyName = $xml.coverage.packages.package[0].name

    $category = $null
    if ($assemblyName -match "\.Domain(\.Tests)?$") { $category = "Domain" }
    elseif ($assemblyName -match "\.Application(\.Tests)?$") { $category = "Application" }
    elseif ($assemblyName -match "\.Infrastructure(\.Tests)?$") { $category = "Infrastructure" }

    if ($category) {
        $totalCoverageByCategory[$category].Sum += $lineRate
        $totalCoverageByCategory[$category].Count += 1
        Write-Host "$assemblyName ($category): $lineRate%"
    }
}

$failed = $false
foreach ($cat in $totalCoverageByCategory.Keys) {
    $data = $totalCoverageByCategory[$cat]
    if ($data.Count -eq 0) {
        Write-Warning "$cat 层无覆盖率数据"
        continue
    }
    $avg = $data.Sum / $data.Count
    $status = if ($avg -ge $data.Threshold) { "PASS" } else { "FAIL" }
    if ($avg -lt $data.Threshold) { $failed = $true }
    Write-Host "$cat 层平均覆盖率: $avg% (门槛 $($data.Threshold)%) [$status]"
}

if ($failed) {
    Write-Error "覆盖率门槛校验失败，请提升测试覆盖率后重试"
    exit 1
}

Write-Host "覆盖率门槛校验通过"
exit 0

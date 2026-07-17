<#
.SYNOPSIS
  解析 reportgenerator 生成的 JSON summary，按层校验覆盖率门槛。

.DESCRIPTION
  输入：coverage-results/ 目录下 reportgenerator 生成的 summary.json 文件
  规则：按 scripts/coverage-thresholds.json 配置的层与最小 line coverage 校验
  豁免：temporaryExemptions 列出的项目仅警告不阻止合并（F4 合并后转为阻止）
  退出码：0 全部通过；1 任一未豁免项目低于阈值

.EXAMPLE
  pwsh scripts/check-coverage.ps1 -CoverageResultsPath coverage-results/
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$CoverageResultsPath
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $PSScriptRoot "coverage-thresholds.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$hasError = $false

foreach ($threshold in $config.thresholds) {
    Write-Host "校验 $($threshold.layer) 层覆盖率（阈值 $($threshold.minimumLineCoverage)%）..."

    $summaryFiles = Get-ChildItem -Path $CoverageResultsPath -Filter "summary.json" -Recurse

    foreach ($summaryFile in $summaryFiles) {
        $summary = Get-Content $summaryFile.FullName -Raw | ConvertFrom-Json

        foreach ($assembly in $summary.summary) {
            $assemblyName = $assembly.assembly
            $lineCoverage = [double]$assembly.linecoverage

            if ($assemblyName -notmatch $threshold.pathPattern) {
                continue
            }

            $projectName = $assemblyName -replace '\.dll$', ''
            $exemption = $config.temporaryExemptions.$projectName

            if ($exemption) {
                Write-Host "::warning::$projectName 覆盖率 $lineCoverage% 低于 $($threshold.minimumLineCoverage)%（豁免中：$($exemption.reason))" -ForegroundColor Yellow
                continue
            }

            if ($lineCoverage -lt $threshold.minimumLineCoverage) {
                Write-Host "::error::$projectName 覆盖率 $lineCoverage% 低于阈值 $($threshold.minimumLineCoverage)%"
                $hasError = $true
            } else {
                Write-Host "$projectName 覆盖率 $lineCoverage% 通过阈值 $($threshold.minimumLineCoverage)%" -ForegroundColor Green
            }
        }
    }
}

if ($hasError) {
    Write-Host "覆盖率校验失败，请提升测试覆盖后重试" -ForegroundColor Red
    exit 1
}

Write-Host "全部项目覆盖率通过门槛校验" -ForegroundColor Green
exit 0

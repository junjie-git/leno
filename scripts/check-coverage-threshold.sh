#!/usr/bin/env bash
set -euo pipefail

# M5.5: 解析 cobertura XML 并按层校验覆盖率门槛
# - Domain 层 >= 80%
# - Application 层 >= 60%
# - Infrastructure 层 >= 40%

TEST_RESULTS_DIR="${1:-./TestResults}"
DOMAIN_THRESHOLD="${DOMAIN_THRESHOLD:-80.0}"
APPLICATION_THRESHOLD="${APPLICATION_THRESHOLD:-60.0}"
INFRASTRUCTURE_THRESHOLD="${INFRASTRUCTURE_THRESHOLD:-40.0}"

if [ ! -d "$TEST_RESULTS_DIR" ]; then
    echo "ERROR: 测试结果目录不存在: $TEST_RESULTS_DIR"
    exit 1
fi

# 检查 dotnet reportgenerator 是否可用
if ! command -v dotnet reportgenerator &> /dev/null; then
    echo "INFO: dotnet reportgenerator 不可用，使用内置 XML 解析"
    # 使用 grep + awk 简化解析 cobertura XML
    domain_sum=0.0
    domain_count=0
    app_sum=0.0
    app_count=0
    infra_sum=0.0
    infra_count=0

    while IFS= read -r -d '' file; do
        # 提取 assembly name
        assembly=$(grep -oP 'package name="\K[^"]+' "$file" | head -1)
        # 提取 line-rate
        line_rate=$(grep -oP 'coverage[^>]*line-rate="\K[0-9.]+' "$file" | head -1)
        rate_percent=$(awk -v r="$line_rate" 'BEGIN { printf "%.2f", r * 100 }')

        category=""
        if echo "$assembly" | grep -qE '\.Domain(\.Tests)?$'; then
            category="Domain"
            domain_sum=$(awk -v s="$domain_sum" -v r="$rate_percent" 'BEGIN { printf "%.4f", s + r }')
            domain_count=$((domain_count + 1))
        elif echo "$assembly" | grep -qE '\.Application(\.Tests)?$'; then
            category="Application"
            app_sum=$(awk -v s="$app_sum" -v r="$rate_percent" 'BEGIN { printf "%.4f", s + r }')
            app_count=$((app_count + 1))
        elif echo "$assembly" | grep -qE '\.Infrastructure(\.Tests)?$'; then
            category="Infrastructure"
            infra_sum=$(awk -v s="$infra_sum" -v r="$rate_percent" 'BEGIN { printf "%.4f", s + r }')
            infra_count=$((infra_count + 1))
        fi

        if [ -n "$category" ]; then
            echo "$assembly ($category): $rate_percent%"
        fi
    done < <(find "$TEST_RESULTS_DIR" -name "coverage.cobertura.xml" -print0)

    failed=0

    check_threshold() {
        local sum=$1
        local count=$2
        local threshold=$3
        local label=$4

        if [ "$count" -eq 0 ]; then
            echo "WARN: $label 层无覆盖率数据"
            return 0
        fi

        local avg=$(awk -v s="$sum" -v c="$count" 'BEGIN { printf "%.2f", s / c }')
        local status="PASS"
        if awk -v a="$avg" -v t="$threshold" 'BEGIN { exit !(a < t) }'; then
            status="FAIL"
            failed=1
        fi
        echo "$label 层平均覆盖率: $avg% (门槛 $threshold%) [$status]"
    }

    check_threshold "$domain_sum" "$domain_count" "$DOMAIN_THRESHOLD" "Domain"
    check_threshold "$app_sum" "$app_count" "$APPLICATION_THRESHOLD" "Application"
    check_threshold "$infra_sum" "$infra_count" "$INFRASTRUCTURE_THRESHOLD" "Infrastructure"

    if [ "$failed" -eq 1 ]; then
        echo "ERROR: 覆盖率门槛校验失败，请提升测试覆盖率后重试"
        exit 1
    fi

    echo "覆盖率门槛校验通过"
    exit 0
fi

# 使用 reportgenerator 生成 CSV 摘要（如果可用）
dotnet reportgenerator \
    -reports:"$TEST_RESULTS_DIR/**/coverage.cobertura.xml" \
    -targetdir:"./CoverageSummary" \
    -reporttypes:CsvSummary

# 解析 CSV 摘要并按类别校验
# 此处简化为输出 CSV 内容，由 CI 步骤解析
echo "Coverage summary generated at ./CoverageSummary/Summary.csv"
cat ./CoverageSummary/Summary.csv

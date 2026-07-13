#!/bin/bash
set -euo pipefail

echo "🔍 扫描占位实现..."

ERRORS=0

RESULTS=$(grep -rn "NotImplementedException" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 NotImplementedException："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

RESULTS=$(grep -rn "SmokeTest_ShouldPass\|true\.Should()\.BeTrue()\|Assert\.True(true)" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 SmokeTest 占位："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

RESULTS=$(grep -rn "return default!\|return null!" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" --exclude-dir="*Tests*" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 return default!/null! 占位："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

if [ $ERRORS -gt 0 ]; then
    echo ""
    echo "❌ 检测到 $ERRORS 类占位实现，请替换为真实业务逻辑后重试。"
    exit 1
fi

echo "✅ 未检测到占位实现。"

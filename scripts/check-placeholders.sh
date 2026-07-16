#!/bin/bash
# 检测仓库中的占位实现与测试占位文件，发现即 exit 1。
# 适用范围：src/ 下所有 *.cs（排除 obj/bin）。
set -euo pipefail

echo "🔍 扫描占位实现..."

ERRORS=0

# 1. NotImplementedException 占位实现
RESULTS=$(grep -rn "NotImplementedException" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 NotImplementedException："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

# 2. SmokeTest_ShouldPass / true.Should().BeTrue() / Assert.True(true) 等空断言占位测试
RESULTS=$(grep -rn "SmokeTest_ShouldPass\|true\.Should()\.BeTrue()\|Assert\.True(true)" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 SmokeTest 占位："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

# 3. NewFeatureTests*.cs 占位测试文件（应已重命名为具名测试文件）
RESULTS=$(find src/ -type f -name "NewFeatureTests*.cs" \
    -not -path "*/obj/*" -not -path "*/bin/*" || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 NewFeatureTests*.cs 占位测试文件："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

# 4. TODO / FIXME 占位注释
RESULTS=$(grep -rn "TODO\|FIXME" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 TODO/FIXME 占位注释："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

# 5. return default! / return null! 占位返回值（非测试项目）
RESULTS=$(grep -rn "return default!\|return null!" \
    --include="*.cs" \
    --exclude-dir="obj" --exclude-dir="bin" --exclude-dir="*Tests*" \
    src/ || true)
if [ -n "$RESULTS" ]; then
    echo "❌ 发现 return default!/null! 占位："
    echo "$RESULTS"
    ERRORS=$((ERRORS + 1))
fi

# 6. 空测试类：测试项目中只声明了 class 但没有任何 [Fact]/[Theory] 测试方法
EMPTY_TEST_CLASSES=""
while IFS= read -r f; do
    [ -z "$f" ] && continue
    # 提取所有 public/internal class 名（仅测试文件中常见的）
    while IFS= read -r line; do
        # 仅处理 class 声明行
        if echo "$line" | grep -qE "^\s*(public|internal|file\s+)?\s*(sealed\s+|abstract\s+|static\s+)*class\s+"; then
            CLASS_NAME=$(echo "$line" | sed -E 's/.*class\s+([A-Za-z0-9_]+).*/\1/')
            # 仅扫描以 Tests 结尾或所在文件以 Tests.cs 结尾的类
            if echo "$CLASS_NAME" | grep -qE "Tests$" || echo "$f" | grep -qE "Tests\.cs$"; then
                # 检查该文件内是否包含 [Fact] 或 [Theory]
                if ! grep -qE "\[(Fact|Theory)\]" "$f"; then
                    EMPTY_TEST_CLASSES="${EMPTY_TEST_CLASSES}${f}:${CLASS_NAME}\n"
                fi
            fi
        fi
    done < <(grep -nE "class\s+[A-Za-z0-9_]+" "$f")
done < <(find src/ -type f -name "*.cs" \
    -path "*Tests*" \
    -not -path "*/obj/*" -not -path "*/bin/*" || true)

if [ -n "$EMPTY_TEST_CLASSES" ]; then
    echo "❌ 发现空测试类（无 [Fact]/[Theory] 方法）："
    echo -e "$EMPTY_TEST_CLASSES"
    ERRORS=$((ERRORS + 1))
fi

if [ $ERRORS -gt 0 ]; then
    echo ""
    echo "❌ 检测到 $ERRORS 类占位实现，请替换为真实业务逻辑后重试。"
    exit 1
fi

echo "✅ 未检测到占位实现。"

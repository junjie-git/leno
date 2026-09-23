#!/bin/bash
# 云依赖白名单体检（G2-文档，2026-09-24）。
#
# 约束（ADR-0010）：运行时基础设施全自建（SQL Server / Redis / RabbitMQ / Elasticsearch /
# Consul / YARP 均自托管），不引入云托管中间件或云厂商 SDK。
# 唯一登记例外：Azure Key Vault —— 密钥托管后端（可选、默认关闭）。
#
# 白名单的唯一权威来源是 ADR-0010；本脚本的白名单是它的执行副本，两者必须同步修改。
# 适用范围：src/ 下所有 *.csproj 的 <PackageReference>（排除 obj/bin/Tests）。
set -euo pipefail

echo "🔍 扫描云厂商 SDK 依赖..."

# 已登记的例外（只允许这两项，且必须与 ADR-0010 一致）
ALLOWED=(
    "Azure.Identity"                 # AKV 凭据（DefaultAzureCredential）
    "Azure.Security.KeyVault.Keys"   # AKV 密钥/签名客户端
)

# 云厂商 SDK 前缀（命中且不在白名单即判违规）
CLOUD_PATTERN='^(Azure\.|Microsoft\.Azure\.|Amazon\.|AWSSDK\.|Google\.Cloud\.|Google\.Apis\.|AlibabaCloud\.|aliyun|TencentCloud|HuaweiCloud\.)'

ERRORS=0
VIOLATIONS=""
ALLOWED_HITS=""

while IFS= read -r f; do
    [ -z "$f" ] && continue
    # 抓取 <PackageReference Include="X" ...>
    while IFS= read -r pkg; do
        [ -z "$pkg" ] && continue
        if [[ "$pkg" =~ $CLOUD_PATTERN ]]; then
            matched_allowed=0
            for a in "${ALLOWED[@]}"; do
                if [ "$pkg" = "$a" ]; then
                    matched_allowed=1
                    ALLOWED_HITS="${ALLOWED_HITS}${pkg}  <- ${f}\n"
                    break
                fi
            done
            if [ $matched_allowed -eq 0 ]; then
                VIOLATIONS="${VIOLATIONS}${f}: ${pkg}\n"
            fi
        fi
    done < <(grep -oE '<PackageReference Include="[^"]+"' "$f" | sed -E 's/.*Include="([^"]+)"/\1/' || true)
done < <(find src -type f -name "*.csproj" -not -path "*/obj/*" -not -path "*/bin/*" || true)

if [ -n "$VIOLATIONS" ]; then
    echo "❌ 发现未登记的云厂商 SDK 依赖："
    echo -e "$VIOLATIONS"
    echo "   处理方式（二选一）："
    echo "   1) 去掉该云依赖（约束：运行时基础设施全自建）；"
    echo "   2) 若确有必要保留，先在 docs/decisions/0010-cloud-dependency-constraint-and-akv-exception.md"
    echo "      登记为例外（写明范围/审计/回退），再同步本脚本的 ALLOWED 白名单。"
    ERRORS=$((ERRORS + 1))
fi

if [ $ERRORS -gt 0 ]; then
    echo ""
    echo "❌ 云依赖体检失败。"
    exit 1
fi

echo "✅ 云依赖体检通过：仅命中已登记例外。"
echo -e "$ALLOWED_HITS" | sed '/^$/d' | sed 's/^/   /'
using PactNet;
using PactNet.Verifier;

namespace Leno.Contracts.Provider.Tests;

/// <summary>
/// Product BC Provider 契约验证测试（阶段 4.10）。
///
/// 验证 Product BC 的 GET /internal/v1/products/skus/{skuId} 端点遵从
/// Order BC（Consumer）生成的 pact 契约文件。
/// pact 文件由 Consumer 测试生成至仓库根 pacts/Order BC-Product BC.json。
///
/// 本样例作为后续 BC 推广 Provider 验证的模板：
///   1. 启动 Provider API（真实 TCP socket，见 <see cref="ProviderApiFixture"/>）
///   2. PactVerifier 读取 pact 文件，逐交互调用 Provider 端点
///   3. 每个交互前通过 /provider-states 注入测试数据（见 <see cref="ProviderStateMiddleware"/>）
///   4. Verify 通过表示 Provider 未破坏 Consumer 依赖的契约
/// </summary>
public sealed class OrderBcProviderTests : IClassFixture<ProviderApiFixture>
{
    private readonly ProviderApiFixture _fixture;

    public OrderBcProviderTests(ProviderApiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// 验证 Product BC 遵从 Order BC 的全部 pact 契约交互。
    /// </summary>
    [Fact]
    public void EnsureProductBcHonoursPactWithOrderBc()
    {
        var pactPath = ResolvePactFilePath();
        EnsurePactFileExists(pactPath);

        var config = new PactVerifierConfig();

        using var pactVerifier = new PactVerifier("Product BC", config);
        pactVerifier
            .WithHttpEndpoint(_fixture.ServerUri)
            .WithFileSource(new FileInfo(pactPath))
            .WithProviderStateUrl(new Uri(_fixture.ServerUri, "/provider-states"))
            .Verify();
    }

    private static string ResolvePactFilePath()
    {
        var repoRoot = ResolveRepoRoot();
        return Path.Combine(repoRoot, "pacts", "Order BC-Product BC.json");
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Leno.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "无法定位仓库根目录（未找到 Leno.slnx），pact 文件路径解析失败。");
        }

        return dir.FullName;
    }

    private static void EnsurePactFileExists(string pactPath)
    {
        if (!File.Exists(pactPath))
        {
            throw new FileNotFoundException(
                $"未找到 pact 契约文件：{pactPath}。请先运行 Consumer 测试" +
                $"（dotnet test tests/Contracts/Leno.Contracts.Consumer.Tests）生成 pact 文件，再执行 Provider 验证。",
                pactPath);
        }
    }
}

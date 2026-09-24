using PactNet;
using PactNet.Verifier;

namespace Leno.Contracts.Provider.Tests;

/// <summary>
/// Product BC 对 Cart BC 契约的 Provider 验证（P1#16 契约推广第 2 条）。
///
/// 验证 Product BC 的 POST /internal/v1/products/skus/batch 端点遵从
/// Cart BC（Consumer）生成的 pact 契约文件
/// （pacts/Cart BC-Product BC.json，由 CartBcConsumerTests 生成）。
/// </summary>
[Collection("ProductBcProviderApi")]
public sealed class CartBcProviderTests : IClassFixture<ProviderApiFixture>
{
    private readonly ProviderApiFixture _fixture;

    public CartBcProviderTests(ProviderApiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// 验证 Product BC 遵从 Cart BC 的全部 pact 契约交互。
    /// </summary>
    [Fact]
    public void EnsureProductBcHonoursPactWithCartBc()
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
        return Path.Combine(repoRoot, "pacts", "Cart BC-Product BC.json");
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

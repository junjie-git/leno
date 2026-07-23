using Leno.Payment.Domain.Services;
using Leno.Payment.Infrastructure.Channels;
using Leno.Payment.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// 阶段三 3.8 单元测试：验证 <see cref="PaymentChannelPluginLoader"/> 通过
/// <c>Assembly.LoadFrom</c> 动态加载外部插件程序集并扫描实现
/// <see cref="IPaymentChannelAdapter"/> 的非抽象公共类型。
///
/// 覆盖场景：
/// - 空配置（无 PluginAssemblies）返回空结果
/// - null 配置抛 <see cref="ArgumentNullException"/>
/// - 非存在文件路径记录失败项
/// - 非 .dll 扩展名记录失败项
/// - 空路径记录失败项
/// - 有效程序集（本测试程序集）扫描到 <see cref="TestUnionPayPluginAdapter"/> 与 <see cref="TestApplePayPluginAdapter"/>
/// - 混合路径（部分有效 + 部分无效）部分成功
/// - null 构造函数参数抛 <see cref="ArgumentNullException"/>
/// </summary>
public class PaymentChannelPluginLoaderTests
{
    private readonly PaymentChannelPluginLoader _loader = new(NullLogger<PaymentChannelPluginLoader>.Instance);

    /// <summary>
    /// 获取当前测试程序集的 DLL 文件路径，用于验证 <see cref="PaymentChannelPluginLoader.Load"/>
    /// 能通过 <c>Assembly.LoadFrom</c> 加载并扫描到 <see cref="TestUnionPayPluginAdapter"/> 等测试桩适配器。
    /// </summary>
    private static string TestAssemblyPath => typeof(PaymentChannelPluginLoaderTests).Assembly.Location;

    [Fact]
    public void Load_WithNullOptions_ShouldThrowArgumentNullException()
    {
        var act = () => _loader.Load(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Load_WithEmptyPluginAssemblies_ShouldReturnEmptyResult()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = Array.Empty<string>()
        };

        var result = _loader.Load(options);

        result.AdapterTypes.Should().BeEmpty();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public void Load_WithNonExistentFile_ShouldRecordFailure()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { "/nonexistent/path/fake_plugin.dll" }
        };

        var result = _loader.Load(options);

        result.AdapterTypes.Should().BeEmpty();
        result.Failures.Should().HaveCount(1);
        var failure = result.Failures.Single();
        failure.Reason.Should().Contain("不存在");
        failure.Exception.Should().BeNull();
    }

    [Fact]
    public void Load_WithNonDllExtension_ShouldRecordFailure()
    {
        var tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".txt");
        try
        {
            File.WriteAllText(tempFile, "not a dll");
            var options = new PaymentChannelPluginOptions
            {
                PluginAssemblies = new[] { tempFile }
            };

            var result = _loader.Load(options);

            result.AdapterTypes.Should().BeEmpty();
            result.Failures.Should().HaveCount(1);
            result.Failures.Single().Reason.Should().Contain(".dll");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_WithEmptyPath_ShouldRecordFailure()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { "   " }
        };

        var result = _loader.Load(options);

        result.AdapterTypes.Should().BeEmpty();
        result.Failures.Should().HaveCount(1);
        result.Failures.Single().Reason.Should().Contain("为空");
    }

    [Fact]
    public void Load_WithValidTestAssembly_ShouldScanTestPluginAdapterTypes()
    {
        // 测试程序集包含 TestUnionPayPluginAdapter 与 TestApplePayPluginAdapter 两个 public 适配器
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { TestAssemblyPath }
        };

        var result = _loader.Load(options);

        result.Failures.Should().BeEmpty();
        result.AdapterTypes.Should().NotBeEmpty();
        result.AdapterTypes.Should().Contain(typeof(TestUnionPayPluginAdapter));
        result.AdapterTypes.Should().Contain(typeof(TestApplePayPluginAdapter));
    }

    [Fact]
    public void Load_WithValidTestAssembly_ScannedTypesShouldImplementIPaymentChannelAdapter()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { TestAssemblyPath }
        };

        var result = _loader.Load(options);

        result.AdapterTypes.Should().NotBeEmpty();
        foreach (var type in result.AdapterTypes)
        {
            typeof(IPaymentChannelAdapter).IsAssignableFrom(type).Should().BeTrue(
                $"加载器扫描到的类型 {type.FullName} 必须实现 {nameof(IPaymentChannelAdapter)}");
            type.IsAbstract.Should().BeFalse("抽象类型不应被加载器注册");
            type.IsInterface.Should().BeFalse("接口类型不应被加载器注册");
            type.IsClass.Should().BeTrue("加载器只注册 class 类型");
            type.IsPublic.Should().BeTrue("加载器只注册 public 类型");
        }
    }

    [Fact]
    public void Load_WithMixedPaths_PartialSuccessAndPartialFailure()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[]
            {
                TestAssemblyPath,
                "/nonexistent/other_plugin.dll"
            }
        };

        var result = _loader.Load(options);

        // 有效程序集应扫描到至少 2 个测试桩适配器
        result.AdapterTypes.Should().Contain(typeof(TestUnionPayPluginAdapter));
        result.AdapterTypes.Should().Contain(typeof(TestApplePayPluginAdapter));

        // 无效路径应记录 1 个失败
        result.Failures.Should().HaveCount(1);
        result.Failures.Single().Reason.Should().Contain("不存在");
    }

    [Fact]
    public void Load_WithRelativePath_ShouldResolveAndScan()
    {
        // 构造相对路径：先获取绝对路径，再转为相对路径（相对于当前工作目录）
        var absolutePath = Path.GetFullPath(TestAssemblyPath);
        var currentDir = Directory.GetCurrentDirectory();
        var relativePath = Path.GetRelativePath(currentDir, absolutePath);

        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { relativePath }
        };

        var result = _loader.Load(options);

        result.Failures.Should().BeEmpty();
        result.AdapterTypes.Should().Contain(typeof(TestUnionPayPluginAdapter));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new PaymentChannelPluginLoader(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Load_ScannedAdapterTypes_HaveExpectedChannelKeys()
    {
        var options = new PaymentChannelPluginOptions
        {
            PluginAssemblies = new[] { TestAssemblyPath }
        };

        var result = _loader.Load(options);

        // 验证扫描到的测试桩适配器能被实例化并返回预期 ChannelKey
        var unionPayType = result.AdapterTypes.First(t => t == typeof(TestUnionPayPluginAdapter));
        var applePayType = result.AdapterTypes.First(t => t == typeof(TestApplePayPluginAdapter));

        var unionPayInstance = (IPaymentChannelAdapter)Activator.CreateInstance(unionPayType)!;
        var applePayInstance = (IPaymentChannelAdapter)Activator.CreateInstance(applePayType)!;

        unionPayInstance.ChannelKey.Should().Be("UnionPay");
        unionPayInstance.DisplayName.Should().Contain("银联");
        unionPayInstance.IsEnabled.Should().BeTrue();
        unionPayInstance.Capabilities.SupportsRefund.Should().BeTrue();
        unionPayInstance.Capabilities.SupportsPartialCapture.Should().BeTrue();

        applePayInstance.ChannelKey.Should().Be("ApplePay");
        applePayInstance.DisplayName.Should().Contain("Apple");
        applePayInstance.IsEnabled.Should().BeTrue();
        applePayInstance.Capabilities.SupportsRefund.Should().BeFalse();
        applePayInstance.Capabilities.AsyncNotifyMode.Should().Be(AsyncNotifyMode.None);
    }
}

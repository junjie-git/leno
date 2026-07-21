using Leno.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Storage;

/// <summary>
/// ObjectStorageService 构造函数不应 sync-over-async 阻塞线程池。
/// 验证 P0-T5：Bucket 确保延迟到首次使用时异步执行。
/// </summary>
public class ObjectStorageServiceConstructorTests
{
    [Fact]
    public void Constructor_ShouldNotBlockOnEnsureBucketExists()
    {
        // Arrange — 构造函数不应同步调用 EnsureBucketExistsAsync
        var options = Options.Create(new ObjectStorageOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            UseSsl = false,
            BucketName = "test-bucket"
        });
        var logger = new Mock<ILogger<ObjectStorageService>>();

        // Act — 构造函数应快速返回（不阻塞）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var service = new ObjectStorageService(options, logger.Object);
        sw.Stop();

        // Assert — 构造函数不应同步等待网络调用
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "构造函数不应 sync-over-async 阻塞线程池");

        // EnsureBucketExists 应延迟到首次使用时异步执行
        service.IsBucketEnsurePending.Should().BeTrue(
            "Bucket 确保应延迟到首次使用时异步执行");
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        var logger = new Mock<ILogger<ObjectStorageService>>();

        var act = () => new ObjectStorageService(null!, logger.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrow()
    {
        var options = Options.Create(new ObjectStorageOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            UseSsl = false,
            BucketName = "test-bucket"
        });

        var act = () => new ObjectStorageService(options, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

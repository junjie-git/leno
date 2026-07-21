using System.Reflection;
using FluentAssertions;
using Leno.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Minio;
using Minio.DataModel.Args;

namespace Leno.Infrastructure.Tests.Storage;

/// <summary>
/// ObjectStorageService.ExistsAsync 异常日志修复验证。
/// T36：catch 块从静默吞异常改为 LogWarning 记录异常信息与 ObjectName/FileUrl。
/// </summary>
public class ObjectStorageServiceExistsLoggingTests
{
    private static ObjectStorageService CreateService(
        out Mock<ILogger<ObjectStorageService>> loggerMock,
        bool skipBucketEnsure = true)
    {
        var options = Options.Create(new ObjectStorageOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            UseSsl = false,
            BucketName = "test-bucket",
            PublicUrl = "http://localhost:9000/test-bucket"
        });

        loggerMock = new Mock<ILogger<ObjectStorageService>>();
        var service = new ObjectStorageService(options, loggerMock.Object);

        if (skipBucketEnsure)
        {
            // 设置 _bucketEnsured = 1 跳过 EnsureBucketExistsOnceAsync，避免 MinIO 连接
            var bucketEnsuredField = typeof(ObjectStorageService).GetField("_bucketEnsured",
                BindingFlags.NonPublic | BindingFlags.Instance);
            bucketEnsuredField!.SetValue(service, 1);
        }

        return service;
    }

    private static void SetMinioClient(ObjectStorageService service, IMinioClient? client)
    {
        var minioField = typeof(ObjectStorageService).GetField("_minioClient",
            BindingFlags.NonPublic | BindingFlags.Instance);
        minioField!.SetValue(service, client);
    }

    [Fact]
    public async Task ExistsAsync_WhenStatThrowsNonNotFoundException_ShouldLogWarningAndReturnFalse()
    {
        // Arrange — 创建服务并跳过 Bucket 确保
        var service = CreateService(out var loggerMock, skipBucketEnsure: true);

        // 将 _minioClient 设为 null，使 StatObjectAsync 扩展方法抛出 ArgumentNullException
        SetMinioClient(service, null);

        // Act — 调用 ExistsAsync，StatObjectAsync 将抛出异常
        var result = await service.ExistsAsync("http://localhost:9000/test-bucket/test-file.jpg");

        // Assert — 异常被 catch 捕获，返回 false
        result.Should().BeFalse("非 ObjectNotFoundException 异常应被捕获并返回 false");

        // 验证 Warning 日志被调用（包含异常信息）
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MinIO 文件存在性检查失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "catch 块应记录 Warning 日志包含 'MinIO 文件存在性检查失败' 消息");
    }

    [Fact]
    public async Task ExistsAsync_WhenStatThrowsNonNotFoundException_ShouldIncludeObjectNameInLog()
    {
        // Arrange
        var service = CreateService(out var loggerMock, skipBucketEnsure: true);
        SetMinioClient(service, null);

        // Act
        var result = await service.ExistsAsync("http://localhost:9000/test-bucket/avatar/photo.jpg");

        // Assert — 日志应包含 ObjectName
        result.Should().BeFalse();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("avatar/photo.jpg")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Warning 日志应包含 ObjectName 以便运维定位问题");
    }

    [Fact]
    public async Task ExistsAsync_InvalidUrl_ShouldReturnFalseWithoutLogging()
    {
        // Arrange — 无效 URL 在 ValidateUrlInternal 阶段即返回 false，不进入 try 块
        var service = CreateService(out var loggerMock, skipBucketEnsure: true);
        SetMinioClient(service, null);

        // Act
        var result = await service.ExistsAsync("http://wrong-host/file.jpg");

        // Assert — 无效 URL 不应触发 Warning 日志
        result.Should().BeFalse("无效 URL 应直接返回 false");
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "无效 URL 不应进入 StatObjectAsync 调用，不应记录 Warning 日志");
    }
}

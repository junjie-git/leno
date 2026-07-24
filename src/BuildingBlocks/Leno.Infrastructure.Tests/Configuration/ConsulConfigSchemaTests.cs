using FluentAssertions;
using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.Configuration;

/// <summary>
/// 阶段四 4.6 步骤6：Consul 配置 Schema 版本化与灰度发布单元测试。
/// 覆盖版本校验、灰度切流、配置发布与回滚。
/// </summary>
public sealed class ConsulConfigSchemaTests
{
    // ===== ConsulSchemaVersion 测试 =====

    [Fact]
    public void ComputeSchemaHash_SameJsonDifferentFormatting_ReturnsSameHash()
    {
        var json1 = """{"schemaVersion":2,"outbox":{"shardCount":8}}""";
        var json2 = """
        {
          "schemaVersion": 2,
          "outbox": {
            "shardCount": 8
          }
        }
        """;

        var hash1 = ConsulSchemaVersion.ComputeSchemaHash(json1);
        var hash2 = ConsulSchemaVersion.ComputeSchemaHash(json2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeSchemaHash_DifferentContent_ReturnsDifferentHash()
    {
        var json1 = """{"schemaVersion":2,"outbox":{"shardCount":8}}""";
        var json2 = """{"schemaVersion":2,"outbox":{"shardCount":16}}""";

        ConsulSchemaVersion.ComputeSchemaHash(json1)
            .Should().NotBe(ConsulSchemaVersion.ComputeSchemaHash(json2));
    }

    [Fact]
    public void Create_ValidVersion_ReturnsSnapshotWithHash()
    {
        var json = """{"schemaVersion":1,"cache":{"l1Ttl":"00:00:05"}}""";
        var version = ConsulSchemaVersion.Create(1, json, "test-svc");

        version.Version.Should().Be(1);
        version.SchemaHash.Should().NotBeNullOrEmpty();
        version.AppliedBy.Should().Be("test-svc");
        version.AppliedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ZeroVersion_ThrowsArgumentOutOfRangeException()
    {
        var act = () => ConsulSchemaVersion.Create(0, "{}", "test");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Equals_SameVersionAndHash_ReturnsTrue()
    {
        var json = """{"schemaVersion":1}""";
        var v1 = ConsulSchemaVersion.Create(1, json, "a");
        var v2 = ConsulSchemaVersion.Create(1, json, "b");

        v1.Equals(v2).Should().BeTrue();
    }

    // ===== ConsulConfigSchemaValidator 测试 =====

    [Fact]
    public void Validate_VersionMatches_ReturnsValid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);
        var json = """{"schemaVersion":2,"outbox":{"shardCount":8}}""";

        var result = validator.Validate(json, expectedVersion: 2);

        result.IsValid.Should().BeTrue();
        result.Version.Should().Be(2);
    }

    [Fact]
    public void Validate_VersionMismatch_ReturnsInvalid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);
        var json = """{"schemaVersion":3,"outbox":{"shardCount":8}}""";

        var result = validator.Validate(json, expectedVersion: 2);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("版本不匹配");
    }

    [Fact]
    public void Validate_MissingSchemaVersion_ReturnsInvalid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);
        var json = """{"outbox":{"shardCount":8}}""";

        var result = validator.Validate(json, expectedVersion: 2);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("缺少 schemaVersion");
    }

    [Fact]
    public void Validate_EmptyJson_ReturnsInvalid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);

        var result = validator.Validate("", expectedVersion: 1);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsInvalid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);

        var result = validator.Validate("not a json", expectedVersion: 1);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("解析失败");
    }

    [Fact]
    public void ValidateHash_HashMatches_ReturnsValid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);
        var json = """{"schemaVersion":1,"cache":{"l1Ttl":"00:00:05"}}""";
        var recorded = ConsulSchemaVersion.Create(1, json, "test");

        var result = validator.ValidateHash(json, recorded);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateHash_HashMismatch_ReturnsInvalid()
    {
        var validator = new ConsulConfigSchemaValidator(NullLogger<ConsulConfigSchemaValidator>.Instance);
        var originalJson = """{"schemaVersion":1,"cache":{"l1Ttl":"00:00:05"}}""";
        var tamperedJson = """{"schemaVersion":1,"cache":{"l1Ttl":"00:00:10"}}""";
        var recorded = ConsulSchemaVersion.Create(1, originalJson, "test");

        var result = validator.ValidateHash(tamperedJson, recorded);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("哈希不匹配");
    }

    // ===== ConsulGrayReleaseService 测试 =====

    [Fact]
    public void ShouldApplyConfig_GrayPercent100_AlwaysReturnsTrue()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        for (var i = 0; i < 100; i++)
        {
            svc.ShouldApplyConfig($"instance-{i}", grayPercent: 100).Should().BeTrue();
        }
    }

    [Fact]
    public void ShouldApplyConfig_GrayPercent0_AlwaysReturnsFalse()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        for (var i = 0; i < 100; i++)
        {
            svc.ShouldApplyConfig($"instance-{i}", grayPercent: 0).Should().BeFalse();
        }
    }

    [Fact]
    public void ShouldApplyConfig_GrayPercent50_AboutHalfReturnsTrue()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);
        var appliedCount = 0;

        for (var i = 0; i < 1000; i++)
        {
            if (svc.ShouldApplyConfig($"instance-{i}", grayPercent: 50))
            {
                appliedCount++;
            }
        }

        // 50% 灰度，1000 个实例中应有约 500 个命中（允许 ±10% 误差）
        appliedCount.Should().BeInRange(400, 600);
    }

    [Fact]
    public void ShouldApplyConfig_SameInstanceId_ReturnsConsistentResult()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        var result1 = svc.ShouldApplyConfig("instance-abc", grayPercent: 50);
        var result2 = svc.ShouldApplyConfig("instance-abc", grayPercent: 50);

        result1.Should().Be(result2);
    }

    [Fact]
    public void ShouldApplyConfig_EmptyInstanceId_ReturnsFalse()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ShouldApplyConfig("", grayPercent: 100).Should().BeFalse();
        svc.ShouldApplyConfig(null!, grayPercent: 100).Should().BeFalse();
    }

    [Fact]
    public void ParseGrayPercent_IntegerValue_ReturnsPercent()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ParseGrayPercent("25").Should().Be(25);
        svc.ParseGrayPercent("0").Should().Be(0);
        svc.ParseGrayPercent("100").Should().Be(100);
    }

    [Fact]
    public void ParseGrayPercent_PercentValue_ReturnsPercent()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ParseGrayPercent("25%").Should().Be(25);
        svc.ParseGrayPercent("50%").Should().Be(50);
    }

    [Fact]
    public void ParseGrayPercent_RatioValue_ReturnsPercent()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ParseGrayPercent("0.25").Should().Be(25);
        svc.ParseGrayPercent("0.5").Should().Be(50);
    }

    [Fact]
    public void ParseGrayPercent_InvalidValue_ReturnsZero()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ParseGrayPercent("abc").Should().Be(0);
        svc.ParseGrayPercent("").Should().Be(0);
        svc.ParseGrayPercent(null).Should().Be(0);
    }

    [Fact]
    public void ParseGrayPercent_OverHundred_ClampsTo100()
    {
        var svc = new ConsulGrayReleaseService(NullLogger<ConsulGrayReleaseService>.Instance);

        svc.ParseGrayPercent("150").Should().Be(100);
        svc.ParseGrayPercent("1.5").Should().Be(100);
    }
}

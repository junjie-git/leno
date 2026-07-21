using Leno.Infrastructure.AntiCorruption;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GuidProtoConverterTests
{
    [Fact]
    public void GuidToString_AndBack_ShouldRoundTrip()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();

        // Act
        var str = GuidProtoConverter.ToString(originalGuid);
        var parsed = GuidProtoConverter.TryParse(str, out var resultGuid);

        // Assert
        parsed.Should().BeTrue("Guid 字符串应可解析回 Guid");
        resultGuid.Should().Be(originalGuid, "往返转换应保持一致");
    }

    [Fact]
    public void ToString_ShouldNotUseGetHashCode()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var str = GuidProtoConverter.ToString(guid);

        // Assert — 应为 Guid.ToString() 而非 GetHashCode 的数字表示
        str.Should().NotBe(guid.GetHashCode().ToString(),
            "不应使用 GetHashCode()，应为 Guid.ToString() 格式");
        str.Should().Be(guid.ToString("D"),
            "应为 Guid 的 D 格式（默认）");
    }

    [Fact]
    public void TryParse_InvalidString_ShouldReturnFalse()
    {
        // Arrange
        var invalid = "not-a-guid";

        // Act
        var parsed = GuidProtoConverter.TryParse(invalid, out var result);

        // Assert
        parsed.Should().BeFalse("无效字符串应返回 false");
        result.Should().Be(Guid.Empty, "无效字符串应返回 Guid.Empty");
    }

    [Fact]
    public void ToString_EmptyGuid_ShouldReturnEmptyString()
    {
        // Arrange & Act
        var str = GuidProtoConverter.ToString(Guid.Empty);

        // Assert
        str.Should().Be(Guid.Empty.ToString("D"),
            "Guid.Empty 应返回 00000000-0000-0000-0000-000000000000");
    }

    [Fact]
    public void TryParse_NullOrWhitespace_ShouldReturnFalse()
    {
        // Arrange & Act & Assert — null、空字符串、纯空白都应解析失败
        GuidProtoConverter.TryParse(null, out var r1).Should().BeFalse("null 应返回 false");
        r1.Should().Be(Guid.Empty);

        GuidProtoConverter.TryParse(string.Empty, out var r2).Should().BeFalse("空字符串应返回 false");
        r2.Should().Be(Guid.Empty);

        GuidProtoConverter.TryParse("   ", out var r3).Should().BeFalse("纯空白应返回 false");
        r3.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Parse_InvalidString_ShouldThrowFormatException()
    {
        // Arrange
        var invalid = "not-a-guid";

        // Act
        var act = () => GuidProtoConverter.Parse(invalid);

        // Assert
        act.Should().Throw<FormatException>("无效字符串应抛 FormatException");
    }

    [Fact]
    public void Parse_ValidString_ShouldReturnGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var str = guid.ToString("D");

        // Act
        var parsed = GuidProtoConverter.Parse(str);

        // Assert
        parsed.Should().Be(guid, "有效字符串应返回对应 Guid");
    }
}

using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

/// <summary>
/// 告警静默匹配器值对象单元测试，覆盖匹配逻辑（精确/正则）、相等性与字段校验。
/// </summary>
public class AlertMatcherTests
{
    private const string ValidName = "module";
    private const string ValidValue = "Payment";

    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidParameters_ShouldSetProperties()
    {
        var matcher = new AlertMatcher(ValidName, ValidValue, isRegex: false);

        matcher.Name.Should().Be(ValidName);
        matcher.Value.Should().Be(ValidValue);
        matcher.IsRegex.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldTrimFields()
    {
        var matcher = new AlertMatcher("  " + ValidName + "  ", "  " + ValidValue + "  ", isRegex: true);

        matcher.Name.Should().Be(ValidName);
        matcher.Value.Should().Be(ValidValue);
        matcher.IsRegex.Should().BeTrue();
    }

    #endregion

    #region Constructor - Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowNameEmpty(string? name)
    {
        var act = () => new AlertMatcher(name!, ValidValue, false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MATCHER_NAME_EMPTY");
    }

    [Fact]
    public void Constructor_WithTooLongName_ShouldThrowNameLength()
    {
        var name = new string('n', 129);

        var act = () => new AlertMatcher(name, ValidValue, false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MATCHER_NAME_LENGTH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidValue_ShouldThrowValueEmpty(string? value)
    {
        var act = () => new AlertMatcher(ValidName, value!, false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MATCHER_VALUE_EMPTY");
    }

    [Fact]
    public void Constructor_WithTooLongValue_ShouldThrowValueLength()
    {
        var value = new string('v', 257);

        var act = () => new AlertMatcher(ValidName, value, false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ALERT_MATCHER_VALUE_LENGTH");
    }

    #endregion

    #region Matches - Exact

    [Fact]
    public void Matches_ExactMatch_ShouldReturnTrue()
    {
        var matcher = new AlertMatcher("module", "Payment", isRegex: false);
        var labels = new Dictionary<string, string> { ["module"] = "Payment" };

        matcher.Matches(labels).Should().BeTrue();
    }

    [Fact]
    public void Matches_ExactMismatch_ShouldReturnFalse()
    {
        var matcher = new AlertMatcher("module", "Payment", isRegex: false);
        var labels = new Dictionary<string, string> { ["module"] = "Order" };

        matcher.Matches(labels).Should().BeFalse();
    }

    [Fact]
    public void Matches_MissingLabel_ShouldReturnFalse()
    {
        var matcher = new AlertMatcher("module", "Payment", isRegex: false);
        var labels = new Dictionary<string, string> { ["severity"] = "critical" };

        matcher.Matches(labels).Should().BeFalse();
    }

    [Fact]
    public void Matches_CaseSensitive_ShouldReturnFalseForDifferentCase()
    {
        var matcher = new AlertMatcher("module", "Payment", isRegex: false);
        var labels = new Dictionary<string, string> { ["module"] = "payment" };

        matcher.Matches(labels).Should().BeFalse();
    }

    [Fact]
    public void Matches_NullLabels_ShouldThrowArgumentNullException()
    {
        var matcher = new AlertMatcher(ValidName, ValidValue, false);

        var act = () => matcher.Matches(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Matches - Regex

    [Fact]
    public void Matches_RegexMatch_ShouldReturnTrue()
    {
        var matcher = new AlertMatcher("module", "Pay.*", isRegex: true);
        var labels = new Dictionary<string, string> { ["module"] = "Payment" };

        matcher.Matches(labels).Should().BeTrue();
    }

    [Fact]
    public void Matches_RegexWithAnchor_ShouldMatchExactPattern()
    {
        var matcher = new AlertMatcher("severity", "^(critical|warning)$", isRegex: true);
        var labels = new Dictionary<string, string> { ["severity"] = "critical" };

        matcher.Matches(labels).Should().BeTrue();
    }

    [Fact]
    public void Matches_RegexMismatch_ShouldReturnFalse()
    {
        var matcher = new AlertMatcher("module", "^Order.*", isRegex: true);
        var labels = new Dictionary<string, string> { ["module"] = "Payment" };

        matcher.Matches(labels).Should().BeFalse();
    }

    [Fact]
    public void Matches_InvalidRegex_ShouldFallbackToExactMatch()
    {
        // 无效正则 "[" 应回退到精确匹配，避免规则配置错误导致告警丢失
        var matcher = new AlertMatcher("module", "[", isRegex: true);
        var labels = new Dictionary<string, string> { ["module"] = "[" };

        matcher.Matches(labels).Should().BeTrue();
    }

    [Fact]
    public void Matches_InvalidRegexFallback_ShouldReturnFalseForMismatch()
    {
        var matcher = new AlertMatcher("module", "[", isRegex: true);
        var labels = new Dictionary<string, string> { ["module"] = "Payment" };

        matcher.Matches(labels).Should().BeFalse();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equals_SameValues_ShouldReturnTrue()
    {
        var a = new AlertMatcher("module", "Payment", isRegex: false);
        var b = new AlertMatcher("module", "Payment", isRegex: false);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentName_ShouldReturnFalse()
    {
        var a = new AlertMatcher("module", "Payment", isRegex: false);
        var b = new AlertMatcher("severity", "Payment", isRegex: false);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentValue_ShouldReturnFalse()
    {
        var a = new AlertMatcher("module", "Payment", isRegex: false);
        var b = new AlertMatcher("module", "Order", isRegex: false);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentIsRegex_ShouldReturnFalse()
    {
        var a = new AlertMatcher("module", "Payment", isRegex: false);
        var b = new AlertMatcher("module", "Payment", isRegex: true);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var a = new AlertMatcher("module", "Payment", isRegex: false);

        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_ShouldWork()
    {
        object a = new AlertMatcher("module", "Payment", isRegex: false);
        object b = new AlertMatcher("module", "Payment", isRegex: false);

        a.Equals(b).Should().BeTrue();
    }

    #endregion
}
